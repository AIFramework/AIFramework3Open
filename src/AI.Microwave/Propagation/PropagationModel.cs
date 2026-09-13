using Vector3 = AI.Geometry.Primitives.Vector3;

namespace AI.Microwave.Propagation;

/// <summary>
/// Потери на линии как случайная величина: одно или два состояния — с прямой видимостью и без неё, у каждого
/// медиана и логнормальное затенение.
/// </summary>
/// <remarks>
/// Модели без деления на состояния (свободное пространство, Хата) задают одно состояние с вероятностью 1.
/// Затенение складывается с медианой в децибелах: потери равны L + σ·z, z ~ N(0, 1).
/// </remarks>
/// <param name="LineOfSightProbability">Вероятность прямой видимости</param>
/// <param name="LineOfSightDb">Медианные потери с прямой видимостью, дБ</param>
/// <param name="LineOfSightSigmaDb">СКО затенения с прямой видимостью, дБ</param>
/// <param name="NonLineOfSightDb">Медианные потери без прямой видимости, дБ</param>
/// <param name="NonLineOfSightSigmaDb">СКО затенения без прямой видимости, дБ</param>
public readonly record struct LinkLoss(
    double LineOfSightProbability,
    double LineOfSightDb,
    double LineOfSightSigmaDb,
    double NonLineOfSightDb,
    double NonLineOfSightSigmaDb)
{
    /// <summary>Одно состояние</summary>
    /// <param name="lossDb">Медианные потери, дБ</param>
    /// <param name="sigmaDb">СКО затенения, дБ</param>
    public static LinkLoss Single(double lossDb, double sigmaDb) => new(1, lossDb, sigmaDb, lossDb, sigmaDb);

    /// <summary>
    /// Потери, усреднённые по состоянию видимости по мощности, без затенения:
    /// −10·lg(p·10^(−L₁/10) + (1 − p)·10^(−L₂/10)), дБ
    /// </summary>
    public double MeanDb
    {
        get
        {
            double p = LineOfSightProbability;

            return -10 * Math.Log10((p * Math.Pow(10, -LineOfSightDb / 10)) + ((1 - p) * Math.Pow(10, -NonLineOfSightDb / 10)));
        }
    }

    /// <summary>
    /// Вероятность, что потери с затенением не превысят допустимых: смесь по состояниям видимости
    /// p·Φ((A − L₁)/σ₁) + (1 − p)·Φ((A − L₂)/σ₂)
    /// </summary>
    /// <param name="allowedLossDb">Допустимые потери A, дБ</param>
    /// <param name="extraSigmaDb">
    /// Независимый добавочный разброс, дБ, — например проникновения в здание; складывается с затенением
    /// в квадратуре
    /// </param>
    public double ProbabilityWithin(double allowedLossDb, double extraSigmaDb = 0)
    {
        double Within(double loss, double sigma)
            => LinkBudget.CoverageProbability(allowedLossDb, loss, Math.Sqrt((sigma * sigma) + (extraSigmaDb * extraSigmaDb)));

        double p = LineOfSightProbability;

        return p >= 1
            ? Within(LineOfSightDb, LineOfSightSigmaDb)
            : (p * Within(LineOfSightDb, LineOfSightSigmaDb)) + ((1 - p) * Within(NonLineOfSightDb, NonLineOfSightSigmaDb));
    }
}

/// <summary>
/// Модель потерь на трассе для карт покрытия: статистика потерь между двумя точками и расстояния
/// корреляции, по которым строятся карты затенения и состояния видимости.
/// </summary>
/// <remarks>
/// Координаты — метры: X на восток, Y на север, Z — высота антенны над землёй. Реализации: свободное
/// пространство (<see cref="FreeSpaceModel"/>), логарифмическая модель (<see cref="LogDistanceModel"/>),
/// Окумура — Хата (<see cref="HataModel"/>) и сценарии TR 38.901 (<see cref="Tr38901Scenario"/>).
/// </remarks>
public interface IPropagationModel
{
    /// <summary>Название модели</summary>
    string Name { get; }

    /// <summary>Расстояние корреляции затенения, м</summary>
    double ShadowCorrelationDistanceM { get; }

    /// <summary>Расстояние корреляции состояния прямой видимости, м</summary>
    double StateCorrelationDistanceM { get; }

    /// <summary>Потери между антеннами</summary>
    /// <param name="transmitter">Положение передающей антенны, м</param>
    /// <param name="receiver">Положение приёмной антенны, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    LinkLoss Loss(Vector3 transmitter, Vector3 receiver, double frequencyHz);
}

/// <summary>Свободное пространство с необязательным логнормальным затенением</summary>
public sealed class FreeSpaceModel : IPropagationModel
{
    /// <summary>СКО затенения, дБ</summary>
    public double ShadowSigmaDb { get; init; }

    /// <inheritdoc />
    public double ShadowCorrelationDistanceM { get; init; } = 50;

    /// <inheritdoc />
    public double StateCorrelationDistanceM => ShadowCorrelationDistanceM;

    /// <inheritdoc />
    public string Name => "Свободное пространство";

    /// <inheritdoc />
    public LinkLoss Loss(Vector3 transmitter, Vector3 receiver, double frequencyHz)
        => LinkLoss.Single(PathLoss.FreeSpaceDb(frequencyHz, PropagationGeometry.Distance(transmitter, receiver)), ShadowSigmaDb);
}

