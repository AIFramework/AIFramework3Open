using AI.Insights;

namespace AI.Simulation.Planning;

/// <summary>Результат поиска плана</summary>
/// <typeparam name="TState">Тип состояния</typeparam>
/// <param name="Found">Найден ли план</param>
/// <param name="Actions">Действия плана по порядку</param>
/// <param name="States">Состояния от начального до целевого</param>
/// <param name="Cost">Суммарная стоимость плана</param>
/// <param name="Expanded">Сколько состояний раскрыто при поиске</param>
/// <param name="LimitReached">Прерван ли поиск пределом раскрытий</param>
/// <param name="Optimal">Гарантирована ли оптимальность: план найден при допустимой эвристике</param>
public sealed record Plan<TState>(
    bool Found,
    IReadOnlyList<string> Actions,
    IReadOnlyList<TState> States,
    double Cost,
    int Expanded,
    bool LimitReached,
    bool Optimal) : IInterpretable
{
    /// <summary>Число действий в плане</summary>
    public int Length => Actions.Count;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string summary = Found
            ? $"План найден: действий {Length}, стоимость {Fmt.Num(Cost, 2)}, раскрыто состояний {Expanded}."
            : LimitReached
                ? $"Предел поиска исчерпан после {Expanded} раскрытых состояний: план не найден."
                : $"Цель недостижима: все {Expanded} достижимых состояний раскрыты.";

        var builder = new InterpretationBuilder("Планирование действий")
            .Summary(summary)
            .Metric("Найден", Found ? "да" : "нет", null, null,
                Found ? MetricQuality.Good : LimitReached ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Действий", Length, null, "в плане", MetricQuality.Unknown, 0)
            .Metric("Стоимость", Cost, null, "сумма стоимостей действий", MetricQuality.Unknown, 2)
            .Metric("Раскрыто", Expanded, null, "состояний при поиске", MetricQuality.Unknown, 0);

        for (int i = 0; i < Math.Min(Length, 12); i++)
            builder = builder.Metric($"Шаг {i + 1}", Actions[i]);

        return builder
            .FindingIf(Found && Optimal,
                "План оптимален: эвристика не переоценивает остаток пути, а цель проверяется при раскрытии, "
                + "поэтому более дешёвого плана не существует.")
            .FindingIf(!Found && !LimitReached,
                "Недостижимость доказана: поиск перебрал всё, что достижимо из начального состояния. "
                + "Это свойство задачи, а не нехватка времени.")
            .WarningIf(Found && !Optimal,
                "Эвристика объявлена недопустимой: план корректен, но более дешёвый может существовать.")
            .WarningIf(LimitReached,
                "Поиск прерван пределом: план не найден, но и не доказано, что его нет.")
            .Warning("План верен для модели мира, заданной действиями. Если действия в реальности "
                + "срабатывают не всегда, план нужно перепроверять по ходу исполнения.")
            .Build();
    }
}
