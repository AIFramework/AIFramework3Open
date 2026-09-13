using AI.Insights;

namespace AI.Microwave.Propagation;

/// <summary>Составляющие дифракции «дельта-Буллингтон» для двух поляризаций</summary>
/// <param name="Horizontal">Потери на дифракцию при горизонтальной поляризации, дБ</param>
/// <param name="Vertical">Потери на дифракцию при вертикальной поляризации, дБ</param>
/// <param name="BullingtonActual">Буллингтон по настоящему профилю, дБ</param>
/// <param name="BullingtonSmooth">Буллингтон по гладкому профилю с приведёнными высотами антенн, дБ</param>
/// <param name="SphericalHorizontal">Дифракция на сферической Земле, горизонтальная поляризация, дБ</param>
/// <param name="SphericalVertical">Дифракция на сферической Земле, вертикальная поляризация, дБ</param>
public readonly record struct DeltaBullingtonLoss(
    double Horizontal,
    double Vertical,
    double BullingtonActual,
    double BullingtonSmooth,
    double SphericalHorizontal,
    double SphericalVertical)
{
    /// <summary>Потери для поляризации, дБ</summary>
    public double For(Polarization polarization) => polarization == Polarization.Horizontal ? Horizontal : Vertical;
}

/// <summary>Медианные потери по рельефу ITU-R P.1812: свободное пространство и дифракция</summary>
/// <param name="FreeSpaceDb">Потери в свободном пространстве по формуле (8), дБ</param>
/// <param name="Diffraction">Составляющие дифракции</param>
/// <param name="Polarization">Поляризация</param>
/// <param name="IsLineOfSight">Прямая видимость над рельефом с помехами</param>
/// <param name="SmoothTxHeightM">Высота гладкой поверхности у передатчика для дифракции h_std, м</param>
/// <param name="SmoothRxHeightM">Высота гладкой поверхности у приёмника для дифракции h_srd, м</param>
/// <param name="TerminalClutterDb">Поправка на помехи у антенн по ITU-R P.2108, раздел 3.1, дБ</param>
public sealed record P1812Loss(
    double FreeSpaceDb,
    DeltaBullingtonLoss Diffraction,
    Polarization Polarization,
    bool IsLineOfSight,
    double SmoothTxHeightM,
    double SmoothRxHeightM,
    double TerminalClutterDb = 0) : IInterpretable
{
    /// <summary>Потери на дифракцию для выбранной поляризации, дБ</summary>
    public double DiffractionDb => Diffraction.For(Polarization);

    /// <summary>
    /// Основные потери передачи для 50 % времени и 50 % мест: L_bd50 и поправка на помехи у антенн, дБ
    /// </summary>
    public double TotalDb => FreeSpaceDb + DiffractionDb + TerminalClutterDb;

    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Потери по рельефу, ITU-R P.1812")
            .Summary($"{(IsLineOfSight ? "Трасса в прямой видимости" : "Трасса закрыта рельефом")}: свободное пространство {Fmt.Num(FreeSpaceDb, 1)} дБ "
                + $"плюс дифракция {Fmt.Num(DiffractionDb, 1)} дБ, всего {Fmt.Num(TotalDb, 1)} дБ.")
            .Metric("Свободное пространство", Fmt.Num(FreeSpaceDb, 2), "дБ", "формула (8)")
            .Metric("Буллингтон по профилю", Fmt.Num(Diffraction.BullingtonActual, 2), "дБ", "препятствия рельефа и помех")
            .Metric("Сферическая Земля", Fmt.Num(Polarization == Polarization.Horizontal ? Diffraction.SphericalHorizontal : Diffraction.SphericalVertical, 2), "дБ", "выпуклость Земли")
            .Metric("Дифракция", Fmt.Num(DiffractionDb, 2), "дБ", "дельта-Буллингтон, формула (39)")
            .Metric("Помехи у антенн", Fmt.Num(TerminalClutterDb, 2), "дБ", "ITU-R P.2108, раздел 3.1")
            .FindingIf(DiffractionDb > 20,
                "Потери на дифракцию больше 20 дБ: приём держится на огибании препятствий, и небольшое смещение абонента "
                + "или антенны может изменить уровень на единицы децибел.")
            .Warning("Медиана по дифракционному пути: тропосферное рассеяние и волноводное распространение (разделы 4.4–4.5) "
                + "не учитываются — они важны на трассах в сотни километров и малых процентах времени.")
            .Build();
}

