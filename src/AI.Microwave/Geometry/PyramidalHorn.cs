using AI.Microwave.Models;

namespace AI.Microwave.Geometry;

/// <summary>
/// Геометрия пирамидального рупора: синтез апертуры под заданную ширину
/// луча, оптимальная осевая длина, фазовые ошибки и площадь стенок.
/// </summary>
/// <remarks>
/// Используется трижды: как самостоятельная антенна, как облучатель
/// параболы и как облучатель линзы. Раньше каждый из трёх случаев считал
/// свою геометрию по своей формуле, и ни одна из них не давала настоящую
/// осевую длину: величина (A^2 - a^2)/(2 lambda) - это расстояние до
/// вершины клина, а не длина рупора.
/// </remarks>
public sealed class PyramidalHorn
{
    /// <summary>theta_E = 56 lambda / b1 - ширина луча оптимального рупора в E-плоскости.</summary>
    public const double BeamFactorE = 56.0;

    /// <summary>theta_H = 67 lambda / a1 - то же в H-плоскости (косинусное распределение).</summary>
    public const double BeamFactorH = 67.0;

    /// <summary>УБЛ равномерного распределения по E-плоскости, дБ.</summary>
    public const double SidelobeEPlaneDb = -13.3;

    /// <summary>УБЛ косинусного распределения по H-плоскости, дБ.</summary>
    public const double SidelobeHPlaneDb = -23.0;

    /// <summary>КИП оптимального пирамидального рупора.</summary>
    public const double OptimalApertureEfficiency = 0.51;

    /// <summary>Предельная крутизна раскрыва, град (нижняя граница длины).</summary>
    private const double MaxFlareHalfAngleDeg = 30.0;

    /// <summary>Апертура в E-плоскости b1, м.</summary>
    public double ApertureHeightM { get; }

    /// <summary>Апертура в H-плоскости a1, м.</summary>
    public double ApertureWidthM { get; }

    /// <summary>Осевая длина от фланца волновода до апертуры, м.</summary>
    public double AxialLengthM { get; }

    /// <summary>Образующая широкой стенки (наклон в E-плоскости), м.</summary>
    public double SlantEPlaneM { get; }

    /// <summary>Образующая узкой стенки (наклон в H-плоскости), м.</summary>
    public double SlantHPlaneM { get; }

    /// <summary>Половинный угол раскрыва в E-плоскости, град.</summary>
    public double FlareAngleEDeg { get; }

    /// <summary>Половинный угол раскрыва в H-плоскости, град.</summary>
    public double FlareAngleHDeg { get; }

    /// <summary>Квадратичная фазовая ошибка s в E-плоскости (у оптимального рупора 1/4).</summary>
    public double PhaseErrorE { get; }

    /// <summary>Квадратичная фазовая ошибка t в H-плоскости (у оптимального рупора 3/8).</summary>
    public double PhaseErrorH { get; }

    /// <summary>Площадь четырёх стенок, м^2: по ней считаются и масса, и теплосъём.</summary>
    public double WallAreaM2 { get; }

    /// <summary>Площадь раскрыва, м^2.</summary>
    public double ApertureAreaM2 => ApertureWidthM * ApertureHeightM;

    /// <summary>Апертура упёрлась в размер волновода: заданная ШДН недостижимо широка.</summary>
    public bool ApertureClampedToThroat { get; }

