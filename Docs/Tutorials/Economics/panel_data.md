# Панельные данные: фиксированные и случайные эффекты

Оценка на данных «объект × период»: объединённый МНК, внутригрупповая
регрессия, первые разности, случайные эффекты и тест Хаусмана, выбирающий
между ними.

## Постановка задачи

Дано: наблюдения по нескольким объектам (фирмам, регионам, магазинам) за
несколько периодов, регрессоры и отклик.

Требуется: оценка эффекта регрессора, очищенная от ненаблюдаемых
характеристик объектов — качества менеджмента, географии, отраслевой
специфики.

Где встречается: оценка отдачи от инвестиций по филиалам, влияние политики на
регионы, эффект программы лояльности по магазинам, производственные функции.

## Теория

**Модель с индивидуальными эффектами:**

$$
y_{it} \;=\; x_{it}^{\top}\beta + u_i + \varepsilon_{it},
$$

где $u_i$ — ненаблюдаемая постоянная характеристика объекта. Если $u_i$
коррелирует с регрессорами, объединённый МНК смещён: он приписывает эффекту
регрессора всё влияние скрытого качества объекта.

**Фиксированные эффекты** (внутригрупповая оценка) устраняют $u_i$
вычитанием средних по объекту:

$$
y_{it} - \bar y_i \;=\; (x_{it} - \bar x_i)^{\top}\beta + (\varepsilon_{it} - \bar\varepsilon_i).
$$

Приём работает при **любой** корреляции $u_i$ с регрессорами — в этом его
сила. Цена — невозможность оценить коэффициенты при неизменных во времени
переменных (пол, отрасль, регион): они исчезают вместе с эффектом.

**Первые разности** достигают того же вычитанием предыдущего наблюдения. При
$T = 2$ метод эквивалентен внутригрупповому; при $T > 2$ он эффективнее, если
ошибки — случайное блуждание, и менее эффективен, если они независимы.

**Двусторонние эффекты** дополнительно убирают общие для всех объектов
шоки периодов — кризис, сезон, изменение налога.

**Случайные эффекты** предполагают независимость $u_i$ от регрессоров и
оценивают модель обобщённым МНК на квази-разностях:

$$
y_{it} - \theta\bar y_i,
\qquad
\theta = 1 - \sqrt{\frac{\sigma_\varepsilon^2}{T\sigma_u^2 + \sigma_\varepsilon^2}} .
$$

При $\theta = 0$ получается объединённый МНК, при $\theta = 1$ —
внутригрупповая оценка. Метод эффективнее фиксированных эффектов и сохраняет
неизменные переменные — но ценой сильного допущения.

**Тест Хаусмана** сравнивает две оценки:

$$
H = (\hat\beta_{FE} - \hat\beta_{RE})^{\top}
\left[\mathrm{Var}(\hat\beta_{FE}) - \mathrm{Var}(\hat\beta_{RE})\right]^{-1}
(\hat\beta_{FE} - \hat\beta_{RE}) \sim \chi^2_k .
$$

Отклонение означает, что случайные эффекты несостоятельны и надо брать
фиксированные.

**Доля дисперсии эффектов** $\rho = \sigma_u^2/(\sigma_u^2 +
\sigma_\varepsilon^2)$ показывает, насколько вообще важна панельная структура.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(NTk^2 + k^3)$ | $N$ объектов, $T$ периодов |
| Память | $O(NTk)$ | преобразованная матрица плана |

## API

| Метод | Описание |
|-------|----------|
| `PanelData.Fit(dataset, estimator)` | Оценка выбранным методом |
| `PanelData.Hausman(fixed, random)` | Выбор между спецификациями |
| `PanelDataset` | Регрессоры, отклик, номера объектов и периодов |
| `PanelResult.Rho` | Доля дисперсии, приходящаяся на эффекты |
| `PanelResult.Theta` | Коэффициент квази-разности для случайных эффектов |
| `PanelEstimator` | `Pooled`, `FixedEffects`, `TwoWayFixedEffects`, `RandomEffects`, `FirstDifference`, `Between` |

