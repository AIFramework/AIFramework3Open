namespace AI.Script.Runtime;

/// <summary>
/// Область видимости: собственные имена плюс ссылка на объемлющую.
/// </summary>
/// <remarks>
/// Лексическая, а не динамическая: лямбда, переданная в <c>core.map</c>, обязана видеть имена
/// того места, где она записана, а не того, где её вызвали.
/// </remarks>
public sealed class Scope
{
    private readonly Dictionary<string, ScriptValue> _values = new(StringComparer.Ordinal);

    /// <summary>Объемлющая область; <c>null</c> у глобальной.</summary>
    public Scope? Parent { get; }

    /// <summary>Создаёт область.</summary>
    /// <param name="parent">Объемлющая область.</param>
    public Scope(Scope? parent = null) => Parent = parent;

    /// <summary>Объявлено ли имя именно в этой области.</summary>
    public bool DeclaredHere(string name) => _values.ContainsKey(name);

    /// <summary>Объявляет имя в этой области.</summary>
    public void Declare(string name, ScriptValue value) => _values[name] = value;

    /// <summary>Ищет имя вверх по цепочке.</summary>
    public bool TryGet(string name, out ScriptValue value)
    {
        for (Scope? scope = this; scope != null; scope = scope.Parent)
        {
            if (scope._values.TryGetValue(name, out value)) return true;
        }

        value = ScriptValue.None;
        return false;
    }

    /// <summary>Присваивает уже связанному имени; <c>false</c>, если имя не найдено.</summary>
    public bool TryAssign(string name, ScriptValue value)
    {
        for (Scope? scope = this; scope != null; scope = scope.Parent)
        {
            if (!scope._values.ContainsKey(name)) continue;

            scope._values[name] = value;
            return true;
        }

        return false;
    }

    /// <summary>Имена, объявленные в этой области.</summary>
    public IReadOnlyCollection<string> Names => _values.Keys;
}
