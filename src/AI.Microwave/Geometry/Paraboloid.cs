namespace AI.Microwave.Geometry;

/// <summary>
/// Параболоид вращения z = r^2 / (4f): зеркало осесимметричной антенны.
/// </summary>
public sealed class Paraboloid
{
    /// <summary>Диаметр раскрыва, м.</summary>
    public double DiameterM { get; }

    /// <summary>Фокусное расстояние, м.</summary>
    public double FocalLengthM { get; }

    public Paraboloid(double diameterM, double focalLengthM)
    {
        DiameterM = diameterM;
        FocalLengthM = focalLengthM;
    }

    /// <summary>Отношение f/D.</summary>
    public double FocalToDiameterRatio => FocalLengthM / DiameterM;

    /// <summary>Глубина зеркала, м: h = D^2 / (16 f).</summary>
    public double DepthM => DiameterM * DiameterM / (16.0 * FocalLengthM);

    /// <summary>Площадь раскрыва, м^2.</summary>
    public double ApertureAreaM2 => Math.PI * DiameterM * DiameterM / 4.0;

    /// <summary>
    /// Половинный угол, под которым край зеркала виден из фокуса, град:
    /// theta_0 = 2 arctg(D / 4f). При f/D = 0.4 это 64 градуса.
    /// </summary>
    /// <remarks>
    /// Прежний расчёт брал arctg(D/4f) без множителя два, то есть ровно
    /// половину нужного угла, и от этой половины назначал ДН облучателя.
    /// </remarks>
    public double RimHalfAngleDeg
        => 2.0 * Math.Atan(DiameterM / (4.0 * FocalLengthM)) * 180.0 / Math.PI;

    /// <summary>
    /// Площадь отражающей поверхности, м^2:
    /// S = (8 pi f^2 / 3) ((1 + D^2/(16 f^2))^{3/2} - 1).
    /// </summary>
    /// <remarks>
    /// Прежняя запись pi D sqrt(R^2 + h^2) - это удвоенная боковая
    /// поверхность конуса, вдвое больше истинной площади параболоида.
    /// А поскольку по ней считалась масса, ошибка шла прямо в стоимость.
    /// </remarks>
    public double SurfaceAreaM2
    {
        get
        {
            double k = DiameterM * DiameterM / (16.0 * FocalLengthM * FocalLengthM);
            return 8.0 * Math.PI * FocalLengthM * FocalLengthM / 3.0
                 * (Math.Pow(1.0 + k, 1.5) - 1.0);
        }
    }

    /// <summary>Расстояние от фокуса до точки края зеркала, м.</summary>
    public double RimSlantDistanceM
    {
        get
        {
            double r = DiameterM / 2.0;
            double z = DepthM;
            double dz = FocalLengthM - z;
            return Math.Sqrt(r * r + dz * dz);
        }
    }

    /// <summary>
    /// Масса зеркала, кг: лист плюс рёбра жёсткости.
    /// </summary>
    /// <remarks>
    /// В прежней версии слагаемое рёбер записывалось как площадь на
    /// плотность без толщины, то есть как сплошной металл в полметра:
    /// зеркало диаметром 1.7 м весило 21.7 тонны вместо 34 кг.
    /// </remarks>
    public double WeightKg(double sheetThicknessM, double densityKgPerM3, double ribMassFraction)
        => SurfaceAreaM2 * sheetThicknessM * densityKgPerM3 * (1.0 + ribMassFraction);
}
