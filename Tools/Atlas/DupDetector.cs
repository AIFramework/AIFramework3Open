using AI.NLP.Similarity;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiFramework.Tools.Atlas;

/// <summary>Признаки пары методов: каждый от 0 до 1.</summary>
/// <param name="Code">Жаккар шинглов нормализованного кода.</param>
/// <param name="Doc">Жаккар слов описаний.</param>
/// <param name="Name">1 — одно имя (без учёта регистра), иначе Жаккар слов имён.</param>
/// <param name="Calls">Жаккар множеств вызываемых методов.</param>
/// <param name="Constants">Жаккар числовых констант, если их хотя бы по две.</param>
/// <param name="Control">Сходство числа циклов, ветвлений, возвратов.</param>
/// <param name="SameInputs">Одинаковые входы-данные (или одинаковые параметры, если данных нет).</param>
/// <param name="SameType">Оба метода в одном типе: перегрузки.</param>
public sealed record DupFeatures(double Code, double Doc, double Name, double Calls, double Constants, double Control, bool SameInputs, bool SameType);

/// <summary>Находка: пара, признаки, вердикт, что делать и какая версия каноническая.</summary>
public sealed record DupFinding(CodeUnit A, CodeUnit B, DupFeatures Features, string Verdict, string Advice, CodeUnit Canonical, string Sources);

/// <summary>
/// Поиск дублей по коду и описаниям: кандидаты из четырёх источников, признаки пары, вердикт.
/// </summary>
/// <remarks>
/// <para>
/// Все пары методов библиотеки — это сотни миллионов сравнений. Поэтому сначала дешёвые
/// источники кандидатов с запасом по полноте: LSH по коду, одно имя при одинаковых входах,
/// похожие описания, общие вызовы. Точные признаки считаются только для кандидатов.
/// </para>
/// <para>
/// Пара, где один метод вызывает другой, — это обёртка, а не дубль: она сознательно
/// переиспользует код. Такие пары отсеиваются до вердикта.
/// </para>
/// <para>
/// Вердикт без исполнения — только по коду и описаниям. «Один предмет, разный код» — это
/// вопрос, а не ответ: дубль по поведению, расхождение или сознательное перекрытие решает
/// исполнение (этап 5) либо модель по пакету доказательств.
/// </para>
/// </remarks>
public sealed class DupDetector
{
    /// <summary>Меньше токенов — тривиальный код (геттер, проброс): сходство таких тел ничего не значит.</summary>
    public const int MinTokens = 30;

    /// <summary>Вердикт «нет связи».</summary>
    public const string Unrelated = "нет связи";

    private const int MaxNameGroup = 12;

    // Для совпадения имени хватает короткого тела: SoftMax в одну строку — такой же дубль,
    // как длинный. Порог MinTokens нужен сравнению кода, где короткие тела похожи все.
    private const int MinNameTokens = 10;
    private const int MaxPostings = 150;

