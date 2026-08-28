# Скоринговая карта на весах доказательства

Биннинг признаков с расчётом WoE и информационной ценности, отбор переменных,
логистическая регрессия на весах и перевод коэффициентов в объяснимую шкалу баллов.

## Постановка задачи

Дано: выборка заявок с признаками $x_1,\dots,x_k$ и бинарным исходом
«дефолт / нет дефолта» на согласованном горизонте наблюдения.

Требуется: правило, которое по заявке выдаёт балл, монотонно связанный с
вероятностью дефолта, и при этом разбирается построчно — сколько баллов дал
каждый признак и почему.

Где встречается: розничное и МСБ-кредитование, лимиты рассрочки, отбор
контрагентов на постоплату, любые решения, которые нужно объяснить заявителю
и регулятору.

## Теория

Скоркарта строится в четыре шага.

**Биннинг.** Непрерывный признак режется на интервалы, и каждому присваивается
вес доказательства — логарифм отношения долей исправных и дефолтных заёмщиков:

$$
\mathrm{WoE}_i \;=\; \ln\frac{g_i / G}{b_i / B},
\qquad
\mathrm{IV} \;=\; \sum_i \Bigl(\frac{g_i}{G} - \frac{b_i}{B}\Bigr)\,\mathrm{WoE}_i .
$$

Здесь $g_i, b_i$ — число исправных и дефолтных в интервале, $G, B$ — их общее
число. Положительный вес означает, что интервал безопаснее среднего.

Замена значения признака на вес доказательства решает сразу три задачи:
монотонизирует связь с риском, устраняет влияние выбросов и переводит все
признаки в одну шкалу логарифма шансов. Интервалы объединяются, пока доля
дефолтов не станет монотонной, а каждый интервал не наберёт минимальную долю
наблюдений — иначе карта запомнит шум выборки.

**Отбор.** Признак берётся в модель, если его информационная ценность попадает
в коридор. Ниже 0,02 признак бесполезен; выше 0,5 он почти всегда означает
утечку целевой переменной — например, признак, который заполняется уже после
дефолта.

**Регрессия.** По весам доказательства строится логистическая регрессия. Все
коэффициенты при корректном биннинге должны быть положительными: отрицательный
коэффициент означает, что признак коллинеарен другому и модель переворачивает
его смысл.

**Шкала.** Логарифм шансов переводится в баллы двумя числами — базовым баллом
при известных шансах и числом баллов, удваивающим шансы:

$$
\text{factor} = \frac{\mathrm{PDO}}{\ln 2},
\qquad
\text{offset} = \text{baseScore} - \text{factor}\cdot\ln(\text{baseOdds}),
$$

$$
\text{points}_{ij} \;=\; -\Bigl(\beta_j\,\mathrm{WoE}_{ij} + \frac{\alpha}{k}\Bigr)\cdot \text{factor}
\;+\; \frac{\text{offset}}{k}.
$$

Баллы аддитивны, а шкала линейна в логарифме шансов, поэтому перевод балла
обратно в вероятность точен и обратим.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(k\,n\log n + I\,n\,k^2)$ | сортировка на биннинг, $I$ итераций IRLS |
| Память | $O(n\,k)$ | матрица весов доказательства |

## API

| Метод | Описание |
|-------|----------|
| `WoeBinning.Fit(variable, values, defaults, maxBins, minShare, enforceMonotonic)` | Биннинг одного признака |
| `WoeBinning.FitAll(names, values, defaults, maxBins, minShare)` | Все признаки, по убыванию IV |
| `VariableBinning.Transform(value)` | Значение признака в вес доказательства |
| `VariableBinning.InformationValue` / `IsMonotone` / `Predictiveness` | Оценка признака |
| `Scorecard.Fit(names, values, defaults, options)` | Отбор, регрессия и шкала баллов |
| `Scorecard.Score(applicant)` | Балл заявки |
| `Scorecard.ProbabilityOfDefault(score)` | Обратный перевод балла в вероятность |
| `ScorecardResult.Points` / `Rejected` / `Quality` / `ScoreRange` | Карта, отсев, качество, шкала |

Исходники: `src/AI.Economics/Credit/`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Credit;

var applications = new Matrix(1_200, 2);
var outcomes = new List<bool>(1_200);
var rng = new Random(7);

for (int i = 0; i < 1_200; i++)
{
    double income = 30_000 + (rng.NextDouble() * 170_000);
    double burden = 0.05 + (rng.NextDouble() * 0.70);

    applications[i, 0] = income;
    applications[i, 1] = burden;

    double logit = -1.4 - (1.8 * ((income - 100_000) / 100_000)) + (3.0 * burden);
    outcomes.Add(rng.NextDouble() < 1.0 / (1.0 + Math.Exp(-logit)));
}

string[] names = ["доход", "долговая нагрузка"];

var scorecard = new Scorecard();
ScorecardResult card = scorecard.Fit(names, applications, outcomes);

Console.WriteLine($"Джини {card.Quality.Gini:F3}, шкала {card.ScoreRange.Min:F0}-{card.ScoreRange.Max:F0}");

foreach (ScorecardPoint point in card.Points)
    Console.WriteLine($"{point.Variable} {point.Bin}: {point.Points:F1} балла (WoE {point.Woe:F2})");
```

Балл заявки и обратный перевод в вероятность:

```csharp
var applicant = new Dictionary<string, double>
{
    ["доход"] = 70_000,
    ["долговая нагрузка"] = 0.45,
};

double score = scorecard.Score(applicant);

Console.WriteLine($"Балл {score:F0} → вероятность дефолта {scorecard.ProbabilityOfDefault(score):P2}");
```

Отдельный признак можно изучить до построения карты — это основной инструмент
диалога с бизнесом о том, где проходит граница риска:

```csharp
var burdenColumn = new Vector(applications.Height);
for (int i = 0; i < applications.Height; i++) burdenColumn[i] = applications[i, 1];

VariableBinning binning = WoeBinning.Fit("долговая нагрузка", burdenColumn, outcomes);

Console.WriteLine($"IV = {binning.InformationValue:F3} ({binning.Predictiveness})");

foreach (ScoreBin bin in binning.Bins)
    Console.WriteLine($"{bin.Label}: доля дефолтов {bin.BadRate:P1}, WoE {bin.Woe:F3}");

Console.WriteLine(binning.Interpret().ToLlmText());
```

## Ограничения

- Метрики в `ScorecardResult.Quality` посчитаны на обучающей выборке и потому
  оптимистичны. Перед внедрением нужна проверка на отложенной выборке и на
  более позднем периоде — см. [мониторинг модели](score_monitoring.md).
- Границы интервалов фиксируются вместе с картой. Пересчёт биннинга на новых
  данных меняет смысл баллов и делает исторические отсечки несопоставимыми.
- Устойчивая карта требует нескольких сотен дефолтов. При меньшем числе
  интервалы приходится укрупнять, и разделяющая способность падает.
- Карта не различает причины отказа от обслуживания долга: если нужно понимать
  «кто и когда уйдёт», используйте [модель Кокса](cox_ph.md).
- Монотонизация интервалов — предпосылка, а не факт. Признаки с честно
  немонотонной связью (например, возраст) теряют часть силы; их лучше
  разбивать на категории вручную.

## См. также

- [Мониторинг модели: Джини, KS и PSI](score_monitoring.md) — проверка карты в эксплуатации
- [Резерв по МСФО 9](ifrs9.md) — куда подставляется вероятность дефолта
- [Скоринг контрагента](counterparty.md) — балльная модель без обучающей выборки
