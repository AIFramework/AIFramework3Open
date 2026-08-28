# Предсказание банкротства на моделях фреймворка

Классификаторы `AI.ML` на признаках отчётности, стратифицированный скользящий
контроль и перестановочная важность — с обязательным сравнением с баллом
Альтмана.

## Постановка задачи

Дано: отчётность компаний с известным исходом — обанкротилась компания в
горизонте наблюдения или нет.

Требуется: модель, ранжирующая компании по риску банкротства лучше классических
формул, честная оценка её качества и понимание того, какие признаки на неё
влияют.

Где встречается: кредитный скоринг корпоративных заёмщиков, отбор контрагентов,
управление портфелем облигаций, раннее предупреждение по портфелю.

## Теория

Из отчётности извлекается набор признаков, повторяющий логику классических
моделей банкротства, но дополненный качеством прибыли и покрытием долга
денежным потоком: рабочий капитал к активам, нераспределённая прибыль к
активам, операционная прибыль к активам, капитал к обязательствам,
оборачиваемость, текущая ликвидность, чистая прибыль к активам, денежный поток
к обязательствам, долг к прибыли, начисления и покрытие процентов.

Признаки обрезаются по разумным границам и стандартизируются по обучающей
выборке — без этого расстояния в методах, чувствительных к масштабу, определялись
бы одним признаком с наибольшим разбросом.

Обучается один из классификаторов фреймворка: логистическая регрессия (как
интерпретируемая базовая линия), байесовский классификатор или машина опорных
векторов.

**Оценка качества** ведётся на стратифицированном скользящем контроле: доля
банкротств в каждом блоке сохраняется, а вероятности собираются вне обучения.
Разрыв между качеством на обучающей выборке и на контроле — прямая мера
переобучения, и именно его нужно смотреть, а не абсолютное значение Джини.

**Важность признаков** считается перестановочным методом: значения одного
признака перемешиваются, и измеряется падение площади под кривой. Метод не
зависит от устройства модели, поэтому одинаково применим ко всем
классификаторам.

Отдельно возвращается балл Альтмана на тех же данных. Прирост качества
относительно классической формулы — единственное честное обоснование сложной
модели: если его нет, работать нужно с формулой.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(F\,C(n,k) + k\,n)$ | $F$ блоков контроля, $C$ — стоимость обучения |
| Память | $O(n\,k)$ | стандартизованная матрица признаков |

## API

| Метод | Описание |
|-------|----------|
| `BankruptcyPredictor.Train(observations, kind, folds, seed)` | Обучение с оценкой на скользящем контроле |
| `BankruptcyPredictor.Predict(statement, threshold)` | Вероятность банкротства и профиль признаков |
| `BankruptcyPredictor.CompareAll(observations, folds, seed)` | Все модели по убыванию качества |
| `BankruptcyPredictor.ExtractFeatures(statement)` | Признаки до стандартизации |
| `BankruptcyModelResult.CrossValidated` / `InSample` / `OverfitGap` | Честная и оптимистичная оценки |
| `BankruptcyModelResult.Importances` | Перестановочная важность признаков |

Исходники: `src/AI.Economics/Statements/BankruptcyPredictor.cs`.

## Код

