using AI.Geometry.Transforms;
using AI.Microwave.Safety;
using AlgebraicVector = AI.DataStructs.Algebraic.Vector;
using Vector3 = AI.Geometry.Primitives.Vector3;

namespace AI.Microwave.Propagation;

/// <summary>Поляризационная схема антенной решётки</summary>
public enum ArrayPolarization
{
    /// <summary>Одна поляризация на позицию с наклоном <see cref="AntennaArray.PolarizationSlantDeg"/></summary>
    Single,

    /// <summary>Две ортогональные поляризации на позицию: ζ и ζ − 90°, например ±45°</summary>
    Dual
}

/// <summary>
/// Антенная решётка — прямоугольная панель одинаковых элементов с диаграммой, поляризацией и ориентацией
/// по TR 38.901, разделы 7.1 и 7.3.
/// </summary>
/// <remarks>
/// <para>
/// В локальной системе координат элементы лежат в плоскости YZ — столбцы вдоль Y, строки вдоль Z, центр панели
/// в начале координат, — а главное направление — ось X. Ориентация задаётся тремя поворотами: азимутом α вокруг Z,
/// наклоном β вокруг Y (положительный наклоняет луч вниз) и механическим наклоном γ вокруг X;
/// R = R_z(α)·R_y(β)·R_x(γ) собирается из кватернионов.
/// </para>
/// <para>
/// Поле элемента — модель поляризации 2 из 38.901: <c>F_θ' = √A'·cos ζ</c>, <c>F_φ' = √A'·sin ζ</c>, где A' —
/// диаграмма по мощности в локальных углах, ζ — наклон поляризации (0 — вертикальная). В глобальные составляющие
/// поле переводится проекцией повёрнутых локальных ортов θ̂', φ̂' на глобальные θ̂, φ̂ — это то же, что формула
/// (7.1-11) с углом ψ, но без особых случаев на полюсах.
/// </para>
/// <para>
/// Диаграмма элемента — любая <see cref="AntennaPattern"/>. Элемент 38.901 (таблица 7.3-1) — это гауссова
/// аппроксимация <see cref="GaussianPattern"/> с шириной 65° в обеих плоскостях и полкой 30 дБ при усилении
/// 8 дБи: <see cref="Tr38901Panel"/>.
/// </para>
/// </remarks>
public sealed record AntennaArray
{
    /// <summary>Создаёт панель из одинаковых изотропных элементов с вертикальной поляризацией</summary>
    /// <param name="rows">Число строк (по вертикали)</param>
    /// <param name="columns">Число столбцов (по горизонтали)</param>
    public AntennaArray(int rows = 1, int columns = 1)
    {
        if (rows < 1)
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "Нужна хотя бы одна строка");

        if (columns < 1)
            throw new ArgumentOutOfRangeException(nameof(columns), columns, "Нужен хотя бы один столбец");

