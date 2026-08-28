namespace AI.Economics.Clv;

/// <summary>
/// Модель числа будущих покупок клиента без контракта.
/// </summary>
/// <remarks>
/// Общий интерфейс нужен затем, чтобы расчёт CLV не зависел от выбора между
/// BG/NBD и Pareto/NBD: обе модели дают одинаковый набор величин, и заменить
/// одну другой можно без переписывания денежной части.
/// </remarks>
public interface ITransactionModel
{
    /// <summary>Вероятность того, что клиент всё ещё активен.</summary>
    /// <param name="customer">Сводка клиента.</param>
    double ProbabilityAlive(CustomerSummary customer);

    /// <summary>Ожидаемое число покупок за горизонт.</summary>
    /// <param name="customer">Сводка клиента.</param>
    /// <param name="horizon">Горизонт прогноза в единицах времени модели.</param>
    double ExpectedTransactions(CustomerSummary customer, double horizon);
}