    private static readonly HashSet<string> Primitives =
        ["bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "char", "string", "object"];

    private readonly Profile[] _profiles;
    private readonly Dictionary<(string, string), int> _position = [];
    private readonly IReadOnlyDictionary<(string, string), int> _usage;
    private readonly Func<string, int> _layer;
    private readonly MinHashLsh _lsh = new(bands: 32, rows: 4);

    private sealed record Profile(CodeUnit Unit, CodeShape? Shape, HashSet<string> Doc, HashSet<string> Name, HashSet<string> Calls, string SimpleName, string Inputs, string Output)
    {
        public string Key => Unit.Project + "|" + Unit.Id;

        public bool NonTrivial => Shape is { Tokens: >= MinTokens };

        /// <summary>
        /// Функция, а не поведение объекта: статический или закрытый метод. Одноимённые
        /// открытые методы экземпляра разных классов — это чаще полиморфизм (Refit, Interpret),
        /// закрытый метод переопределением не бывает, а статический — вычисление без состояния.
        /// </summary>
        public bool IsFunction => Unit.IsStatic || Unit.Access == "private";
    }

    /// <summary>Строит профили методов.</summary>
    /// <param name="units">Методы библиотеки с телами.</param>
    /// <param name="callees">Кого вызывает каждый метод: ключи «сборка|id».</param>
    /// <param name="usage">Сколько мест вызывают метод: для выбора канонической версии.</param>
    /// <param name="layer">Слой проекта: чем меньше, тем ближе к ядру.</param>
    /// <param name="shapes">Форма тела единицы; <c>null</c> — разбор каждый раз (<see cref="ShapeCache"/> быстрее).</param>
    public DupDetector(
        IReadOnlyList<CodeUnit> units, IReadOnlyDictionary<(string, string), HashSet<string>> callees,
        IReadOnlyDictionary<(string, string), int> usage, Func<string, int> layer, Func<CodeUnit, CodeShape?>? shapes = null)
    {
        ArgumentNullException.ThrowIfNull(units);

        _usage = usage;
        _layer = layer;
        _profiles = new Profile[units.Count];

        for (int i = 0; i < units.Count; i++)
        {
            _profiles[i] = Build(units[i], callees.GetValueOrDefault((units[i].Project, units[i].Id)) ?? [], shapes);
            _position.TryAdd((units[i].Project, units[i].Id), i);

            if (_profiles[i].NonTrivial) _lsh.Add(i, _profiles[i].Shape!.Signature);
        }
    }

    /// <summary>Число методов под наблюдением.</summary>
    public int Count => _profiles.Length;

    /// <summary>Все пары-кандидаты с вердиктом, кроме «нет связи»; сначала серьёзные.</summary>
    public IReadOnlyList<DupFinding> FindAll()
    {
        var sources = new Dictionary<(int, int), SortedSet<string>>();

        void Add(int a, int b, string source)
        {
            if (a == b) return;
            var key = (Math.Min(a, b), Math.Max(a, b));
            if (!sources.TryGetValue(key, out var set)) sources[key] = set = new SortedSet<string>(StringComparer.Ordinal);
            set.Add(source);
        }

        foreach ((int a, int b) in _lsh.CandidatePairs()) Add(a, b, "код");
        foreach ((int a, int b) in NamePairs()) Add(a, b, "имя");
        foreach ((int a, int b) in OverlapPairs(profile => profile.Doc, minSize: 3, minJaccard: 0.6)) Add(a, b, "описание");
        foreach ((int a, int b) in OverlapPairs(profile => profile.Calls, minSize: 3, minJaccard: 0.7)) Add(a, b, "вызовы");

        return [.. sources
            .Where(pair => !Delegates(_profiles[pair.Key.Item1], _profiles[pair.Key.Item2]))
            .Select(pair => Judge(_profiles[pair.Key.Item1], _profiles[pair.Key.Item2], string.Join("+", pair.Value)))
            .Where(finding => finding.Verdict != Unrelated)
            .OrderBy(finding => Severity(finding.Verdict))
            .ThenByDescending(finding => finding.Features.Code + finding.Features.Doc)];
    }

    /// <summary>Общие вызываемые методы библиотеки (с телом, не конструкторы), ключи «сборка|id».</summary>
    public IReadOnlyList<string> CommonCalls(CodeUnit a, CodeUnit b) =>
        _position.TryGetValue((a.Project, a.Id), out int i) && _position.TryGetValue((b.Project, b.Id), out int j)
            ? [.. _profiles[i].Calls.Intersect(_profiles[j].Calls).Where(IsLibraryHelper).Order(StringComparer.Ordinal)]
            : [];

    /// <summary>Пара по ключам; <c>null</c>, если одного из методов нет среди наблюдаемых.</summary>
    public DupFinding? Compare(CodeUnit a, CodeUnit b) =>
        _position.TryGetValue((a.Project, a.Id), out int i) && _position.TryGetValue((b.Project, b.Id), out int j)
            ? Judge(_profiles[i], _profiles[j], "запрос")
            : null;

    /// <summary>
    /// Черновик против библиотеки: код метода (или только тело) и, по желанию, замысел словами.
    /// </summary>
    /// <returns>Похожие методы библиотеки, самые серьёзные первыми; «нет связи» не попадает.</returns>
    public IReadOnlyList<DupFinding> Check(string code, string? intent)
    {
        Profile draft = Draft(code ?? "", intent);
        var candidates = new HashSet<int>();

        if (draft.Shape is { } shape) candidates.UnionWith(_lsh.Query(shape.Signature));

        for (int i = 0; i < _profiles.Length; i++)
        {
            Profile profile = _profiles[i];
            if ((draft.SimpleName.Length > 0 && profile.SimpleName == draft.SimpleName)
                || (draft.Doc.Count >= 2 && MinHash.Jaccard(draft.Doc, profile.Doc) >= 0.4)) candidates.Add(i);
        }

        return [.. candidates
            .Select(i => Judge(draft, _profiles[i], "черновик") with { Canonical = _profiles[i].Unit })
            .Where(finding => finding.Verdict != Unrelated)
            .OrderBy(finding => Severity(finding.Verdict))
            .ThenByDescending(finding => finding.Features.Code + finding.Features.Doc)
            .Take(10)];
    }

    /// <summary>Порядок вердиктов в отчёте.</summary>
    public static int Severity(string verdict) => verdict switch
    {
        "копия" => 0,
        "похожий код" => 1,
        "один предмет, разный код" => 2,
        "копия в перегрузках" => 3,
        _ => 4,
    };

    private DupFinding Judge(Profile a, Profile b, string sources)
    {
        var features = new DupFeatures(
            Code: a.Shape != null && b.Shape != null ? MinHash.Jaccard(a.Shape.Shingles, b.Shape.Shingles) : 0,
            Doc: MinHash.Jaccard(a.Doc, b.Doc),
            Name: a.SimpleName.Length > 0 && a.SimpleName == b.SimpleName ? 1 : MinHash.Jaccard(a.Name, b.Name),
            Calls: MinHash.Jaccard(a.Calls, b.Calls),
            Constants: a.Shape is { Constants.Count: >= 2 } && b.Shape is { Constants.Count: >= 2 } ? MinHash.Jaccard(a.Shape.Constants, b.Shape.Constants) : 0,
            Control: a.Shape != null && b.Shape != null ? CodeShapes.ControlSimilarity(a.Shape.Control, b.Shape.Control) : 0,
            SameInputs: a.Inputs == b.Inputs && a.Output == b.Output,
            SameType: GoldSet.TypeOf(a.Unit) == GoldSet.TypeOf(b.Unit));

        bool overrides = a.Unit.Signature.Contains("override ", StringComparison.Ordinal) && b.Unit.Signature.Contains("override ", StringComparison.Ordinal);
        (string verdict, string advice) = Verdict(features, CodeEvidence(a) && CodeEvidence(b), a.IsFunction || b.IsFunction, overrides);
        CodeUnit canonical = Canonical(a.Unit, b.Unit);

        return new DupFinding(a.Unit, b.Unit, features, verdict, advice, canonical, sources);
    }

    /// <summary>
    /// Пороги вердикта без исполнения, проверенные на эталоне (<see cref="DupGold"/>) и на
    /// выборке находок: копия с переименованием даёт Жаккар шинглов выше 0.8; при 0.45
    /// «похожим» оказывался шаблонный код соседних методов (Tan и Tanh, Bar и Scatter),
    /// поэтому граница поднята до 0.6, а соседи в одном типе считаются только копией.
    /// </summary>
    /// <remarks>
    /// Переопределения одного базового метода похожи по построению (разбор аргументов,
    /// возврат того же типа) и описаны одинаково через <c>inheritdoc</c>: у них засчитывается
    /// только копия. Совпадение имени или описания — улика лишь для функций (см. <see cref="Profile.IsFunction"/>).
    /// </remarks>
    private static (string Verdict, string Advice) Verdict(DupFeatures f, bool codeEvidence, bool function, bool overrides)
    {
        if (f.Code >= 0.8 && codeEvidence)
        {
            return f.SameType
                ? ("копия в перегрузках", "общий код перегрузок можно вынести в закрытый метод")
                : ("копия", "заменить вызовом канонической версии");
        }

        if (f.SameType || overrides) return (Unrelated, "");

        // Сходство кода 0.6–0.8 без общего имени или описания — это шаблон (пересчёт единиц,
        // присваивание полей в конструкторе, цикл преобразования), а не дубль: на выборке из
        // десяти таких находок настоящей оказалась одна.
        if (codeEvidence && f.Code >= 0.6 && (f.Name >= 0.5 || f.Doc >= 0.5))
            return ("похожий код", "сверить поведение (этап 5) и свести к канонической версии");

        if (function && f.SameInputs && (f.Name >= 0.99 || f.Doc >= 0.6))
            return ("один предмет, разный код", "проверить исполнением: дубль, расхождение или сознательное перекрытие; перекрытие объяснить в комментарии");

        return (Unrelated, "");
    }

    /// <summary>
    /// Годится ли тело для сравнения кода. Не годятся: тривиальное тело; конструктор без логики
    /// (одни присваивания полей — после нормализации все такие конструкторы одинаковы); тело-текст;
    /// оператор (у перегруженных операторов одного типа код одинаков по построению).
    /// </summary>
    private static bool CodeEvidence(Profile profile) =>
        profile.NonTrivial
        && !profile.Shape!.IsText
        && profile.Unit.Kind != "operator"
        && (profile.Unit.Kind != "ctor" || profile.Shape.Control[0] + profile.Shape.Control[1] >= 2);

    /// <summary>Каноническая версия: ниже по слою, больше вызывающих, есть описание.</summary>
    private CodeUnit Canonical(CodeUnit a, CodeUnit b) =>
        new[] { a, b }
            .OrderBy(unit => _layer(unit.Project))
            .ThenByDescending(unit => _usage.GetValueOrDefault((unit.Project, unit.Id)))
            .ThenByDescending(unit => unit.Doc.Length > 0)
            .ThenBy(unit => unit.Id, StringComparer.Ordinal)
            .First();

    private bool IsLibraryHelper(string key)
    {
        int bar = key.IndexOf('|');
        return bar > 0 && !key.Contains("#ctor", StringComparison.Ordinal) && _position.ContainsKey((key[..bar], key[(bar + 1)..]));
    }

    private static bool Delegates(Profile a, Profile b) => a.Calls.Contains(b.Key) || b.Calls.Contains(a.Key);

    private IEnumerable<(int, int)> NamePairs() =>
        Enumerable.Range(0, _profiles.Length)
            .Where(i => _profiles[i].Shape is { Tokens: >= MinNameTokens } || _profiles[i].Doc.Count > 0)
            .GroupBy(i => _profiles[i].SimpleName)
            .Where(group => group.Key.Length > 0 && group.Count() is >= 2 and <= MaxNameGroup)
            .SelectMany(group => group.SelectMany(i => group.Where(j => j > i).Select(j => (i, j))))
            .Where(pair => GoldSet.TypeOf(_profiles[pair.i].Unit) != GoldSet.TypeOf(_profiles[pair.j].Unit));

    /// <summary>
    /// Пары с большим пересечением множеств через обратный индекс; частые элементы (есть в
    /// сотнях методов) пропускаются: они ничего не различают.
    /// </summary>
    private IEnumerable<(int, int)> OverlapPairs(Func<Profile, HashSet<string>> set, int minSize, double minJaccard)
    {
        var postings = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (int i = 0; i < _profiles.Length; i++)
        {
            if (set(_profiles[i]).Count < minSize) continue;
            foreach (string item in set(_profiles[i]))
            {
                if (!postings.TryGetValue(item, out var list)) postings[item] = list = [];
                list.Add(i);
            }
        }

        var overlap = new Dictionary<(int, int), int>();

        foreach (List<int> list in postings.Values.Where(list => list.Count is >= 2 and <= MaxPostings))
            for (int x = 0; x < list.Count; x++)
                for (int y = x + 1; y < list.Count; y++)
                    overlap[(list[x], list[y])] = overlap.GetValueOrDefault((list[x], list[y])) + 1;

        foreach (((int a, int b), int common) in overlap)
        {
            int union = set(_profiles[a]).Count + set(_profiles[b]).Count - common;
            if ((double)common / union >= minJaccard) yield return (a, b);
        }
    }

    private static Profile Build(CodeUnit unit, HashSet<string> calls, Func<CodeUnit, CodeShape?>? shapes)
    {
        string name = ApiSearch.QualifiedName(unit.Id);
        string simple = name[(name.LastIndexOf('.') + 1)..];
        if (simple == "#ctor") simple = "";

        return new Profile(
            unit,
            shapes is null ? CodeShapes.Of(unit.Body) : shapes(unit),
            [.. CodeTokenizer.Tokens(ApiSearch.DocText(unit.Doc))],
            [.. CodeTokenizer.Tokens(simple)],
            calls,
            simple.ToLowerInvariant(),
            DataInputs(unit.Inputs),
            Output(unit.Returns));
    }

    /// <summary>Выход для сравнения: короткое имя типа, все числовые типы — одно «число».</summary>
    private static string Output(string returns)
    {
        string type = System.Text.RegularExpressions.Regex.Replace(returns, @"\w+\.", "");
        return type is "int" or "long" or "short" or "byte" or "uint" or "ulong" or "float" or "double" or "decimal" ? "num" : type;
    }

    /// <summary>
    /// Входы-данные для сравнения: непримитивные параметры, а у метода только с примитивами —
    /// все параметры. Так <c>Sigmoid(Vector, double)</c> и <c>Sigmoid(Vector)</c> совпадают, а
    /// <c>Gamma(double)</c> и <c>Gamma(int)</c> — нет.
    /// </summary>
    private static string DataInputs(string inputs)
    {
        // Короткие имена типов: у черновика в коде «Vector», в индексе — полное имя.
        string[] all = inputs.Length > 0 ? [.. inputs.Split('|').Select(type => System.Text.RegularExpressions.Regex.Replace(type, @"\w+\.", ""))] : [];
        string[] data = [.. all.Where(type => !Primitives.Contains(type.TrimEnd('?')))];
        return string.Join('|', data.Length > 0 ? data : all);
    }

    /// <summary>Профиль черновика: объявление метода целиком либо только тело.</summary>
    private static Profile Draft(string code, string? intent)
    {
        MethodDeclarationSyntax? method = CSharpSyntaxTree.ParseText($"class __C {{ {code} }}").GetRoot()
            .DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Body != null || m.ExpressionBody != null);

        string body = method?.Body?.ToString() ?? method?.ExpressionBody?.ToString() ?? (code.TrimStart().StartsWith('{') ? code : "{ " + code + " }");
        string name = method?.Identifier.ValueText ?? "";
        string inputs = method is null ? "" : string.Join('|', method.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? ""));
        string returns = method?.ReturnType.ToString() ?? "";
        string doc = string.Join(' ', intent ?? "", method?.GetLeadingTrivia().ToString() ?? "");

        // Черновик считается функцией: его сравнивают с библиотекой, чтобы не написать её второй раз.
        var unit = new CodeUnit("(черновик)", $"M:Черновик.{(name.Length > 0 ? name : "Метод")}", "method", "private", "(черновик)", 0,
            method?.WithBody(null).WithExpressionBody(null).ToString().Trim() ?? "(тело)", "", body, null, "", returns);

        return new Profile(unit, CodeShapes.Of(body), [.. CodeTokenizer.Tokens(doc)], [.. CodeTokenizer.Tokens(name)], [],
            name.ToLowerInvariant(), DataInputs(inputs), Output(returns));
    }
}
