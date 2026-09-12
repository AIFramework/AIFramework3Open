using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>Слагаемые формулы Вайцзеккера</summary>
/// <param name="Volume">Объёмная энергия: каждый нуклон связан с соседями, и связь растёт с их числом</param>
/// <param name="Surface">Поверхностная поправка: нуклонам на поверхности не хватает соседей</param>
/// <param name="Coulomb">Кулоновское отталкивание протонов</param>
/// <param name="Asymmetry">Поправка за неравенство чисел протонов и нейтронов</param>
/// <param name="Pairing">Спаривание: плюс у чётно-чётных ядер, минус у нечётно-нечётных</param>
public readonly record struct MassFormulaTerms(
    Quantity Volume,
    Quantity Surface,
    Quantity Coulomb,
    Quantity Asymmetry,
    Quantity Pairing)
{
    /// <summary>Энергия связи: объём минус поверхность, отталкивание и асимметрия, плюс спаривание</summary>
    public Quantity Total => new(
        Volume.SiValue - Surface.SiValue - Coulomb.SiValue - Asymmetry.SiValue + Pairing.SiValue,
        Dimension.Energy);
}