        Rows = rows;
        Columns = columns;
    }

    /// <summary>Одиночный изотропный элемент с вертикальной поляризацией</summary>
    public static AntennaArray Isotropic { get; } = new();

    /// <summary>Диаграмма элемента TR 38.901: 65° по уровню −3 дБ в обеих плоскостях, полка 30 дБ</summary>
    public static AntennaPattern Tr38901ElementPattern { get; } = new GaussianPattern
    {
        AzimuthBeamwidthDeg = 65,
        ElevationBeamwidthDeg = 65,
        FrontToBackDb = 30,
    };

    /// <summary>Усиление элемента TR 38.901 в максимуме, дБи</summary>
    public const double Tr38901ElementGainDbi = 8;

    /// <summary>Число строк</summary>
    public int Rows { get; }

    /// <summary>Число столбцов</summary>
    public int Columns { get; }

    /// <summary>Шаг по вертикали в длинах волны</summary>
    public double VerticalSpacingWavelengths { get; init; } = 0.5;

    /// <summary>Шаг по горизонтали в длинах волны</summary>
    public double HorizontalSpacingWavelengths { get; init; } = 0.5;

    /// <summary>Поляризационная схема</summary>
    public ArrayPolarization Polarization { get; init; } = ArrayPolarization.Single;

    /// <summary>Наклон поляризации ζ первого элемента позиции, градусы: 0 — вертикальная, 90 — горизонтальная</summary>
    public double PolarizationSlantDeg { get; init; }

    /// <summary>Диаграмма элемента</summary>
    public AntennaPattern Pattern { get; init; } = new IsotropicPattern();

    /// <summary>Усиление элемента в максимуме, дБи</summary>
    public double GainDbi { get; init; }

    /// <summary>Азимут главного направления α, градусы, против часовой стрелки от оси X</summary>
    public double BearingDeg { get; init; }

    /// <summary>Наклон луча β, градусы: положительный — вниз</summary>
    public double DowntiltDeg { get; init; }

    /// <summary>Механический поворот панели γ вокруг главного направления, градусы</summary>
    public double MechanicalSlantDeg { get; init; }

    /// <summary>Поляризаций на позицию: 1 или 2</summary>
    public int PolarizationsPerPosition => Polarization == ArrayPolarization.Dual ? 2 : 1;

    /// <summary>Число элементов (портов)</summary>
    public int ElementCount => Rows * Columns * PolarizationsPerPosition;

    /// <summary>
    /// Панель из элементов TR 38.901 с шагом λ/2: по умолчанию двухполяризационная ±45°, иначе вертикальная
    /// </summary>
    /// <param name="rows">Число строк</param>
    /// <param name="columns">Число столбцов</param>
    /// <param name="dualPolarized">Две поляризации ±45° на позицию</param>
    public static AntennaArray Tr38901Panel(int rows, int columns, bool dualPolarized = true)
        => new(rows, columns)
        {
            Pattern = Tr38901ElementPattern,
            GainDbi = Tr38901ElementGainDbi,
            Polarization = dualPolarized ? ArrayPolarization.Dual : ArrayPolarization.Single,
            PolarizationSlantDeg = dualPolarized ? 45 : 0,
        };

    /// <summary>Наклон поляризации элемента, градусы</summary>
    /// <param name="element">Номер элемента</param>
    public double ElementSlantDeg(int element)
    {
        CheckElement(element);

        return element % PolarizationsPerPosition == 0 ? PolarizationSlantDeg : PolarizationSlantDeg - 90;
    }

    /// <summary>Смещение элемента от центра панели в глобальной системе, м</summary>
    /// <param name="element">Номер элемента: позиции по строкам, внутри позиции — поляризации</param>
    /// <param name="wavelengthM">Длина волны, м</param>
    public Vector3 ElementOffset(int element, double wavelengthM) => ElementOffset(RotationMatrix(), element, wavelengthM);

    /// <summary>Составляющие поля элемента F_θ и F_φ в глобальной системе, в корнях из разов</summary>
    /// <param name="element">Номер элемента</param>
    /// <param name="zenithDeg">Зенитный угол направления, градусы: 0 — вверх, 90 — горизонт</param>
    /// <param name="azimuthDeg">Азимут направления, градусы, против часовой стрелки от оси X</param>
    public (double Theta, double Phi) FieldPattern(int element, double zenithDeg, double azimuthDeg)
        => FieldPattern(RotationMatrix(), element, Spherical.Direction(zenithDeg, azimuthDeg));

    /// <summary>Матрица поворота из локальной системы в глобальную</summary>
    internal double[,] RotationMatrix()
    {
        Quaternion rotation = Quaternion.FromAxisAngle(new AlgebraicVector(0, 0, 1), BearingDeg * Spherical.Rad)
            * Quaternion.FromAxisAngle(new AlgebraicVector(0, 1, 0), DowntiltDeg * Spherical.Rad)
            * Quaternion.FromAxisAngle(new AlgebraicVector(1, 0, 0), MechanicalSlantDeg * Spherical.Rad);

        var matrix = rotation.ToRotationMatrix3();
        var result = new double[3, 3];

        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
                result[i, j] = matrix[i, j];
        }

        return result;
    }

    internal Vector3 ElementOffset(double[,] rotation, int element, double wavelengthM)
    {
        CheckElement(element);

        int position = element / PolarizationsPerPosition;
        int row = position / Columns, column = position % Columns;

        var local = new Vector3(
            0,
            (column - ((Columns - 1) / 2.0)) * HorizontalSpacingWavelengths * wavelengthM,
            (row - ((Rows - 1) / 2.0)) * VerticalSpacingWavelengths * wavelengthM);

        return Spherical.Rotate(rotation, local);
    }

    internal (double Theta, double Phi) FieldPattern(double[,] rotation, int element, Vector3 direction)
    {
        double slant = ElementSlantDeg(element) * Spherical.Rad;
        Vector3 local = Spherical.RotateBack(rotation, direction);
        (double zenith, double azimuth) = Spherical.Angles(local);

        // Диаграмма отсчитывает азимут от главного направления по часовой стрелке, угол места — от горизонта
        double attenuation = Pattern.AttenuationDb(-azimuth, 90 - zenith);
        double amplitude = Math.Pow(10, (GainDbi - attenuation) / 20);

        (Vector3 thetaLocal, Vector3 phiLocal) = Spherical.Basis(local);
        Vector3 field = Spherical.Rotate(rotation, (amplitude * Math.Cos(slant) * thetaLocal) + (amplitude * Math.Sin(slant) * phiLocal));
        (Vector3 theta, Vector3 phi) = Spherical.Basis(direction);

        return (field.Dot(theta), field.Dot(phi));
    }

    private void CheckElement(int element)
    {
        if (element < 0 || element >= ElementCount)
            throw new ArgumentOutOfRangeException(nameof(element), element, $"Номер элемента должен быть от 0 до {ElementCount - 1}");
    }
}

