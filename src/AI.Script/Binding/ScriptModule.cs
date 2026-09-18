using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Reflection;
using System.Text;

namespace AI.Script.Binding;

/// <summary>
/// Модуль языка, собранный из класса с атрибутами либо построенный вручную.
/// </summary>
public sealed class ScriptModule : IScriptModule
{
    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public string Version { get; }

    /// <inheritdoc/>
    public string Group { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ScriptFunction> Functions { get; }

    /// <summary>Создаёт модуль из готового списка функций.</summary>
    public ScriptModule(
        string name,
        string description,
        string version,
        IReadOnlyList<ScriptFunction> functions,
        string group = "")
    {
        Name = name;
        Description = description;
        Version = version;
        Functions = functions;
        Group = group ?? string.Empty;
    }

    /// <summary>Собирает модуль из статических методов типа.</summary>
    public static ScriptModule FromType<T>() => Build(null, typeof(T));

    /// <summary>Собирает модуль из статических методов типа.</summary>
    public static ScriptModule FromType(Type type) => Build(null, type);

    /// <summary>Собирает модуль из методов экземпляра: для модулей с состоянием.</summary>
    public static ScriptModule FromObject(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return Build(instance, instance.GetType());
    }

    private static ScriptModule Build(object? instance, Type type)
    {
        var moduleAttribute = type.GetCustomAttribute<ScriptModuleAttribute>()
            ?? throw new InvalidOperationException(
                $"Тип {type.Name} не помечен [ScriptModule]: модулем языка он стать не может.");

        var functions = new List<ScriptFunction>();

        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            var attribute = method.GetCustomAttribute<ScriptFnAttribute>();
            if (attribute == null) continue;

            if (!method.IsStatic && instance == null)
            {
                throw new InvalidOperationException(
                    $"{type.Name}.{method.Name} — метод экземпляра, но модуль собирается из типа. " +
                    "Используйте ScriptModule.FromObject.");
            }

            functions.Add(BuildFunction(moduleAttribute.Name, instance, method, attribute));
        }

        return new ScriptModule(
            moduleAttribute.Name, moduleAttribute.Description, moduleAttribute.Version, functions, moduleAttribute.Group);
    }

    private static ScriptFunction BuildFunction(string ns, object? instance, MethodInfo method, ScriptFnAttribute attribute)
    {
        string name = attribute.Name ?? ToSnakeCase(method.Name);
        string fullName = $"{ns}.{name}";

        ParameterInfo[] clrParameters = method.GetParameters();
        var scriptParameters = new List<ScriptParameter>();
        var mapping = new List<int>();

        for (int i = 0; i < clrParameters.Length; i++)
        {
            ParameterInfo parameter = clrParameters[i];

            if (parameter.ParameterType == typeof(CancellationToken)) continue;
            if (typeof(IScriptContext).IsAssignableFrom(parameter.ParameterType)) continue;

            if (!Marshaller.IsSupported(parameter.ParameterType))
            {
                throw new InvalidOperationException(
                    $"{fullName}: параметр '{parameter.Name}' имеет тип {parameter.ParameterType.Name}, " +
                    "непригодный для привязки (ref/out/указатель).");
            }

            var parameterAttribute = parameter.GetCustomAttribute<ScriptParamAttribute>();

            scriptParameters.Add(new ScriptParameter
            {
                Name = parameterAttribute?.Name ?? ToSnakeCase(parameter.Name ?? $"arg{i}"),
                Type = Marshaller.TypeOf(parameter.ParameterType),
                Description = parameterAttribute?.Description ?? string.Empty,
                IsOptional = parameter.HasDefaultValue,
                Default = parameter.HasDefaultValue ? Marshaller.FromClr(DefaultOf(parameter)) : ScriptValue.None,
                IsVariadic = parameterAttribute?.Variadic ?? false,
            });

            mapping.Add(i);
        }

        Type returnType = UnwrapTask(method.ReturnType);
        var methodAttribute = method.GetCustomAttribute<ScriptMethodAttribute>();
        string? unavailable = (instance as IScriptAvailability)?.Unavailable(name);

        var function = new ScriptFunction
        {
            Namespace = ns,
            Name = name,
            // Пометка в описании, а не в отдельном поле справки: описание показывают все виды
            // справки и поиск, и ни один из них не забудет сказать, что функция не подключена.
            Description = unavailable is null ? attribute.Description : $"[не подключено] {attribute.Description}",
            Unavailable = unavailable,
            Example = attribute.Example,
            Parameters = scriptParameters,
            ReturnType = attribute.Returns != null ? ScriptType.Handle : Marshaller.TypeOf(returnType),
            ReturnHandleType = attribute.Returns,
            MethodOf = methodAttribute?.HandleType,
            Reads = SplitList(attribute.Reads),
            Writes = SplitList(attribute.Writes),
            Columns = SplitList(attribute.Columns),
            Invoke = CreateInvoker(fullName, instance, method, clrParameters, scriptParameters, mapping, attribute.Returns),
        };

        return function;
    }