/// <summary>
/// Потери на трассе над рельефом по ITU-R P.1812-8 для 50 % времени и 50 % мест: свободное пространство и
/// дифракция «дельта-Буллингтон».
/// </summary>
/// <remarks>
/// <para>
/// Дифракция — раздел 4.3. Буллингтон заменяет все препятствия профиля одним эквивалентным краем в точке
/// пересечения самых крутых лучей от антенн и считает его потери приближением P.526 J(ν)
/// (<see cref="KnifeEdgeDiffraction.ItuApproximationLossDb"/>) с поправкой (1 − e^(−J/6))·(10 + 0,02·d). Дифракция на
/// гладкой сферической Земле — первым членом ряда вычетов с электрическими свойствами суши (ε = 22, σ = 0,003 См/м)
/// и моря (80, 5). «Дельта-Буллингтон» складывает их так, чтобы гладкая Земля не считалась дважды:
/// L_d = L_bulla + max(L_dsph − L_bulls, 0), где L_bulls — Буллингтон для той же трассы без рельефа.
/// </para>
/// <para>
/// Помехи — застройка и лес — прибавляются к высотам профиля во всех точках, кроме концов; гладкая поверхность
/// строится по одному рельефу (приложение 1, раздел 5.6). Эффективный радиус Земли — 6371·157/(157 − ΔN) км,
/// ΔN = 45 N-единиц/км по умолчанию дают k = 1,40.
/// </para>
/// <para>
/// Реализация сверена с эталонной программой исследовательской комиссии 3 МСЭ-R (Py1812) по её проверочным
/// трассам. Итог — L_bd50: тропосферное рассеяние и волноводное распространение, которые P.1812 смешивает с
/// дифракцией, здесь не считаются; на проверочных трассах при 50 % времени это меняет потери меньше чем на 0,02 дБ.
/// </para>
/// </remarks>
public static class ItuP1812
{
    /// <summary>Радиус Земли, км</summary>
    public const double EarthRadiusKm = 6371;

    /// <summary>Эффективный радиус для β₀ % времени, км: k = 3</summary>
    public const double BetaEffectiveEarthRadiusKm = EarthRadiusKm * 3;

    // Скорость света в формулах P.1812 (как в P.2001): λ = 0,2998/f, м при f в ГГц
    private const double LightSpeedGmPerS = 0.2998;

    /// <summary>Медианный эффективный радиус Земли, км: 6371·157/(157 − ΔN)</summary>
    /// <param name="refractivityGradient">Средний градиент рефракции ΔN в нижнем километре, N-единиц/км</param>
    public static double MedianEffectiveEarthRadiusKm(double refractivityGradient = 45)
    {
        if (!(refractivityGradient < 157) || double.IsNaN(refractivityGradient))
            throw new ArgumentOutOfRangeException(nameof(refractivityGradient), refractivityGradient, "ΔN должен быть меньше 157 N-единиц/км");

        return EarthRadiusKm * 157 / (157 - refractivityGradient);
    }

    /// <summary>
    /// Разброс по местам σ_L, дБ: (0,52 + 0,024·f)·w^0,28, f в ГГц, w — размер области прогноза в метрах
    /// </summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="resolutionM">Сторона области, для которой делается прогноз, м</param>
    public static double LocationVariabilityDb(double frequencyHz, double resolutionM = 100)
    {
        Guard.RequirePositive(frequencyHz, nameof(frequencyHz));
        Guard.RequirePositive(resolutionM, nameof(resolutionM));

        return (0.52 + (0.024 * frequencyHz / 1e9)) * Math.Pow(resolutionM, 0.28);
    }