/// <summary>Сферические углы TR 38.901: зенит θ от оси Z, азимут φ от оси X против часовой стрелки</summary>
internal static class Spherical
{
    public const double Deg = 180 / Math.PI;
    public const double Rad = Math.PI / 180;

    /// <summary>Единичный вектор направления по зениту и азимуту в градусах</summary>
    public static Vector3 Direction(double zenithDeg, double azimuthDeg)
    {
        double theta = zenithDeg * Rad, phi = azimuthDeg * Rad;

        return new Vector3(Math.Sin(theta) * Math.Cos(phi), Math.Sin(theta) * Math.Sin(phi), Math.Cos(theta));
    }

    /// <summary>Зенит и азимут единичного вектора, градусы; азимут в (−180, 180]</summary>
    public static (double ZenithDeg, double AzimuthDeg) Angles(Vector3 unit)
        => (Math.Acos(Math.Clamp(unit.Z, -1, 1)) * Deg, Math.Atan2(unit.Y, unit.X) * Deg);

    /// <summary>Орты θ̂ и φ̂ в направлении единичного вектора</summary>
    public static (Vector3 Theta, Vector3 Phi) Basis(Vector3 unit)
    {
        double theta = Math.Acos(Math.Clamp(unit.Z, -1, 1));
        double phi = Math.Atan2(unit.Y, unit.X);

        return (
            new Vector3(Math.Cos(theta) * Math.Cos(phi), Math.Cos(theta) * Math.Sin(phi), -Math.Sin(theta)),
            new Vector3(-Math.Sin(phi), Math.Cos(phi), 0));
    }

    /// <summary>Угол, приведённый к (−180, 180]</summary>
    public static double WrapAzimuth(double angleDeg)
    {
        double a = (angleDeg + 180) % 360;

        return (a <= 0 ? a + 360 : a) - 180;
    }

    /// <summary>Зенитный угол, отражённый в [0, 180]: θ из [180, 360] переходит в 360 − θ</summary>
    public static double FoldZenith(double angleDeg)
    {
        double a = angleDeg % 360;

        if (a < 0)
            a += 360;

        return a > 180 ? 360 - a : a;
    }

    public static Vector3 Rotate(double[,] m, Vector3 v) => new(
        (m[0, 0] * v.X) + (m[0, 1] * v.Y) + (m[0, 2] * v.Z),
        (m[1, 0] * v.X) + (m[1, 1] * v.Y) + (m[1, 2] * v.Z),
        (m[2, 0] * v.X) + (m[2, 1] * v.Y) + (m[2, 2] * v.Z));

    public static Vector3 RotateBack(double[,] m, Vector3 v) => new(
        (m[0, 0] * v.X) + (m[1, 0] * v.Y) + (m[2, 0] * v.Z),
        (m[0, 1] * v.X) + (m[1, 1] * v.Y) + (m[2, 1] * v.Z),
        (m[0, 2] * v.X) + (m[1, 2] * v.Y) + (m[2, 2] * v.Z));
}
