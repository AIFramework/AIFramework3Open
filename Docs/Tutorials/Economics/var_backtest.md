# Бэктестирование VaR и стресс-тесты

Проверка модели риска по числу и по расположению пробоев: тесты Купца и
Кристофферсена, светофор Базеля, сценарный стресс-тест и обратный
стресс-тест.

## Постановка задачи

Дано: ряд фактических доходностей и ряд прогнозов VaR, выданных моделью на
каждый день; для стресс-теста — экспозиции по факторам риска и набор
сценариев шоков.

Требуется: ответ на вопрос, можно ли доверять модели, и оценка потерь в
заранее заданных и в наихудших мыслимых условиях.

Где встречается: ежегодная валидация моделей, надзорная отчётность,
обоснование мультипликатора капитала, подготовка к проверке регулятора,
внутренний риск-комитет.

## Теория

**Пробой** — день, в который фактический убыток превысил прогноз VaR. Если
модель верна, пробои образуют схему Бернулли с вероятностью $p = 1-\alpha$ и
происходят независимо друг от друга. Обе части проверяются отдельно.

**Тест Купца** (безусловное покрытие) проверяет только частоту:

$$
\mathrm{LR}_{uc} = -2\ln\frac{p^{x}(1-p)^{n-x}}{\hat p^{x}(1-\hat p)^{n-x}}
\;\sim\;\chi^2_1,\qquad \hat p = x/n .
$$

**Тест Кристофферсена** (независимость) проверяет, не идут ли пробои
подряд. По ряду строится матрица переходов «пробой — не пробой», и
сравниваются условные вероятности $\pi_{01}$ и $\pi_{11}$:

$$
\mathrm{LR}_{ind} = -2\ln\frac{(1-\pi)^{n_{00}+n_{10}}\pi^{n_{01}+n_{11}}}
{(1-\pi_{01})^{n_{00}}\pi_{01}^{n_{01}}(1-\pi_{11})^{n_{10}}\pi_{11}^{n_{11}}} \;\sim\;\chi^2_1 .
$$

**Условное покрытие** объединяет обе гипотезы:
$\mathrm{LR}_{cc} = \mathrm{LR}_{uc} + \mathrm{LR}_{ind} \sim \chi^2_2$.

Кластеризация пробоев опаснее их избытка: пять пробоев подряд означают, что
модель не реагирует на смену режима волатильности, и капитал закончится
именно тогда, когда он нужен.

**Светофор Базеля** переводит число пробоев за 250 дней в надбавку к
капиталу: до 4 — зелёная зона, 5–9 — жёлтая с растущим множителем, 10 и
более — красная, требующая пересмотра модели.

**Стресс-тест** считает потери при заданных шоках факторов:

$$
L_s \;=\; -\sum_j w_j\,\sigma_j\,\delta_{s,j},
$$

где $\delta_{s,j}$ — шок фактора $j$ в сценарии $s$ в стандартных
отклонениях. Сценарии берутся историческими (кризис 2008 года, март 2020-го)
или гипотетическими.

**Обратный стресс-тест** переворачивает вопрос: не «сколько потеряем при
таком шоке», а «какой шок уничтожит заданную долю капитала». Ответ — вектор
минимальной нормы, дающий целевой убыток; он показывает, по какому фактору
портфель уязвимее всего.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(n)$ на бэктест, $O(SF)$ на стресс-тест | $S$ сценариев, $F$ факторов |
| Память | $O(n + SF)$ | ряды и матрица сценариев |

## API

| Метод | Описание |
|-------|----------|
| `VarBacktesting.Backtest(returns, forecasts, confidence, model)` | Все три теста и светофор |
| `VarBacktesting.StressTest(exposures, volatility, scenarios, factors, var, target, portfolio)` | Сценарии и обратный тест |
| `BacktestVarResult.KupiecPValue` и соседние | Значимость каждого теста |
| `BacktestVarResult.TrafficLight` / `IsAccepted` | Зона Базеля и итоговый вердикт |
| `BacktestVarResult.LongestExceptionRun` | Максимальная серия пробоев подряд |
| `StressTestResult.ReverseStressShocks` | Шоки, приводящие к целевому убытку |