    /// <summary>Потери в свободном пространстве, формула (8): 92,4 + 20·lg f + 10·lg(d² + ((h_ts − h_rs)/1000)²), дБ</summary>
    /// <param name="distanceKm">Длина трассы, км</param>
    /// <param name="txHeightAmslM">Высота передающей антенны над уровнем моря, м</param>
    /// <param name="rxHeightAmslM">Высота приёмной антенны над уровнем моря, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double FreeSpaceDb(double distanceKm, double txHeightAmslM, double rxHeightAmslM, double frequencyHz)
    {
        double height = (txHeightAmslM - rxHeightAmslM) / 1000;

        return 92.4 + (20 * Math.Log10(frequencyHz / 1e9)) + (10 * Math.Log10((distanceKm * distanceKm) + (height * height)));
    }

    /// <summary>Потери Буллингтона, раздел 4.3.1, дБ</summary>
    /// <param name="distancesKm">Расстояния точек профиля от передатчика, км</param>
    /// <param name="heightsM">Высоты точек профиля над уровнем моря с помехами, м</param>
    /// <param name="txHeightAmslM">Высота передающей антенны над уровнем моря, м</param>
    /// <param name="rxHeightAmslM">Высота приёмной антенны над уровнем моря, м</param>
    /// <param name="effectiveRadiusKm">Эффективный радиус Земли, км</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    public static double BullingtonDb(IReadOnlyList<double> distancesKm, IReadOnlyList<double> heightsM, double txHeightAmslM, double rxHeightAmslM, double effectiveRadiusKm, double frequencyHz)
        => Bullington(distancesKm, heightsM, txHeightAmslM, rxHeightAmslM, effectiveRadiusKm, frequencyHz, out _);

    /// <summary>
    /// Дифракция на гладкой сферической Земле, раздел 4.3.2, для горизонтальной и вертикальной поляризации, дБ
    /// </summary>
    /// <param name="distanceKm">Длина трассы, км</param>
    /// <param name="txEffectiveHeightM">Эффективная высота передающей антенны над гладкой поверхностью, м</param>
    /// <param name="rxEffectiveHeightM">Эффективная высота приёмной антенны, м</param>
    /// <param name="effectiveRadiusKm">Эффективный радиус Земли, км</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="seaFraction">Доля трассы над морем ω</param>
    public static (double Horizontal, double Vertical) SphericalEarthDb(double distanceKm, double txEffectiveHeightM, double rxEffectiveHeightM, double effectiveRadiusKm, double frequencyHz, double seaFraction = 0)
    {
        double f = frequencyHz / 1e9, d = distanceKm, hte = txEffectiveHeightM, hre = rxEffectiveHeightM, ap = effectiveRadiusKm;
        double lambda = LightSpeedGmPerS / f;

        // Граница прямой видимости над гладкой Землёй, формула (22)
        double lineOfSight = Math.Sqrt(2 * ap) * (Math.Sqrt(0.001 * hte) + Math.Sqrt(0.001 * hre));

        if (d >= lineOfSight)
            return FirstTermDb(d, hte, hre, ap, f, seaFraction);

        // Наименьший просвет между лучом и выпуклой Землёй, формулы (23)–(24)
        double c = (hte - hre) / (hte + hre);
        double m = 250 * d * d / (ap * (hte + hre));
        double b = 2 * Math.Sqrt((m + 1) / (3 * m)) * Math.Cos((Math.PI / 3) + (Math.Acos(1.5 * c * Math.Sqrt(3 * m / Math.Pow(m + 1, 3))) / 3));
        double d1 = d / 2 * (1 + b), d2 = d - d1;
        double clearance = (((hte - (500 * d1 * d1 / ap)) * d2) + ((hre - (500 * d2 * d2 / ap)) * d1)) / d;

        // Просвет, при котором потерь нет, формула (25)
        double required = 17.456 * Math.Sqrt(d1 * d2 * lambda / d);

        if (clearance > required)
            return (0, 0);

        // Радиус, дающий касание луча, формулы (26)–(27)
        double marginal = 500 * Math.Pow(d / (Math.Sqrt(hte) + Math.Sqrt(hre)), 2);
        (double h, double v) = FirstTermDb(d, hte, hre, marginal, f, seaFraction);
        double scale = 1 - (clearance / required);

        return (scale * Math.Max(h, 0), scale * Math.Max(v, 0));
    }

