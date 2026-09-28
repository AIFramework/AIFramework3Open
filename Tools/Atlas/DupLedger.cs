using System.Text.Encodings.Web;
using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Решение по паре: что это и почему. <c>HashA</c>/<c>HashB</c> — отпечатки тел на момент
/// решения; <c>*</c> — решение о паре типов целиком, оно действует на любые их члены.
/// </summary>
public sealed record DupDecision(string A, string HashA, string B, string HashB, string Verdict, string Reason, DateTime At);

/// <summary>
/// Журнал решений по дублям: принятое решение не всплывает в отчёте повторно, пока код пары
/// не изменился.
/// </summary>
/// <remarks>
/// <para>
/// Ключ — пара методов и отпечатки их тел. Изменилось тело — решение открывается заново:
/// «перекрытие», признанное для старой версии, про новую ничего не говорит.
/// </para>
/// <para>
/// Журнал локальный (<c>%LOCALAPPDATA%\Atlas</c>) — так выбрано в плане. При первом открытии
/// он наполняется тремя парами из <c>Docs/Architecture/Econometrics.md</c>, которые при
/// ревизии сознательно оставлены раздельными.
/// </para>
/// </remarks>
public sealed class DupLedger
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly DupDecision[] Seeds =
    [
        Seed("AI.Econometrics.StateSpace", "AI.ControlSystems.Observers.KalmanFilter",
            "общая только рекурсия: в управлении — матричный фильтр общего вида, здесь — скалярная специализация с правдоподобием и сглаживанием Дурбина — Купмана"),
        Seed("AI.Econometrics.Numerics.LinearAlgebra.Cholesky", "AI.ClassicMath.MatrixUtils.Cholesky",
            "разные контракты: double[,] с гребнем и null при вырождении против Matrix с исключением; делегирование добавило бы преобразование в горячий цикл МНК"),
        Seed("AI.Econometrics.Numerics.Ols", "AI.ML.Regression.MultipleRegression",
            "разные задачи: вывод (стандартные ошибки, (X'X)⁻¹, R²) против предсказания (обучение, масштабирование)"),
    ];

    private readonly string _path;
    private readonly List<DupDecision> _decisions;

    private DupLedger(string path, List<DupDecision> decisions)
    {
        _path = path;
        _decisions = decisions;
    }

    /// <summary>Решения журнала.</summary>
    public IReadOnlyList<DupDecision> Decisions => _decisions;

    /// <summary>Открывает журнал; нет файла — создаёт с решениями из документации.</summary>
    public static DupLedger Open(string path)
    {
        if (File.Exists(path))
            return new DupLedger(path, JsonSerializer.Deserialize<List<DupDecision>>(File.ReadAllText(path), Json) ?? []);

        var ledger = new DupLedger(path, [.. Seeds]);
        ledger.Save();
        return ledger;
    }

    /// <summary>
    /// Решение, действующее для пары; <c>null</c> — решения нет или код изменился после него.
    /// </summary>
    public DupDecision? Find(CodeUnit a, CodeUnit b) =>
        _decisions.LastOrDefault(decision => Covers(decision, a, b) || Covers(decision, b, a));

    /// <summary>Есть ли решение, которое код уже пережил: для отчёта «переоткрыто».</summary>
    public bool IsReopened(CodeUnit a, CodeUnit b) => Outdated(a, b) != null;

    /// <summary>Последнее решение о паре, которое код уже пережил; <c>null</c> — такого нет.</summary>
    public DupDecision? Outdated(CodeUnit a, CodeUnit b) =>
        Find(a, b) is null ? _decisions.LastOrDefault(decision => Names(decision, a, b) || Names(decision, b, a)) : null;

    /// <summary>Записывает решение о паре методов с текущими отпечатками тел.</summary>
    public DupDecision Decide(CodeUnit a, CodeUnit b, string verdict, string reason)
    {
        var decision = new DupDecision(Key(a), a.Hash, Key(b), b.Hash, verdict, reason, DateTime.UtcNow);
        _decisions.Add(decision);
        Save();
        return decision;
    }

    /// <summary>Ключ метода в журнале: сборка и идентификатор.</summary>
    public static string Key(CodeUnit unit) => unit.Project + "|" + unit.Id;

    private static bool Covers(DupDecision decision, CodeUnit a, CodeUnit b) =>
        decision.HashA == "*"
            ? Within(a, decision.A) && Within(b, decision.B)
            : decision.A == Key(a) && decision.B == Key(b) && decision.HashA == a.Hash && decision.HashB == b.Hash;

    private static bool Names(DupDecision decision, CodeUnit a, CodeUnit b) =>
        decision.HashA != "*" && decision.A == Key(a) && decision.B == Key(b);

    /// <summary>Единица — сам тип решения, его член или член его вложенного типа.</summary>
    private static bool Within(CodeUnit unit, string type)
    {
        string name = ApiSearch.QualifiedName(unit.Id);
        return name == type || name.StartsWith(type + ".", StringComparison.Ordinal);
    }

    private static DupDecision Seed(string a, string b, string reason) =>
        new(a, "*", b, "*", "перекрытие", reason + " (Docs/Architecture/Econometrics.md)", new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc));

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_decisions, Json));
    }
}
