using AI.Microwave.Models;

namespace AI.Microwave.Calculators;

/// <summary>
/// Расчёт одного типа антенны по общему техническому заданию.
/// Реализации сравниваются между собой через <see cref="AntennaDesignResult"/>.
/// </summary>
public interface IAntennaCalculator
{
    /// <summary>Название типа антенны.</summary>
    string AntennaType { get; }

    /// <summary>Полный расчёт конструкции по заданию.</summary>
    AntennaDesignResult Calculate(AntennaParameters parameters);

    /// <summary>Описание принципа работы.</summary>
    string GetDescription();

    /// <summary>Достоинства типа.</summary>
    string GetAdvantages();

    /// <summary>Недостатки типа.</summary>
    string GetDisadvantages();
}
