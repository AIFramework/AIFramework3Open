# Оборотный капитал и финансовый цикл

Драйверы финансового цикла, цена одного дня оборота и сумма, которую можно
высвободить, не меняя ни выручку, ни маржу.

## Постановка задачи

Дано: отчётность компании и целевые сроки оборота — по дебиторской
задолженности, запасам и расчётам с поставщиками.

Требуется: разложить финансовый цикл на драйверы, оценить, сколько стоит один
день каждого из них, и посчитать потенциал высвобождения денег.

Где встречается: программа повышения эффективности, подготовка к кассовому
разрыву при быстром росте, переговоры об отсрочке с поставщиками, оценка
потребности в оборотном кредитовании.

## Теория

Финансовый цикл показывает, сколько дней деньги компании связаны в обороте:

$$
\mathrm{DSO} = \frac{\mathrm{AR}}{\mathrm{Revenue}/365},
\quad
\mathrm{DIO} = \frac{\mathrm{Inventory}}{\mathrm{COGS}/365},
\quad
\mathrm{DPO} = \frac{\mathrm{AP}}{\mathrm{COGS}/365},
$$

$$
\mathrm{CCC} = \mathrm{DSO} + \mathrm{DIO} - \mathrm{DPO}.
$$

Каждый день цикла имеет цену: для дебиторской задолженности это дневная
выручка, для запасов и кредиторской задолженности — дневная себестоимость.
Умножив отклонение от целевого срока на эту цену, получаем сумму, которую
можно высвободить:

$$
\Delta\mathrm{Cash} = (\mathrm{DSO} - \mathrm{DSO}^{*})\cdot\frac{\mathrm{Revenue}}{365}
+ (\mathrm{DIO} - \mathrm{DIO}^{*})\cdot\frac{\mathrm{COGS}}{365}
+ (\mathrm{DPO}^{*} - \mathrm{DPO})\cdot\frac{\mathrm{COGS}}{365}.
$$

Высвобожденные деньги имеют цену — стоимость финансирования оборота, поэтому
эффект удобно выражать и в годовой экономии на процентах.

Отдельно считается **потребность в финансировании роста**: оборотный капитал
масштабируется вместе с выручкой, поэтому быстрый рост при неизменном цикле
требует денег, которых у прибыльной компании может не оказаться. Это самая
частая причина кассовых разрывов в растущем бизнесе и главный аргумент в пользу
работы с циклом до привлечения кредита.

Отрицательный финансовый цикл — отдельная модель: компания получает деньги от
покупателей раньше, чем платит поставщикам, и рост сам себя финансирует. Плата
за это — зависимость от условий поставщиков.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(1)$ | три драйвера, фиксированный набор формул |
| Память | $O(1)$ | три записи о драйверах |

## API

| Метод | Описание |
|-------|----------|
| `WorkingCapitalAnalysis.Analyze(statement, targets)` | Цикл, драйверы и потенциал высвобождения |
| `WorkingCapitalTargets` | Целевые сроки оборота и стоимость финансирования |
| `WorkingCapitalResult.Drivers` | Цена дня и эффект по каждому драйверу |
| `WorkingCapitalResult.PotentialCashRelease` | Сумма высвобождения при выходе на цели |
| `WorkingCapitalResult.FundingPerGrowthPoint` | Деньги на каждый процент прироста выручки |

Исходники: `src/AI.Economics/Statements/WorkingCapitalAnalysis.cs`.

## Код

```csharp
using AI.Economics.Statements;

var business = new FinancialStatement
{
    Company = "Компания", Period = "2024",
    TotalAssets = 1_000_000_000, CurrentAssets = 450_000_000,
    Cash = 80_000_000, AccountsReceivable = 180_000_000, Inventory = 150_000_000,
    PropertyPlantEquipment = 500_000_000, IntangibleAssets = 50_000_000,
    TotalLiabilities = 480_000_000, CurrentLiabilities = 240_000_000,
    AccountsPayable = 150_000_000, ShortTermDebt = 90_000_000, LongTermDebt = 240_000_000,
    Revenue = 1_000_000_000, CostOfGoodsSold = 600_000_000,
    OperatingExpenses = 180_000_000, Depreciation = 50_000_000,
    InterestExpense = 42_900_000, IncomeTax = 25_400_000, NetIncome = 101_700_000,
    OperatingCashFlow = 140_000_000, CapitalExpenditures = 60_000_000,
};

WorkingCapitalResult cycle = WorkingCapitalAnalysis.Analyze(business);

Console.WriteLine($"DSO {cycle.DaysSalesOutstanding:F0} дн., DIO {cycle.DaysInventoryOutstanding:F0} дн., " +
                  $"DPO {cycle.DaysPayablesOutstanding:F0} дн.");
Console.WriteLine($"Финансовый цикл {cycle.CashConversionCycle:F0} дн., " +
                  $"операционный {cycle.OperatingCycle:F0} дн.");
Console.WriteLine($"В обороте связано {cycle.WorkingCapital:N0} ({cycle.WorkingCapitalToRevenue:P1} выручки)");
Console.WriteLine($"Финансирование роста: {cycle.FundingPerGrowthPoint:N0} на процент прироста");
```

Драйверы показывают, где именно лежит резерв и сколько стоит один день:

```csharp
foreach (WorkingCapitalDriver driver in cycle.Drivers)
{
    Console.WriteLine($"{driver.Name}: {driver.Days:F0} дн. при цели {driver.TargetDays:F0} дн., " +
                      $"день стоит {driver.AmountPerDay:N0}, эффект {driver.CashImpact:N0}");
}
```

Собственные целевые сроки и стоимость финансирования задаются явно:

```csharp
var targets = new WorkingCapitalTargets
{
    DaysSalesOutstanding = 30,
    DaysInventoryOutstanding = 40,
    DaysPayablesOutstanding = 60,
    CostOfFunding = 0.22,
};

WorkingCapitalResult ambitious = WorkingCapitalAnalysis.Analyze(business, targets);

Console.WriteLine($"Потенциал высвобождения {ambitious.PotentialCashRelease:N0}, " +
                  $"экономия на процентах {ambitious.AnnualFundingSaving:N0} в год");

Console.WriteLine(ambitious.Interpret().ToLlmText());
```

## Ограничения

- Расчёт по годовым показателям сглаживает сезонность. Если продажи
  неравномерны, сроки оборота на отчётную дату заметно расходятся со средними
  за период, и потенциал высвобождения окажется завышенным.
- Целевые сроки — управленческое решение, а не расчёт. Отраслевой бенчмарк
  задаёт коридор, но конкретная цель зависит от переговорной позиции.
- Удлинение отсрочки поставщикам не бесплатно: отказ от скидки в 2% за
  20 дней в годовом выражении обходится дороже банковского кредита. Сравнивать
  надо именно эти величины.
- Резерв почти всегда сконцентрирован в узкой части портфеля покупателей и
  товарных групп. Считать нужно по сегментам, а не по компании целиком.
- Модель не учитывает авансы полученные и выданные. Для бизнеса с предоплатой
  их надо включать в расчёт цикла отдельно.

## См. также

- [Коэффициентный анализ отчётности](financial_ratios.md) — оборачиваемость среди прочих групп
- [Разложение Дюпона](dupont.md) — как оборачиваемость влияет на доходность собственника
- [Качество прибыли](earnings_quality.md) — почему прибыль не превращается в деньги
