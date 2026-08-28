using AI.Script.Binding;
using AI.Script.Syntax.Ast;

namespace AI.Script.Runtime;

/// <summary>
/// Функция, объявленная скриптом: <c>fn</c>, <c>stage</c> либо лямбда.
/// </summary>
/// <remarks>
/// Захватывает область объявления, а не вызова: лямбда, отданная в <c>core.map</c>, обязана
/// видеть имена того места, где она записана.
/// </remarks>
public sealed class ScriptClosure : ScriptCallable
{
    /// <inheritdoc/>
    public override string Name { get; }

    /// <summary>Параметры.</summary>
    public IReadOnlyList<ParameterNode> Parameters { get; }

    /// <summary>Тело: выражение либо блок.</summary>
    public Expr Body { get; }

    /// <summary>Захваченная область видимости.</summary>
    public Scope Captured { get; }

    /// <summary>Является ли объявление стадией конвейера.</summary>
    public bool IsStage { get; }

    /// <summary>Разобранные атрибуты стадии; <c>null</c> у обычной функции и лямбды.</summary>
    public StageOptions? Stage { get; }

    /// <summary>
    /// Отпечаток исходного текста объявления — часть ключа кэша.
    /// </summary>
    /// <remarks>
    /// Считается один раз при подъёме объявлений: правка тела стадии обязана обесценить
    /// прежний результат, а сравнивать тексты на каждом вызове незачем.
    /// </remarks>
    public string SourceDigest { get; }

    /// <summary>Документирующий комментарий.</summary>
    public string? Documentation { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<string> ParameterNames
    {
        get
        {
            var names = new List<string>(Parameters.Count);
            foreach (ParameterNode parameter in Parameters) names.Add(parameter.Name);
            return names;
        }
    }

    /// <summary>Создаёт замыкание.</summary>
    public ScriptClosure(
        string name,
        IReadOnlyList<ParameterNode> parameters,
        Expr body,
        Scope captured,
        bool isStage = false,
        string? documentation = null,
        StageOptions? stage = null,
        string? sourceDigest = null)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
        Captured = captured;
        IsStage = isStage;
        Stage = isStage ? stage ?? StageOptions.Default : null;
        SourceDigest = sourceDigest ?? string.Empty;
        Documentation = documentation;
    }
}

/// <summary>Функция модуля в роли значения языка.</summary>
public sealed class NativeFunction : ScriptCallable
{
    /// <summary>Описание функции модуля.</summary>
    public ScriptFunction Function { get; }

    /// <inheritdoc/>
    public override string Name => Function.FullName;

    /// <inheritdoc/>
    public override IReadOnlyList<string> ParameterNames
    {
        get
        {
            var names = new List<string>(Function.Parameters.Count);
            foreach (ScriptParameter parameter in Function.Parameters) names.Add(parameter.Name);
            return names;
        }
    }

    /// <summary>Оборачивает функцию модуля в значение.</summary>
    public NativeFunction(ScriptFunction function) => Function = function;
}