    /// <summary>Сглаженные высоты поверхности для модели дифракции, приложение 1, раздел 5.6</summary>
    /// <param name="distancesKm">Расстояния точек профиля, км</param>
    /// <param name="groundHeightsM">Высоты земли без помех, м</param>
    /// <param name="txHeightAmslM">Высота передающей антенны над уровнем моря, м</param>
    /// <param name="rxHeightAmslM">Высота приёмной антенны над уровнем моря, м</param>
    /// <returns>
    /// Линия наименьших квадратов у концов h_st и h_sr (формулы (85)–(88)) и высоты для дифракции h_std и h_srd
    /// (формулы (89)–(91))
    /// </returns>
    public static (double Hst, double Hsr, double Hstd, double Hsrd) SmoothEarthHeights(IReadOnlyList<double> distancesKm, IReadOnlyList<double> groundHeightsM, double txHeightAmslM, double rxHeightAmslM)
    {
        IReadOnlyList<double> d = distancesKm, h = groundHeightsM;
        int n = d.Count;
        double total = d[n - 1];
        double v1 = 0, v2 = 0;

        for (int i = 1; i < n; i++)
        {
            double step = d[i] - d[i - 1];
            v1 += step * (h[i] + h[i - 1]);
            v2 += step * ((h[i] * ((2 * d[i]) + d[i - 1])) + (h[i - 1] * (d[i] + (2 * d[i - 1]))));
        }

        double hst = ((2 * v1 * total) - v2) / (total * total);
        double hsr = (v2 - (v1 * total)) / (total * total);

        // Самое высокое препятствие над линией между антеннами и наклоны на него
        double obstacle = double.NegativeInfinity, alphaTx = double.NegativeInfinity, alphaRx = double.NegativeInfinity;

        for (int i = 1; i < n - 1; i++)
        {
            double above = h[i] - (((txHeightAmslM * (total - d[i])) + (rxHeightAmslM * d[i])) / total);
            obstacle = Math.Max(obstacle, above);
            alphaTx = Math.Max(alphaTx, above / d[i]);
            alphaRx = Math.Max(alphaRx, above / (total - d[i]));
        }

        double hstp = hst, hsrp = hsr;

        if (obstacle > 0)
        {
            hstp = hst - (obstacle * alphaTx / (alphaTx + alphaRx));
            hsrp = hsr - (obstacle * alphaRx / (alphaTx + alphaRx));
        }

        double hstd = hstp >= h[0] ? h[0] : hstp;
        double hsrd = hsrp > h[n - 1] ? h[n - 1] : hsrp;

        return (hst, hsr, hstd, hsrd);
    }