    /// <summary>
    /// Строит рупор по готовым размерам апертуры.
    /// </summary>
    public PyramidalHorn(double apertureHeightM, double apertureWidthM,
        RectangularWaveguide throat, double lambdaM)
    {
        double b = throat.HeightM, a = throat.WidthM;

        ApertureClampedToThroat = apertureHeightM < b || apertureWidthM < a;
        ApertureHeightM = Math.Max(apertureHeightM, b);
        ApertureWidthM = Math.Max(apertureWidthM, a);

        // Оптимальный рупор: rho_E = b1^2/(2 lambda), rho_H = a1^2/(3 lambda).
        // Обе плоскости обязаны иметь общую осевую длину, поэтому берётся
        // большая: вторая плоскость получается длиннее оптимума, то есть с
        // меньшей фазовой ошибкой, что допустимо.
        double lengthE = FlareLength(ApertureHeightM * ApertureHeightM / (2.0 * lambdaM), ApertureHeightM, b);
        double lengthH = FlareLength(ApertureWidthM * ApertureWidthM / (3.0 * lambdaM), ApertureWidthM, a);
        AxialLengthM = Math.Max(lengthE, lengthH);

        double riseE = (ApertureHeightM - b) / 2.0;
        double riseH = (ApertureWidthM - a) / 2.0;
        SlantEPlaneM = Math.Sqrt(AxialLengthM * AxialLengthM + riseE * riseE);
        SlantHPlaneM = Math.Sqrt(AxialLengthM * AxialLengthM + riseH * riseH);
        FlareAngleEDeg = Math.Atan2(riseE, AxialLengthM) * 180.0 / Math.PI;
        FlareAngleHDeg = Math.Atan2(riseH, AxialLengthM) * 180.0 / Math.PI;

        PhaseErrorE = PhaseError(AxialLengthM, ApertureHeightM, b, lambdaM);
        PhaseErrorH = PhaseError(AxialLengthM, ApertureWidthM, a, lambdaM);

        // Четыре трапеции: широкие стенки шириной от a до a1 идут по
        // E-образующей, узкие высотой от b до b1 - по H-образующей.
        WallAreaM2 = (a + ApertureWidthM) * SlantEPlaneM
                   + (b + ApertureHeightM) * SlantHPlaneM;
    }

    /// <summary>
    /// Синтез рупора под требуемую ширину луча. E- и H-апертуры получаются
    /// разными: коэффициенты 56 и 67 не равны, и именно поэтому прежний
    /// расчёт, разводивший синтез (51) и проверку (56 и 67), не мог
    /// выполнить собственное требование ни при каких входных данных.
    /// </summary>
    public static PyramidalHorn ForBeamwidth(double beamwidthDeg,
        RectangularWaveguide throat, double lambdaM)
        => new(BeamFactorE * lambdaM / beamwidthDeg,
               BeamFactorH * lambdaM / beamwidthDeg,
               throat, lambdaM);

    /// <summary>Ширина луча в E-плоскости, град.</summary>
    public double BeamwidthEPlaneDeg(double lambdaM) => BeamFactorE * lambdaM / ApertureHeightM;

    /// <summary>Ширина луча в H-плоскости, град.</summary>
    public double BeamwidthHPlaneDeg(double lambdaM) => BeamFactorH * lambdaM / ApertureWidthM;

    /// <summary>Средняя ширина луча по двум плоскостям, град: облучатель круглой апертуры.</summary>
    public double MeanBeamwidthDeg(double lambdaM)
        => 0.5 * (BeamwidthEPlaneDeg(lambdaM) + BeamwidthHPlaneDeg(lambdaM));

    /// <summary>Масса стенок, кг.</summary>
    public double WeightKg(MaterialProperties material, double wallThicknessM)
        => WallAreaM2 * wallThicknessM * material.Density;

    /// <summary>
    /// Осевая длина раскрыва, при которой вершина клина отстоит от центра
    /// апертуры на <paramref name="apexDistance"/>. Снизу ограничена
    /// раскрывом в 30 градусов: у широколучевых облучателей оптимальное
    /// соотношение вырождается (rho меньше половины апертуры), и рупор
    /// превращается в открытый конец волновода.
    /// </summary>
    private static double FlareLength(double apexDistance, double aperture, double throat)
    {
        double onAxisSquared = apexDistance * apexDistance - aperture * aperture / 4.0;
        double optimal = onAxisSquared > 0
            ? Math.Sqrt(onAxisSquared) * (aperture - throat) / aperture
            : 0.0;
        double minimal = (aperture - throat)
                       / (2.0 * Math.Tan(MaxFlareHalfAngleDeg * Math.PI / 180.0));
        return Math.Max(optimal, minimal);
    }

    /// <summary>
    /// Квадратичная фазовая ошибка на краю апертуры в долях длины волны:
    /// s = A^2 / (8 lambda rho).
    /// </summary>
    private static double PhaseError(double axialLength, double aperture, double throat, double lambdaM)
    {
        if (aperture <= throat) return 0.0;
        double apexAxial = axialLength * aperture / (aperture - throat);
        double apexDistance = Math.Sqrt(apexAxial * apexAxial + aperture * aperture / 4.0);
        return aperture * aperture / (8.0 * lambdaM * apexDistance);
    }
}