Исходники: `src/AI.Economics/Econometrics/PanelData.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(109);
int units = 80, periods = 10;
int n = units * periods;

var regressors = new Matrix(n, 1);
var response = new Vector(n);
var unitIds = new List<int>(n);
var periodIds = new List<int>(n);

for (int u = 0; u < units; u++)
{
    double quality = RandomEngine.NextGaussian(rng, 0, 2); // ненаблюдаемое качество

    for (int t = 0; t < periods; t++)
    {
        int i = (u * periods) + t;

        // Инвестиции коррелируют с качеством — источник смещения объединённого МНК
        double investment = (0.8 * quality) + RandomEngine.NextGaussian(rng);

        regressors[i, 0] = investment;
        response[i] = investment + quality + RandomEngine.NextGaussian(rng, 0, 0.5);

        unitIds.Add(u);
        periodIds.Add(t);
    }
}

var panel = new PanelDataset
{
    Regressors = regressors,
    Response = response,
    Units = unitIds,
    Periods = periodIds,
    Names = ["инвестиции"],
};

PanelResult within = PanelData.Fit(panel, PanelEstimator.FixedEffects);
PanelResult pooled = PanelData.Fit(panel, PanelEstimator.Pooled);

Console.WriteLine($"Истинный коэффициент 1");
Console.WriteLine($"Объединённый МНК: {pooled.Coefficients[^1].Estimate:F4} — смещён");
Console.WriteLine($"Фиксированные эффекты: {within.Coefficients[0].Estimate:F4}");
Console.WriteLine($"Доля дисперсии эффектов {within.Rho:P1}");
```

Тест Хаусмана решает, какую спецификацию защищать перед рецензентом:

```csharp
PanelResult random = PanelData.Fit(panel, PanelEstimator.RandomEffects);
HausmanResult hausman = PanelData.Hausman(within, random);

foreach ((string variable, double fe, double re, double diff) in hausman.Differences)
    Console.WriteLine($"{variable}: FE {fe:F4}, RE {re:F4}, разность {diff:F4}");

Console.WriteLine($"Хаусман {hausman.Statistic:F3}, p = {hausman.PValue:F5}");
Console.WriteLine(hausman.PrefersFixedEffects
    ? "Берём фиксированные эффекты"
    : "Случайные эффекты допустимы и эффективнее");
```

Сравнение всех оценщиков полезно как проверка устойчивости:

```csharp
foreach (PanelEstimator estimator in Enum.GetValues<PanelEstimator>())
{
    PanelResult item = PanelData.Fit(panel, estimator);
    Coefficient? slope = item.Coefficients.FirstOrDefault(c => c.Name == "инвестиции");

    Console.WriteLine($"{estimator}: {slope?.Estimate:F4} " +
                      $"(ошибка {slope?.StandardError:F4}), R² {item.RSquared:F3}");
}

Console.WriteLine(within.Interpret().ToLlmText());
```

## Ограничения

- Фиксированные эффекты не спасают от **изменяющихся во времени**
  ненаблюдаемых факторов. Если качество менеджмента меняется вместе с
  инвестициями, смещение остаётся.
- Внутригрупповая оценка использует только вариацию внутри объектов. При
  малой изменчивости регрессора во времени она крайне неточна, и доверительные
  интервалы становятся бесполезно широкими.
- Стандартные ошибки должны быть кластеризованы по объектам — иначе они
  занижены в разы. Используйте
  [кластерные ошибки](regression_robust.md) явным образом.
- Тест Хаусмана может дать отрицательную статистику при малых выборках: это
  признак того, что разность ковариаций не положительно определена, а не
  доказательство в пользу случайных эффектов.
- При наличии лага отклика среди регрессоров внутригрупповая оценка смещена
  (смещение Никелла) — нужны [динамические панели](dynamic_panel.md).

## См. также

- [Динамические панели](dynamic_panel.md) — модель с лагом отклика
- [Разность разностей](causal_did.md) — панельная оценка эффекта политики
- [Регрессия с устойчивыми ошибками](regression_robust.md) — кластерные ошибки
