using System.Reflection;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Tools;

namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Доступ к закрытой логике <see cref="NativeToolCallPolicy"/>: простая схема <c>{"input": string}</c> для
/// инструмента без своей схемы и обратное разворачивание аргумента.
/// </summary>
/// <remarks>
/// Логика переиспользуется, а не копируется: адаптер обязан строить ту же схему, что видит модель
/// в старом движке, и разворачивать аргумент так же. Методы закрытые, а правка AI.LLM (даже
/// <c>InternalsVisibleTo</c>) изменила бы его снимок API, поэтому связь идет через отражение.
/// Если метод переименуют или поменяют сигнатуру, адаптер падает с понятной ошибкой при первом
/// обращении, а тест <c>NativePolicyBridgeTests</c> ловит это на сборке.
/// </remarks>
internal static class NativePolicyBridge
{
    private static readonly Lazy<Func<string, string>> PlainSchemaMethod =
        new(() => Bind<Func<string, string>>("BuildPlainSchema", typeof(string)));

    private static readonly Lazy<Func<string, string, IReadOnlyList<IReActTool>, string>> UnwrapMethod =
        new(() => Bind<Func<string, string, IReadOnlyList<IReActTool>, string>>(
            "Unwrap", typeof(string), typeof(string), typeof(IReadOnlyList<IReActTool>)));

    /// <summary>Схема единственного строкового аргумента, как ее строит <see cref="NativeToolCallPolicy"/>.</summary>
    public static string BuildPlainSchema(string? description) => PlainSchemaMethod.Value(description!);

    /// <summary>
    /// Разворачивает <c>{"input":"…"}</c> в простую строку для инструмента без своей схемы, как
    /// <see cref="NativeToolCallPolicy"/>; инструменту со схемой аргумент отдается как есть.
    /// </summary>
    public static string Unwrap(IReActTool tool, string arguments) => UnwrapMethod.Value(tool.Name, arguments, [tool]);

    private static T Bind<T>(string name, params Type[] parameters)
        where T : Delegate
    {
        MethodInfo? method = typeof(NativeToolCallPolicy).GetMethod(
            name, BindingFlags.NonPublic | BindingFlags.Static, binder: null, parameters, modifiers: null);

        if (method == null || method.ReturnType != typeof(string))
        {
            throw new MissingMethodException(
                $"В {nameof(NativeToolCallPolicy)} нет закрытого статического метода {name}" +
                $"({string.Join(", ", parameters.Select(p => p.Name))}) → string. Адаптеры AI.Agents.Interop " +
                "переиспользуют его через отражение; после переименования поправьте NativePolicyBridge.");
        }

        return method.CreateDelegate<T>();
    }
}