    /// <summary>
    /// Значение по умолчанию параметра.
    /// </summary>
    /// <remarks>
    /// <c>TimeSpan every = default</c> отражение отдает как <c>null</c>, и без поправки длительность
    /// по умолчанию превращалась в пропуск, который проверка отвергает как не тот тип.
    /// </remarks>
    private static object? DefaultOf(ParameterInfo parameter) =>
        parameter.DefaultValue ?? (parameter.ParameterType.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) is null
            ? Activator.CreateInstance(parameter.ParameterType)
            : null);

    private static string[] SplitList(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Func<ScriptValue[], IScriptContext, ValueTask<ScriptValue>> CreateInvoker(
        string fullName,
        object? instance,
        MethodInfo method,
        ParameterInfo[] clrParameters,
        IReadOnlyList<ScriptParameter> scriptParameters,
        IReadOnlyList<int> mapping,
        string? returnHandleType)
    {
        return async (arguments, context) =>
        {
            var call = new object?[clrParameters.Length];

            for (int i = 0; i < clrParameters.Length; i++)
            {
                Type parameterType = clrParameters[i].ParameterType;

                if (parameterType == typeof(CancellationToken)) call[i] = context.Cancellation;
                else if (typeof(IScriptContext).IsAssignableFrom(parameterType)) call[i] = context;
            }

            for (int i = 0; i < mapping.Count; i++)
            {
                int target = mapping[i];

                call[target] = Marshaller.ToClr(
                    arguments[i],
                    clrParameters[target].ParameterType,
                    $"аргумент '{scriptParameters[i].Name}' функции {fullName}");
            }

            object? result;

            try
            {
                result = method.Invoke(instance, call);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw Wrap(fullName, exception.InnerException);
            }
            catch (Exception exception) when (exception is not ScriptError)
            {
                throw Wrap(fullName, exception);
            }

            if (result is Task task)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not ScriptError)
                {
                    throw Wrap(fullName, exception);
                }

                PropertyInfo? resultProperty = task.GetType().GetProperty("Result");
                result = resultProperty?.GetValue(task);
            }

            return Marshaller.FromClr(result, returnHandleType);
        };
    }

    /// <summary>
    /// Оборачивает исключение библиотеки в отказ скрипта.
    /// </summary>
    /// <remarks>
    /// Имя функции в сообщении обязательно: без него автор скрипта видит «Index was outside
    /// the bounds» без единого указания, чей это индекс.
    /// </remarks>
    private static ScriptError Wrap(string fullName, Exception exception)
    {
        if (exception is ScriptError error) return error;
        if (exception is OperationCanceledException) throw exception;

        return new ScriptError(
            DiagnosticCodes.FunctionFailed,
            $"{fullName}: {exception.GetType().Name} — {exception.Message}",
            exception);
    }

    private static Type UnwrapTask(Type type)
    {
        if (type == typeof(Task) || type == typeof(ValueTask)) return typeof(void);

        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>)) return type.GetGenericArguments()[0];
        }

        return type;
    }

    /// <summary>Переводит <c>PascalCase</c> в <c>snake_case</c>.</summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var builder = new StringBuilder(name.Length + 4);

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            if (char.IsUpper(c))
            {
                bool previousLower = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                bool nextLower = i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (i > 0 && (previousLower || nextLower)) _ = builder.Append('_');

                _ = builder.Append(char.ToLowerInvariant(c));
                continue;
            }

            _ = builder.Append(c);
        }

        return builder.ToString();
    }
}