```csharp
using AI.Economics.Statements;

static FinancialStatement Build(string name, double quality, double revenue)
{
    double cogs = revenue * (0.75 - (0.3 * quality));
    double opex = revenue * (0.28 - (0.1 * quality));
    double depreciation = revenue * 0.05;
    double operating = revenue - cogs - opex - depreciation;

    double receivables = revenue * (0.27 - (0.15 * quality));
    double inventory = cogs * (0.35 - (0.2 * quality));
    double currentAssets = receivables + inventory + (revenue * 0.1);
    double assets = currentAssets + (revenue * 0.55);

    double debt = assets * (0.75 - (0.55 * quality));
    double payables = cogs * 0.2;
    double currentLiabilities = payables + (debt * 0.3);
    double liabilities = currentLiabilities + (debt * 0.7);

    double interest = debt * 0.13;
    double pretax = operating - interest;
    double tax = Math.Max(0, pretax * 0.2);
    double net = pretax - tax;

    return new FinancialStatement
    {
        Company = name, Period = "2024",
        TotalAssets = assets, CurrentAssets = currentAssets,
        Cash = revenue * 0.1, AccountsReceivable = receivables, Inventory = inventory,
        PropertyPlantEquipment = revenue * 0.5, IntangibleAssets = revenue * 0.05,
        TotalLiabilities = liabilities, CurrentLiabilities = currentLiabilities,
        AccountsPayable = payables, ShortTermDebt = debt * 0.3, LongTermDebt = debt * 0.7,
        RetainedEarnings = (assets - liabilities) * 0.6,
        Revenue = revenue, CostOfGoodsSold = cogs, OperatingExpenses = opex,
        Depreciation = depreciation, InterestExpense = interest,
        IncomeTax = tax, NetIncome = net,
        OperatingCashFlow = net + depreciation - (revenue * 0.09 * (1 - quality)),
        CapitalExpenditures = revenue * 0.06,
        MarketCapitalization = Math.Max(assets - liabilities, 0) * 2.5,
    };
}

var sample = new List<BankruptcyObservation>();
var sampleRng = new Random(21);

for (int i = 0; i < 400; i++)
{
    double quality = Math.Clamp(0.5 + (0.25 * (sampleRng.NextDouble() - 0.5) * 4), 0.02, 0.98);
    double revenue = 200_000_000 * (0.5 + sampleRng.NextDouble());

    double probability = 1.0 / (1.0 + Math.Exp(6 * (quality - 0.45)));
    sample.Add(new BankruptcyObservation(Build($"Компания {i}", quality, revenue),
        sampleRng.NextDouble() < probability));
}

var predictor = new BankruptcyPredictor();
BankruptcyModelResult trained = predictor.Train(sample);

Console.WriteLine($"Джини на контроле {trained.CrossValidated.Gini:F3}, " +
                  $"на обучении {trained.InSample.Gini:F3}, разрыв {trained.OverfitGap:F3}");
Console.WriteLine($"Банкротств в выборке: {trained.Bankruptcies} из {trained.Observations}");
```

Важность признаков и сравнение моделей:

```csharp
foreach (FeatureImportance feature in trained.Importances.Take(5))
{
    Console.WriteLine($"{feature.Feature}: падение AUC {feature.Importance:F4} " +
                      $"(у выживших {feature.MeanHealthy:F2}, у банкротов {feature.MeanBankrupt:F2})");
}

IReadOnlyList<BankruptcyModelResult> models = BankruptcyPredictor.CompareAll(sample, folds: 4);

foreach (BankruptcyModelResult model in models)
    Console.WriteLine($"{model.Model}: Джини на контроле {model.CrossValidated.Gini:F3}");
```

Прогноз для конкретной компании обязательно сопровождается баллом Альтмана:

```csharp
BankruptcyPrediction verdict = predictor.Predict(Build("Проверяемая", 0.25, 300_000_000));

Console.WriteLine($"Вероятность банкротства {verdict.Probability:P2}, " +
                  $"балл Альтмана {verdict.AltmanZ:F2}");
Console.WriteLine(verdict.Interpret().ToLlmText());
```

## Ограничения

- Банкротство — редкое событие, и обучающая выборка почти всегда смещена: в неё
  попадают компании, дожившие до сдачи отчётности. Абсолютные вероятности из
  такой модели требуют перекалибровки на реальную частоту банкротств.
- При нескольких десятках банкротств оценки качества имеют широкий доверительный
  интервал, и разница между моделями чаще всего статистически незначима.
- Случайный скользящий контроль оптимистичен: отчётность и структура экономики
  меняются, поэтому проверять модель надо и на более позднем периоде.
- Модель наследует все проблемы входных данных. На искажённой отчётности она
  даёт ложное спокойствие — см. [M-score Бениша](beneish.md).
- Порог отсечения по умолчанию равен 0,5, что почти никогда не оптимально: для
  кредитора цена пропущенного банкротства на порядок выше цены отказа хорошему
  заёмщику, и порог надо подбирать по цене ошибок.

## См. также

- [Модели банкротства](distress_scores.md) — классические формулы для сравнения
- [Мониторинг модели: Джини, KS и PSI](score_monitoring.md) — метрики, на которых считается качество
- [Качество прибыли](earnings_quality.md) — проверка входных данных перед обучением
