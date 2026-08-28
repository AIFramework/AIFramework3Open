using AI.Microwave.Models;
using AI.Solvers.Math.Core.Numerics;
using AI.Solvers.Math.Core.Solvers;

namespace AI.Microwave.Geometry;

/// <summary>
/// Плосковыпуклая диэлектрическая линза с гиперболической преломляющей
/// поверхностью, обращённой к облучателю.
/// </summary>
/// <remarks>
/// Профиль задан в полярных координатах из фокуса:
/// r(theta) = (n-1) f / (n cos(theta) - 1). В вершине r = f, к краю радиус
/// растёт и на угле arccos(1/n) уходит в бесконечность.
/// <para>
/// Прежний расчёт брал толщину в центре как f (sqrt(eps) - 1)/sqrt(eps),
/// то есть привязывал её к фокусному расстоянию, а не к реальному диаметру:
/// линза диаметром 0.65 м получалась толщиной 0.53 м. Здесь толщина
/// выводится из геометрии профиля, а объём берётся интегрированием -
/// квадратурой и бисекцией из AI.Solvers.Math.
/// </para>
/// </remarks>
public sealed class DielectricLens
{
    private readonly double _n;
    private readonly double _thetaLimit;

    /// <summary>Материал линзы.</summary>
    public DielectricProperties Material { get; }

    /// <summary>Диаметр раскрыва, м.</summary>
    public double DiameterM { get; }

    /// <summary>Фокусное расстояние (от фокуса до вершины профиля), м.</summary>
    public double FocalLengthM { get; }

    /// <summary>Толщина на краю из условия прочности, м.</summary>
    public double EdgeThicknessM { get; }

    /// <summary>Половинный угол, под которым край линзы виден из фокуса, град.</summary>
    public double RimHalfAngleDeg { get; }

    /// <summary>Толщина в центре, м.</summary>
    public double CenterThicknessM { get; }

    /// <summary>Объём сплошной линзы, м^3.</summary>
    public double VolumeM3 { get; }

    /// <summary>Шаг зонирования lambda / (n - 1), м.</summary>
    public double ZoneStepM { get; }

    /// <summary>Число зон Френеля в сплошном профиле (1 - зонирование не нужно).</summary>
    public int ZoneCount { get; }

    /// <summary>Наибольшая толщина зонированной линзы, м.</summary>
    public double ZonedMaxThicknessM { get; }

    /// <summary>Объём зонированной линзы, м^3.</summary>
    public double ZonedVolumeM3 { get; }

    /// <summary>Площадь раскрыва, м^2.</summary>
    public double ApertureAreaM2 => Math.PI * DiameterM * DiameterM / 4.0;

    /// <summary>Средняя длина пути волны в диэлектрике, м: V / A.</summary>
    public double MeanPathM => VolumeM3 / ApertureAreaM2;

    /// <summary>Средняя длина пути в зонированной линзе, м.</summary>
    public double ZonedMeanPathM => ZonedVolumeM3 / ApertureAreaM2;

    /// <summary>Масса сплошной линзы, кг.</summary>
    public double WeightKg => VolumeM3 * Material.Density;

    /// <summary>Масса зонированной линзы, кг.</summary>
    public double ZonedWeightKg => ZonedVolumeM3 * Material.Density;

    /// <summary>Отношение толщины к диаметру: у разумной линзы заметно меньше 1/3.</summary>
    public double ThicknessToDiameter => CenterThicknessM / DiameterM;

    public DielectricLens(DielectricProperties material, double diameterM,
        double focalLengthM, double lambdaM, double edgeThicknessM)
    {
        Material = material;
        DiameterM = diameterM;
        FocalLengthM = focalLengthM;
        EdgeThicknessM = edgeThicknessM;

        _n = material.RefractiveIndex;
        _thetaLimit = Math.Acos(1.0 / _n) * 0.999999;

        double radius = diameterM / 2.0;
        double thetaMax = ThetaForRadius(radius);
        RimHalfAngleDeg = thetaMax * 180.0 / Math.PI;

        CenterThicknessM = AxialAt(thetaMax) - focalLengthM + edgeThicknessM;

        VolumeM3 = Quadrature.Integrate(
            rho => ThicknessAt(rho) * 2.0 * Math.PI * rho, 0.0, radius, 1e-9);

        // Зонирование: снятие ступени lambda/(n-1) не меняет фазу на выходе,
        // поэтому объём зонированной линзы получается точно, без повторного
        // интегрирования, - вычитанием цилиндров над границами зон.
        ZoneStepM = material.ZoneStepM(lambdaM);
        double sag = CenterThicknessM - edgeThicknessM;
        ZoneCount = Math.Max(1, (int)Math.Ceiling(sag / ZoneStepM));
        ZonedMaxThicknessM = edgeThicknessM + Math.Min(sag, ZoneStepM);

        double removed = 0.0;
        for (int k = 1; k < ZoneCount; k++)
        {
            double rhoK = RadiusForExcess(k * ZoneStepM, radius, sag);
            removed += ZoneStepM * Math.PI * rhoK * rhoK;
        }
        ZonedVolumeM3 = Math.Max(VolumeM3 - removed, 0.0);
    }

    /// <summary>Радиус профиля из фокуса под углом theta, м.</summary>
    private double SurfaceRadius(double theta) => (_n - 1.0) * FocalLengthM / (_n * Math.Cos(theta) - 1.0);

    /// <summary>Осевая координата точки профиля, м.</summary>
    private double AxialAt(double theta) => SurfaceRadius(theta) * Math.Cos(theta);

    /// <summary>Радиальная координата точки профиля, м.</summary>
    private double RadialAt(double theta) => SurfaceRadius(theta) * Math.Sin(theta);

    /// <summary>Угол, на котором профиль имеет заданный радиус.</summary>
    private double ThetaForRadius(double rho)
    {
        if (rho <= 0) return 0.0;
        var (_, root, _) = NumericalEquationSolver.Bisection(
            th => RadialAt(th) - rho, 0.0, _thetaLimit, 1e-12, 200);
        return root;
    }

    /// <summary>Толщина линзы на радиусе rho, м.</summary>
    public double ThicknessAt(double rho)
        => FocalLengthM + CenterThicknessM - AxialAt(ThetaForRadius(rho));

    /// <summary>Толщина зонированной линзы на радиусе rho, м.</summary>
    public double ZonedThicknessAt(double rho)
    {
        double excess = Math.Max(ThicknessAt(rho) - EdgeThicknessM, 0.0);
        return EdgeThicknessM + excess % ZoneStepM;
    }

    /// <summary>Радиус, на котором превышение над краевой толщиной равно заданному.</summary>
    private double RadiusForExcess(double excess, double maxRadius, double sag)
    {
        if (excess <= 0) return maxRadius;
        if (excess >= sag) return 0.0;
        var (_, root, _) = NumericalEquationSolver.Bisection(
            rho => ThicknessAt(rho) - EdgeThicknessM - excess, 0.0, maxRadius, 1e-12, 200);
        return root;
    }
}