    /// <summary>Дифракция «дельта-Буллингтон», раздел 4.3.4</summary>
    /// <param name="distancesKm">Расстояния точек профиля, км</param>
    /// <param name="heightsM">Высоты точек профиля с помехами, м</param>
    /// <param name="txHeightAmslM">Высота передающей антенны над уровнем моря, м</param>
    /// <param name="rxHeightAmslM">Высота приёмной антенны над уровнем моря, м</param>
    /// <param name="smoothTxHeightM">Высота гладкой поверхности у передатчика h_std, м</param>
    /// <param name="smoothRxHeightM">Высота гладкой поверхности у приёмника h_srd, м</param>
    /// <param name="effectiveRadiusKm">Эффективный радиус Земли, км</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="seaFraction">Доля трассы над морем</param>
    public static DeltaBullingtonLoss DeltaBullington(
        IReadOnlyList<double> distancesKm,
        IReadOnlyList<double> heightsM,
        double txHeightAmslM,
        double rxHeightAmslM,
        double smoothTxHeightM,
        double smoothRxHeightM,
        double effectiveRadiusKm,
        double frequencyHz,
        double seaFraction = 0)
    {
        double actual = BullingtonDb(distancesKm, heightsM, txHeightAmslM, rxHeightAmslM, effectiveRadiusKm, frequencyHz);

        // Та же трасса без рельефа, антенны над гладкой поверхностью, формулы (37)–(38)
        double hte = txHeightAmslM - smoothTxHeightM, hre = rxHeightAmslM - smoothRxHeightM;
        double smooth = BullingtonDb(distancesKm, new double[distancesKm.Count], hte, hre, effectiveRadiusKm, frequencyHz);
        double total = distancesKm[^1] - distancesKm[0];
        (double sphericalH, double sphericalV) = SphericalEarthDb(total, hte, hre, effectiveRadiusKm, frequencyHz, seaFraction);

        return new DeltaBullingtonLoss(
            actual + Math.Max(sphericalH - smooth, 0),
            actual + Math.Max(sphericalV - smooth, 0),
            actual,
            smooth,
            sphericalH,
            sphericalV);
    }

    /// <summary>Медианные потери по профилю: свободное пространство и дифракция для 50 % времени и мест</summary>
    /// <param name="profile">Профиль трассы</param>
    /// <param name="txHeightAglM">Высота передающей антенны над землёй, м</param>
    /// <param name="rxHeightAglM">Высота приёмной антенны над землёй, м</param>
    /// <param name="frequencyHz">Частота, Гц; модель определена на 30 МГц — 6 ГГц</param>
    /// <param name="polarization">Поляризация</param>
    /// <param name="refractivityGradient">ΔN, N-единиц/км</param>
    /// <param name="effectiveRadiusKm">Эффективный радиус Земли, км; null — медианный по ΔN</param>
    public static P1812Loss MedianLoss(
        TerrainProfile profile,
        double txHeightAglM,
        double rxHeightAglM,
        double frequencyHz,
        Polarization polarization = Polarization.Vertical,
        double refractivityGradient = 45,
        double? effectiveRadiusKm = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Guard.RequirePositive(frequencyHz, nameof(frequencyHz));

        int n = profile.Count;
        var d = new double[n];
        var g = new double[n];

        for (int i = 0; i < n; i++)
        {
            d[i] = profile.DistancesM[i] / 1000;
            g[i] = profile.GroundHeightsM[i] + (i == 0 || i == n - 1 ? 0 : profile.ClutterHeightsM[i]);
        }

        double hts = profile.GroundHeightsM[0] + txHeightAglM;
        double hrs = profile.GroundHeightsM[n - 1] + rxHeightAglM;
        double radius = effectiveRadiusKm ?? MedianEffectiveEarthRadiusKm(refractivityGradient);

        (_, _, double hstd, double hsrd) = SmoothEarthHeights(d, profile.GroundHeightsM, hts, hrs);
        DeltaBullingtonLoss diffraction = DeltaBullington(d, g, hts, hrs, hstd, hsrd, radius, frequencyHz, profile.SeaFraction);
        Bullington(d, g, hts, hrs, radius, frequencyHz, out bool lineOfSight);

        return new P1812Loss(FreeSpaceDb(d[n - 1], hts, hrs, frequencyHz), diffraction, polarization, lineOfSight, hstd, hsrd);
    }

