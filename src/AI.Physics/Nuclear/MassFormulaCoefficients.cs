namespace AI.Physics.Nuclear;

/// <summary>Коэффициенты формулы Вайцзеккера, МэВ</summary>
/// <param name="VolumeMeV">Объёмный коэффициент</param>
/// <param name="SurfaceMeV">Поверхностный коэффициент</param>
/// <param name="CoulombMeV">Кулоновский коэффициент</param>
/// <param name="AsymmetryMeV">Коэффициент асимметрии</param>
/// <param name="PairingMeV">Коэффициент спаривания; поправка равна ему, делённому на √A</param>
public readonly record struct MassFormulaCoefficients(
    double VolumeMeV,
    double SurfaceMeV,
    double CoulombMeV,
    double AsymmetryMeV,
    double PairingMeV)
{
    /// <summary>
    /// Подгонка по измеренным массам из учебника Рольфа (Rohlf, Modern Physics from α to Z⁰, 1994):
    /// 15.75, 17.8, 0.711, 23.7 и 11.18 МэВ
    /// </summary>
    public static MassFormulaCoefficients Rohlf { get; } = new(15.75, 17.8, 0.711, 23.7, 11.18);
}
