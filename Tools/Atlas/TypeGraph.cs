using AI.Algorithms.EWG;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Граф типов открытого API: вершины — типы данных, дуги — методы «вход → выход».
/// </summary>
/// <remarks>
/// <para>
/// Входом метода считаются данные, а не настройки: объект, у которого метод вызван, и
/// параметры непримитивных типов. Число, строка, перечисление или делегат — это ручка
/// настройки (порог, имя колонки, функция сравнения), а не то, что течёт по конвейеру.
/// Метод, ничего не возвращающий, отдаёт свой объект: <c>model.Train(data)</c> — это обученная
/// модель, которую дальше спросят <c>Predict</c>.
/// </para>
/// <para>
/// Выход подходит ко входу, если это тот же тип или его предок (базовый класс, интерфейс), а
/// через одно преобразование — если есть оператор приведения или конструктор от него.
/// </para>
/// </remarks>
public sealed class TypeGraph
{
    private static readonly HashSet<string> Primitives =
    [
        "bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal",
        "char", "string", "object", "void", "System.Threading.CancellationToken", "System.Random", "System.TimeSpan",
        "System.DateTime", "System.IFormatProvider", "System.Globalization.CultureInfo", "System.Type",
        "AI.Script.Binding.IScriptContext",
    ];

    /// <summary>Значение AIScript любого типа: подходит к любому входу и принимает любой выход.</summary>
    private const string AnyScriptValue = "AI.Script.Runtime.ScriptValue";

    private static readonly string[] Delegates = ["System.Func<", "System.Action", "System.Predicate<", "System.Comparison<", "System.Converter<"];

    private readonly HashSet<string> _enums;
    private readonly Dictionary<string, HashSet<string>> _supertypes;
    private readonly Dictionary<(string From, string To), CodeUnit> _conversions = [];
    private readonly Dictionary<string, int> _index = [];
    private readonly List<string> _types = [];
    private readonly Dictionary<(int From, int To), (CodeUnit Method, int Usage)> _arcs = [];

    /// <summary>Строит граф по единицам открытого API.</summary>
    /// <param name="units">Единицы: типы дают предков, методы — дуги.</param>
    /// <param name="usage">Сколько мест вызывают метод: подпись дуги — самый обжитый из методов.</param>
    public TypeGraph(IReadOnlyList<CodeUnit> units, IReadOnlyDictionary<(string, string), int>? usage = null)
    {
        ArgumentNullException.ThrowIfNull(units);

        _enums = [.. units.Where(unit => unit.Kind == "type" && unit.Signature.Contains("enum ", StringComparison.Ordinal)).Select(GoldSet.TypeOf)];
        _supertypes = units
            .Where(unit => unit.Kind == "type" && unit.Supertypes.Length > 0)
            .GroupBy(GoldSet.TypeOf)
            .ToDictionary(group => group.Key, group => group.SelectMany(unit => unit.Supertypes.Split('|')).ToHashSet());

        foreach (CodeUnit method in units.Where(unit => unit.Kind != "type"))
        {
            if (DataOutput(method) is not { } output) continue;

            IReadOnlyList<string> inputs = DataInputs(method);
            int used = usage?.GetValueOrDefault((method.Project, method.Id)) ?? 0;

            if (IsConversion(method, inputs)) _conversions.TryAdd((inputs[0], output), method);

            foreach (string input in inputs.Where(input => input != output))
            {
                var arc = (Vertex(input), Vertex(output));
                if (!_arcs.TryGetValue(arc, out var best) || used > best.Usage) _arcs[arc] = (method, used);
            }
        }
    }

    /// <summary>Число типов-вершин.</summary>
    public int TypeCount => _types.Count;

    /// <summary>Число дуг.</summary>
    public int ArcCount => _arcs.Count;