Исходники: `src/AI.Economics/Risk/VarBacktesting.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Risk;
using AI.Statistics;

Random engine = RandomEngine.Create(13);
var actual = new Vector(750);
var forecasts = new Vector(750);
double volatility = 0.012;

for (int i = 0; i < actual.Count; i++)
{
    // Волатильность кластеризуется — именно это и должна ловить модель
    volatility = (0.94 * volatility) + (0.06 * 0.012);
    if (engine.NextDouble() < 0.02) volatility *= 2.5;

    actual[i] = RandomEngine.NextGaussian(engine, 0.0004, volatility);
    forecasts[i] = 2.326 * volatility;
}

BacktestVarResult backtest = VarBacktesting.Backtest(
    actual, forecasts, confidence: 0.99, model: "GARCH-VaR");

Console.WriteLine($"Пробоев {backtest.Exceptions} при ожидаемых {backtest.ExpectedExceptions:F1}");
Console.WriteLine($"Купец: {backtest.KupiecStatistic:F2} (p = {backtest.KupiecPValue:F4})");
Console.WriteLine($"Независимость: {backtest.IndependenceStatistic:F2} " +
                  $"(p = {backtest.IndependencePValue:F4})");
Console.WriteLine($"Условное покрытие: p = {backtest.ConditionalCoveragePValue:F4}");
Console.WriteLine($"Зона {backtest.TrafficLight}, максимальная серия " +
                  $"{backtest.LongestExceptionRun}");
Console.WriteLine(backtest.IsAccepted ? "Модель принимается" : "Модель отвергается");
```

Стресс-тест по историческим сценариям — обязательная часть отчёта:

```csharp
var exposures = new Vector(1_500_000_000, 900_000_000, 600_000_000);
var factorVolatility = new Vector(0.018, 0.010, 0.025);

var scenarios = new List<(string Name, Vector Shocks)>
{
    ("Кризис 2008", new Vector(-6.0, -2.5, -7.0)),
    ("Март 2020", new Vector(-5.0, -1.5, -8.0)),
    ("Ставочный шок", new Vector(-2.0, -5.0, -1.0)),
};

StressTestResult stress = VarBacktesting.StressTest(
    exposures, factorVolatility, scenarios,
    factors: ["Акции", "Облигации", "Сырьё"],
    valueAtRisk: 180_000_000,
    reverseTarget: 0.15,
    portfolio: "Инвестиционный портфель");

foreach (StressScenario scenario in stress.Scenarios.OrderByDescending(s => s.Loss))
    Console.WriteLine($"{scenario.Name}: потери {scenario.Loss:N0} ({scenario.LossShare:P1})");

Console.WriteLine($"Худший сценарий {stress.WorstLoss:N0} при VaR {stress.ValueAtRisk:N0}");
```

Обратный стресс-тест указывает на самое слабое место портфеля:

```csharp
for (int j = 0; j < stress.Factors.Count; j++)
    Console.WriteLine($"{stress.Factors[j]}: шок {stress.ReverseStressShocks[j]:F2} σ");

Console.WriteLine($"Расстояние до целевого убытка {stress.ReverseStressDistance:F2} σ");
Console.WriteLine(backtest.Interpret().ToLlmText());
```

## Ограничения

- Мощность тестов низка. На 250 наблюдениях тест Купца при уровне 99% не
  отличает модель с истинным покрытием 98% от корректной — для надёжного
  вывода нужны два-три года данных.
- Тест независимости в форме Кристофферсена ловит только зависимость первого
  порядка. Пробои, разделённые двумя-тремя днями, он не замечает.
- Светофор Базеля рассчитан на уровень 99% и горизонт один день. Применять
  его пороги к другим параметрам нельзя.
- Стресс-сценарии линейны по шокам. Портфель с опционами ведёт себя нелинейно,
  и линейная аппроксимация занижает потери при больших шоках.
- Обратный стресс-тест даёт математически минимальный шок, а не самый
  правдоподобный. Его результат — повод задать вопрос «может ли такое
  случиться», а не готовый сценарий.

## См. также

- [Value at Risk и Expected Shortfall](value_at_risk.md) — тестируемая модель
- [Теория экстремальных значений](extreme_value.md) — альтернативная оценка хвоста
- [Условная волатильность GARCH](garch.md) — источник динамического прогноза VaR