/// <summary>
/// Логарифмическая модель: L = L_св(d₀) + 10·n·lg(d/d₀) за опорным расстоянием d₀ и свободное пространство до него
/// </summary>
/// <remarks>
/// Показатель n — 2 в свободном пространстве, 2,7–3,5 в городе, 3–5 в застройке без прямой видимости. Модель
/// простая, но именно на ней выведены классические формулы покрытия, например формула Джейкса для доли площади
/// с уверенным приёмом.
/// </remarks>
public sealed class LogDistanceModel : IPropagationModel
{
    /// <summary>Создаёт модель</summary>
    /// <param name="exponent">Показатель спада n</param>
    /// <param name="shadowSigmaDb">СКО затенения, дБ</param>
    /// <param name="referenceDistanceM">Опорное расстояние d₀, м</param>
    public LogDistanceModel(double exponent, double shadowSigmaDb = 0, double referenceDistanceM = 1)
    {
        Guard.RequirePositive(exponent, nameof(exponent));
        Guard.RequirePositive(referenceDistanceM, nameof(referenceDistanceM));

        if (!(shadowSigmaDb >= 0) || double.IsInfinity(shadowSigmaDb))
            throw new ArgumentOutOfRangeException(nameof(shadowSigmaDb), shadowSigmaDb, "СКО затенения — конечное неотрицательное число");

        Exponent = exponent;
        ShadowSigmaDb = shadowSigmaDb;
        ReferenceDistanceM = referenceDistanceM;
    }

    /// <summary>Показатель спада n</summary>
    public double Exponent { get; }

    /// <summary>СКО затенения, дБ</summary>
    public double ShadowSigmaDb { get; }

    /// <summary>Опорное расстояние d₀, м</summary>
    public double ReferenceDistanceM { get; }

    /// <inheritdoc />
    public double ShadowCorrelationDistanceM { get; init; } = 50;

    /// <inheritdoc />
    public double StateCorrelationDistanceM => ShadowCorrelationDistanceM;

    /// <inheritdoc />
    public string Name => $"Логарифмическая, n = {Exponent:0.##}";

    /// <inheritdoc />
    public LinkLoss Loss(Vector3 transmitter, Vector3 receiver, double frequencyHz)
    {
        double d = PropagationGeometry.Distance(transmitter, receiver);
        double loss = d <= ReferenceDistanceM
            ? PathLoss.FreeSpaceDb(frequencyHz, d)
            : PathLoss.FreeSpaceDb(frequencyHz, ReferenceDistanceM) + (10 * Exponent * Math.Log10(d / ReferenceDistanceM));

        return LinkLoss.Single(loss, ShadowSigmaDb);
    }
}

/// <summary>
/// Окумура — Хата и COST-231 (<see cref="PathLoss.HataDb"/>) с логнормальным затенением; не меньше потерь
/// свободного пространства
/// </summary>
/// <remarks>
/// Высота базовой станции — Z передатчика, абонента — Z приёмника, расстояние — по горизонтали. Модель
/// построена на 1–20 км; ближе она продолжается формулой и может опуститься ниже свободного пространства, поэтому
/// снизу ограничена им.
/// </remarks>
public sealed class HataModel : IPropagationModel
{
    /// <summary>Создаёт модель</summary>
    /// <param name="environment">Тип местности</param>
    /// <param name="shadowSigmaDb">СКО затенения, дБ; в городе обычно 8</param>
    public HataModel(HataEnvironment environment = HataEnvironment.MediumCity, double shadowSigmaDb = 8)
    {
        if (!(shadowSigmaDb >= 0) || double.IsInfinity(shadowSigmaDb))
            throw new ArgumentOutOfRangeException(nameof(shadowSigmaDb), shadowSigmaDb, "СКО затенения — конечное неотрицательное число");

        Environment = environment;
        ShadowSigmaDb = shadowSigmaDb;
    }

    /// <summary>Тип местности</summary>
    public HataEnvironment Environment { get; }

    /// <summary>СКО затенения, дБ</summary>
    public double ShadowSigmaDb { get; }

    /// <inheritdoc />
    public double ShadowCorrelationDistanceM { get; init; } = 50;

    /// <inheritdoc />
    public double StateCorrelationDistanceM => ShadowCorrelationDistanceM;

    /// <inheritdoc />
    public string Name => $"Окумура — Хата, {Environment}";

    /// <inheritdoc />
    public LinkLoss Loss(Vector3 transmitter, Vector3 receiver, double frequencyHz)
    {
        double hata = PathLoss.HataDb(frequencyHz, PropagationGeometry.Horizontal(transmitter, receiver), transmitter.Z, receiver.Z, Environment);
        double free = PathLoss.FreeSpaceDb(frequencyHz, PropagationGeometry.Distance(transmitter, receiver));

        return LinkLoss.Single(Math.Max(hata, free), ShadowSigmaDb);
    }
}

/// <summary>Расстояния для моделей потерь: не меньше метра, чтобы точка у самой мачты не давала бесконечности</summary>
internal static class PropagationGeometry
{
    public const double MinimumDistanceM = 1;

    public static double Distance(Vector3 a, Vector3 b) => Math.Max(a.DistanceTo(b), MinimumDistanceM);

    public static double Horizontal(Vector3 a, Vector3 b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;

        return Math.Max(Math.Sqrt((dx * dx) + (dy * dy)), MinimumDistanceM);
    }
}