    /// <summary>Данные ли это, а не ручка настройки.</summary>
    public bool IsData(string type)
    {
        string plain = type.TrimEnd('?');

        return plain.Length > 0 && !Primitives.Contains(plain) && !_enums.Contains(plain)
            && !Delegates.Any(prefix => plain.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>Входы-данные метода: объект вызова у экземплярного метода и параметры-данные.</summary>
    public IReadOnlyList<string> DataInputs(CodeUnit method)
    {
        var inputs = new List<string>();

        if (method.Kind == "method" && !method.IsStatic) inputs.Add(GoldSet.TypeOf(method));

        if (method.Inputs.Length > 0) inputs.AddRange(method.Inputs.Split('|').Where(IsData));

        return inputs;
    }

    /// <summary>Выход-данные метода; <c>null</c>, если метод отдаёт только число или ничего.</summary>
    public string? DataOutput(CodeUnit method)
    {
        if (method.Returns == "void") return method.Kind == "method" && !method.IsStatic ? GoldSet.TypeOf(method) : null;

        return IsData(method.Returns) ? method.Returns : null;
    }

    /// <summary>
    /// Как выход <paramref name="from"/> доходит до входа <paramref name="to"/>: 0 — сразу (тот
    /// же тип или предок), 1 — через преобразование <c>Via</c>, -1 — никак.
    /// </summary>
    public (int Distance, CodeUnit? Via) Link(string from, string to)
    {
        if (from == to || from == AnyScriptValue || to == AnyScriptValue
            || (_supertypes.TryGetValue(from, out var parents) && parents.Contains(to))) return (0, null);

        return _conversions.TryGetValue((from, to), out CodeUnit? via) ? (1, via) : (-1, null);
    }

    /// <summary>
    /// До <paramref name="k"/> кратчайших путей от типа к типу; каждый путь — методы по порядку.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<CodeUnit>> Paths(string from, string to, int k = 3)
    {
        if (!_index.TryGetValue(from, out int source) || !_index.TryGetValue(to, out int target) || source == target) return [];

        var graph = new GraphW<BaseEdge>(_types.Count);
        foreach ((int a, int b) in _arcs.Keys) graph.AddArce(a, b);

        return [.. new YenKShortestPaths<BaseEdge>(graph, source, target, k).Paths
            .Select(path => (IReadOnlyList<CodeUnit>)[.. path.Path.Zip(path.Path.Skip(1), (a, b) => _arcs[(a, b)].Method)])];
    }

    /// <summary>
    /// Тип по полному или короткому имени; при нескольких — самый связанный. Короткое имя
    /// находит и коллекцию из таких элементов: «Peak» — это и <c>IReadOnlyList&lt;Peak&gt;</c>.
    /// </summary>
    public string? Resolve(string name)
    {
        if (_index.ContainsKey(name)) return name;

        return _types
            .Select(type => (type, exact: type == name || type.EndsWith("." + name, StringComparison.Ordinal)))
            .Where(pair => pair.exact || pair.type.Contains("." + name + ">", StringComparison.Ordinal) || pair.type.Contains("." + name + "[]", StringComparison.Ordinal))
            .OrderByDescending(pair => pair.exact)
            .ThenByDescending(pair => _arcs.Keys.Count(arc => _types[arc.From] == pair.type || _types[arc.To] == pair.type))
            .Select(pair => pair.type)
            .FirstOrDefault();
    }

    /// <summary>
    /// Преобразование: оператор приведения, конструктор от одного входа-данных или метод
    /// <c>To…</c>/<c>As…</c> без других данных.
    /// </summary>
    private static bool IsConversion(CodeUnit method, IReadOnlyList<string> inputs)
    {
        if (inputs.Count != 1) return false;

        string name = ApiSearch.QualifiedName(method.Id);
        string member = name[(name.LastIndexOf('.') + 1)..];

        return method.Kind == "ctor"
            || member is "op_Implicit" or "op_Explicit"
            || member.StartsWith("To", StringComparison.Ordinal) || member.StartsWith("As", StringComparison.Ordinal);
    }

    private int Vertex(string type)
    {
        if (_index.TryGetValue(type, out int index)) return index;

        _index[type] = _types.Count;
        _types.Add(type);
        return _types.Count - 1;
    }
}
