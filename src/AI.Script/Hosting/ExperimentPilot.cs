using AI.Script.Runtime;

namespace AI.Script.Hosting;

/// <summary>
/// Пробный прогон опыта: одно испытание вместо плана и оценка расхода на весь план.
/// </summary>
/// <remarks>
/// Сколько стоит опыт, заранее не знает никто: в испытании может быть один вызов модели, а может
/// быть десять, и промпт разной длины. Надежнее всего посчитать одно испытание по-настоящему и
/// умножить на размер плана. Хост кладет пилот в настройки прогона, первый <c>exp.run</c>
/// считает первую точку плана, записывает оценку сюда и останавливает прогон кодом
/// <see cref="Semantics.DiagnosticCodes.PilotStopped"/>. Что делать с оценкой (спросить человека,
/// сверить с бюджетом), решает хост.
/// <para>
/// Скрипт, который до <c>exp.run</c> не дошел, отрабатывает пробным прогоном целиком, и оценки
/// тогда нет: считать было нечего, а итог уже получен.
/// </para>
/// </remarks>
public sealed class ExperimentPilot
{
    /// <summary>Оценка по первому <c>exp.run</c>; <c>null</c> — до опыта прогон не дошел.</summary>
    public ExperimentEstimate? Estimate { get; private set; }

    /// <summary>Записывает оценку; повторная запись не заменяет первую.</summary>
    public void Record(ExperimentEstimate estimate) => Estimate ??= estimate;
}

/// <summary>
/// Оценка расхода опыта по одному испытанию.
/// </summary>
/// <param name="Trials">Сколько испытаний в плане с повторами.</param>
/// <param name="Before">Расход прогона до опыта: подготовка данных, разметка.</param>
/// <param name="Trial">Расход одного пробного испытания.</param>
public sealed record ExperimentEstimate(int Trials, ExternalUsage Before, ExternalUsage Trial)
{
    /// <summary>Внешних вызовов на весь прогон.</summary>
    public int Calls => Before.Calls + (Trials * Trial.Calls);

    /// <summary>Токенов на весь прогон.</summary>
    public long Tokens => Before.Tokens + (Trials * Trial.Tokens);

    /// <summary>Стоимость всего прогона в единицах биллинга.</summary>
    public decimal Cost => Before.Cost + (Trials * Trial.Cost);
}
