namespace AI.Script.Binding;

/// <summary>
/// Реестр зарегистрированных модулей и их функций.
/// </summary>
/// <remarks>
/// Реестр читают трое: проверяющий (существует ли имя и подходят ли аргументы), интерпретатор
/// (что вызвать) и построитель манифеста (что рассказать модели). Один источник на всех — то
/// же решение, что и в <c>ToolRegistry</c>: разойтись им нельзя по построению.
/// </remarks>
public sealed class FunctionRegistry
{
    private readonly Dictionary<string, ScriptFunction> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ScriptFunction>> _namespaces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ScriptFunction> _methods = new(StringComparer.Ordinal);
    private readonly List<IScriptModule> _modules = [];

    /// <summary>Зарегистрированные модули.</summary>
    public IReadOnlyList<IScriptModule> Modules => _modules;

    /// <summary>Полные имена всех функций.</summary>
    public IReadOnlyCollection<string> FunctionNames => _functions.Keys;

    /// <summary>Имена всех пространств.</summary>
    public IReadOnlyCollection<string> Namespaces => _namespaces.Keys;

    /// <summary>Число функций.</summary>
    public int Count => _functions.Count;

    /// <summary>Регистрирует модуль; повторное имя функции перекрывает прежнее.</summary>
    public void Add(IScriptModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        _modules.Add(module);

        if (!_namespaces.TryGetValue(module.Name, out List<ScriptFunction>? bucket))
        {
            bucket = [];
            _namespaces[module.Name] = bucket;
        }

        foreach (ScriptFunction function in module.Functions)
        {
            _functions[function.FullName] = function;
            bucket.Add(function);

            if (function.MethodOf != null) _methods[MethodKey(function.MethodOf, function.Name)] = function;
        }
    }

    /// <summary>Ищет функцию по полному имени.</summary>
    public bool TryGet(string fullName, out ScriptFunction function) => _functions.TryGetValue(fullName, out function!);

    /// <summary>Ищет функцию по полному имени; <c>null</c>, если её нет.</summary>
    public ScriptFunction? Find(string fullName) => _functions.TryGetValue(fullName, out ScriptFunction? function) ? function : null;

    /// <summary>Ищет метод дескриптора.</summary>
    public bool TryGetMethod(string handleType, string name, out ScriptFunction function) =>
        _methods.TryGetValue(MethodKey(handleType, name), out function!);

    /// <summary>Есть ли такое пространство имён.</summary>
    public bool HasNamespace(string name) => _namespaces.ContainsKey(name);

    /// <summary>Функции пространства имён.</summary>
    public IReadOnlyList<ScriptFunction> InNamespace(string name) =>
        _namespaces.TryGetValue(name, out List<ScriptFunction>? bucket) ? bucket : [];

    private static string MethodKey(string handleType, string name) => $"{handleType}::{name}";
}
