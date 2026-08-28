# CAC, LTV и срок окупаемости привлечения

Базовый расчёт юнит-экономики: во сколько обходится клиент, сколько он приносит
за всю жизнь и через сколько месяцев возвращаются деньги, потраченные на его
привлечение.

## Постановка задачи

Дано: затраты на маркетинг и продажи за период, число привлечённых клиентов,
средний доход с клиента за месяц, валовая маржа, отток и ставка
дисконтирования.

Требуется: стоимость привлечения $CAC$, пожизненная ценность $LTV$, их
отношение и срок окупаемости.

Где встречается: защита бюджета на привлечение, решение о масштабировании
канала, инвесторская модель, оценка сегмента перед запуском.

## Теория

Маржинальный вклад клиента за один период:

$$
m \;=\; ARPU \cdot g \;-\; c_{var},
$$

где $g$ — доля валовой маржи, $c_{var}$ — переменные затраты на клиента.

Пожизненная ценность — сумма дисконтированных вкладов, взвешенных вероятностью
дожития $S(t)$:

$$
LTV \;=\; m \sum_{t \ge 0} \frac{S(t)}{(1+d)^t}.
$$

При постоянном оттоке $c$ доживание геометрическое, $S(t) = (1-c)^t$, и сумма
берётся в замкнутом виде:

$$
LTV \;=\; m\,\frac{1+d}{c+d}.
$$

Отсюда видно, чем опасен расчёт без дисконтирования: при $d = 0$ формула
вырождается в $m/c$ и приписывает сегодняшнюю ценность деньгам, которые придут
через несколько лет.

Срок окупаемости — момент, когда накопленный вклад покрывает $CAC$:

$$
T_{payback} \;=\; \min \left\{ T : m \sum_{t=0}^{T-1} \frac{S(t)}{(1+d)^t} \ge CAC \right\}.
$$

Реализация интерполирует его внутри периода: округление до целых месяцев даёт
систематическую ошибку почти в месяц.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(H)$ | $H$ — горизонт в периодах |
| Память | $O(H)$ | Кривые доживания и накопленного вклада |

## API

| Метод | Описание |
|-------|----------|
| `UnitEconomicsCalculator.Compute(UnitEconomicsInput)` | Полный расчёт: CAC, LTV, окупаемость, кривые |
| `UnitEconomicsCalculator.LtvFromChurn(m, churn, discount)` | Замкнутая формула при постоянном оттоке |
| `UnitEconomicsCalculator.LtvFromCurve(m, survival, discount)` | LTV по произвольной кривой удержания |
| `UnitEconomicsInput.Survival` | Кривая удержания; приоритетнее `ChurnRate` |
| `UnitEconomicsResult.CumulativeNet` | Накопленная прибыль за вычетом CAC по периодам |

Исходники: `src/AI.Economics/UnitEconomics/`.

## Код

```csharp
using AI.Economics.UnitEconomics;

var input = new UnitEconomicsInput
{
    MarketingSpend = 800_000,
    SalesSpend = 100_000,
    NewCustomers = 300,
    RevenuePerPeriod = 6_000,
    GrossMarginRate = 0.8,
    ChurnRate = 0.045,
    DiscountRate = 0.01,
    Horizon = 36,
};

UnitEconomicsResult result = UnitEconomicsCalculator.Compute(input);

Console.WriteLine($"CAC            {result.Cac:N0}");
Console.WriteLine($"LTV            {result.Ltv:N0}");
Console.WriteLine($"LTV/CAC        {result.LtvToCac:F2}");
Console.WriteLine($"Окупаемость    {result.CacPaybackPeriods:F1} мес.");
Console.WriteLine($"Срок жизни     {result.ExpectedLifetimePeriods:F1} мес.");
```

Кривую удержания из когортного анализа можно подставить напрямую — тогда отток
перестаёт быть константой:

```csharp
using AI.DataStructs.Algebraic;

var curve = new Vector(1.0, 0.62, 0.53, 0.48, 0.45, 0.44, 0.43);

UnitEconomicsResult byCurve = UnitEconomicsCalculator.Compute(new UnitEconomicsInput
{
    CacOverride = 3_000,
    RevenuePerPeriod = 6_000,
    GrossMarginRate = 0.8,
    DiscountRate = 0.01,
    Survival = curve,
});

Console.WriteLine($"LTV по кривой  {byCurve.Ltv:N0}");
```

## Ограничения

- Формула с постоянным оттоком занижает LTV, если удержание со временем растёт;
  для реальных данных подгоняйте кривую через
  [подгонку удержания](retention_fit.md).
- Бесконечный горизонт (`Horizon = 0`) допустим только при `ChurnRate > 0`:
  иначе ряд расходится и LTV становится бесконечным.
- Расчёт не учитывает затраты на удержание и апсейл: если они значимы, вычитайте
  их через `VariableCostPerPeriod`.
- `LtvToCac` при нулевом CAC равен бесконечности — органика в среднем показателе
  канала не должна участвовать, для этого есть
  [экономика каналов](channel_mix.md).

## См. также

- [Экономика каналов привлечения](channel_mix.md) — blended CAC против paid CAC
- [Подгонка кривой удержания](retention_fit.md) — откуда берётся `Survival`
- [SaaS-метрики](saas_health.md) — CAC payback в наборе показателей здоровья