    private static double Bullington(IReadOnlyList<double> d, IReadOnlyList<double> g, double hts, double hrs, double ap, double frequencyHz, out bool lineOfSight)
    {
        double curvature = 1 / ap;
        double lambda = LightSpeedGmPerS / (frequencyHz / 1e9);
        int n = d.Count;
        double total = d[n - 1] - d[0];

        // Наибольший наклон луча от передатчика на точку профиля, формула (13), и прямой луч, формула (14)
        double slopeTx = double.NegativeInfinity;

        for (int i = 1; i < n - 1; i++)
            slopeTx = Math.Max(slopeTx, (g[i] + (500 * curvature * d[i] * (total - d[i])) - hts) / d[i]);

        double slopeDirect = (hrs - hts) / total;
        double nu;

        if (slopeTx < slopeDirect)
        {
            // Прямая видимость: наибольший параметр дифракции, формула (15)
            lineOfSight = true;
            nu = double.NegativeInfinity;

            for (int i = 1; i < n - 1; i++)
            {
                double clearance = g[i] + (500 * curvature * d[i] * (total - d[i])) - (((hts * (total - d[i])) + (hrs * d[i])) / total);
                nu = Math.Max(nu, clearance * Math.Sqrt(0.002 * total / (lambda * d[i] * (total - d[i]))));
            }
        }
        else
        {
            // Загоризонтная трасса: точка Буллингтона на пересечении самых крутых лучей, формулы (17)–(20)
            lineOfSight = false;
            double slopeRx = double.NegativeInfinity;

            for (int i = 1; i < n - 1; i++)
                slopeRx = Math.Max(slopeRx, (g[i] + (500 * curvature * d[i] * (total - d[i])) - hrs) / (total - d[i]));

            double point = (hrs - hts + (slopeRx * total)) / (slopeTx + slopeRx);
            nu = (hts + (slopeTx * point) - (((hts * (total - point)) + (hrs * point)) / total))
                * Math.Sqrt(0.002 * total / (lambda * point * (total - point)));
        }

        double edge = KnifeEdgeDiffraction.ItuApproximationLossDb(nu);

        // Поправка на протяжённость трассы, формула (21)
        return edge + ((1 - Math.Exp(-edge / 6)) * (10 + (0.02 * total)));
    }

    // Первый член ряда вычетов для сферической Земли, раздел 4.3.3, формула (28): суша и море смешиваются по ω
    private static (double Horizontal, double Vertical) FirstTermDb(double d, double hte, double hre, double adft, double f, double seaFraction)
    {
        (double landH, double landV) = FirstTermInner(22, 0.003, d, hte, hre, adft, f);
        (double seaH, double seaV) = FirstTermInner(80, 5, d, hte, hre, adft, f);

        return ((seaFraction * seaH) + ((1 - seaFraction) * landH), (seaFraction * seaV) + ((1 - seaFraction) * landV));
    }

    // Формулы (29)–(36) для одной подстилающей поверхности
    private static (double Horizontal, double Vertical) FirstTermInner(double permittivity, double conductivity, double d, double hte, double hre, double adft, double f)
    {
        double horizontal = 0.036 * Math.Pow(adft * f, -1.0 / 3) * Math.Pow(Math.Pow(permittivity - 1, 2) + Math.Pow(18 * conductivity / f, 2), -0.25);
        double vertical = horizontal * Math.Sqrt((permittivity * permittivity) + Math.Pow(18 * conductivity / f, 2));

        double Loss(double k)
        {
            double beta = (1 + (1.6 * k * k) + (0.67 * Math.Pow(k, 4))) / (1 + (4.5 * k * k) + (1.53 * Math.Pow(k, 4)));
            double x = 21.88 * beta * Math.Pow(f / (adft * adft), 1.0 / 3) * d;
            double yScale = 0.9575 * beta * Math.Pow(f * f / adft, 1.0 / 3);
            double distanceTerm = x >= 1.6
                ? 11 + (10 * Math.Log10(x)) - (17.6 * x)
                : (-20 * Math.Log10(x)) - (5.6488 * Math.Pow(x, 1.425));

            double HeightGain(double height)
            {
                double b = beta * yScale * height;
                double gain = b > 2
                    ? (17.6 * Math.Sqrt(b - 1.1)) - (5 * Math.Log10(b - 1.1)) - 8
                    : 20 * Math.Log10(b + (0.1 * b * b * b));

                return Math.Max(gain, 2 + (20 * Math.Log10(k)));
            }

            return -distanceTerm - HeightGain(hte) - HeightGain(hre);
        }

        return (Loss(horizontal), Loss(vertical));
    }
}

/// <summary>
/// Потери по рельефу для карт покрытия: профиль между антеннами снимается с рельефа, потери — медиана
/// ITU-R P.1812 (<see cref="ItuP1812.MedianLoss"/>), разброс по местам — σ_L.
/// </summary>
public sealed class TerrainPropagationModel : IPropagationModel
{
    /// <summary>Создаёт модель</summary>
    /// <param name="terrain">Рельеф</param>
    public TerrainPropagationModel(ITerrainModel terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);

        Terrain = terrain;
    }

    /// <summary>Рельеф</summary>
    public ITerrainModel Terrain { get; }

    /// <summary>Наибольший шаг профиля, м; разумно брать не крупнее шага рельефа</summary>
    public double ProfileStepM { get; init; } = 30;

    /// <summary>Поляризация</summary>
    public Polarization Polarization { get; init; } = Polarization.Vertical;

    /// <summary>Градиент рефракции ΔN, N-единиц/км</summary>
    public double RefractivityGradient { get; init; } = 45;

    /// <summary>Разброс по местам σ_L, дБ; null — формула P.1812 для области 100 м</summary>
    public double? LocationSigmaDb { get; init; }

    /// <summary>
    /// Прибавлять поправку на помехи у обеих антенн по ITU-R P.2108, раздел 3.1: класс и высота помех берутся
    /// из рельефа в точке антенны. Определена на 30 МГц — 3 ГГц
    /// </summary>
    public bool TerminalClutter { get; init; }

    /// <summary>Ширина улицы для поправки на помехи у антенн, м</summary>
    public double StreetWidthM { get; init; } = ItuP2108.DefaultStreetWidthM;

    /// <inheritdoc />
    public double ShadowCorrelationDistanceM { get; init; } = 50;

    /// <inheritdoc />
    public double StateCorrelationDistanceM => ShadowCorrelationDistanceM;

    /// <inheritdoc />
    public string Name => "ITU-R P.1812 по рельефу";

    /// <summary>Медианные потери по рельефу между антеннами с составляющими</summary>
    /// <param name="transmitter">Передающая антенна: X, Y и высота над землёй, м</param>
    /// <param name="receiver">Приёмная антенна: X, Y и высота над землёй, м</param>
    /// <param name="frequencyHz">Частота, Гц</param>
    public P1812Loss Analyze(AI.Geometry.Primitives.Vector3 transmitter, AI.Geometry.Primitives.Vector3 receiver, double frequencyHz)
    {
        TerrainProfile profile = TerrainProfile.Between(Terrain, transmitter.X, transmitter.Y, receiver.X, receiver.Y, ProfileStepM);
        P1812Loss loss = ItuP1812.MedianLoss(profile, transmitter.Z, receiver.Z, frequencyHz, Polarization, RefractivityGradient);

        return TerminalClutter
            ? loss with { TerminalClutterDb = Terminal(transmitter, frequencyHz) + Terminal(receiver, frequencyHz) }
            : loss;
    }

    // Поправка у одной антенны: высота помех из рельефа, если задана, иначе по классу
    private double Terminal(AI.Geometry.Primitives.Vector3 antenna, double frequencyHz)
    {
        double clutterHeight = Terrain.ClutterHeightAt(antenna.X, antenna.Y);

        return ItuP2108.HeightGainCorrectionDb(
            frequencyHz,
            antenna.Z,
            Terrain.ClutterAt(antenna.X, antenna.Y),
            clutterHeight > 0 ? clutterHeight : null,
            StreetWidthM);
    }

    /// <inheritdoc />
    public LinkLoss Loss(AI.Geometry.Primitives.Vector3 transmitter, AI.Geometry.Primitives.Vector3 receiver, double frequencyHz)
        => LinkLoss.Single(Analyze(transmitter, receiver, frequencyHz).TotalDb, LocationSigmaDb ?? ItuP1812.LocationVariabilityDb(frequencyHz));
}
