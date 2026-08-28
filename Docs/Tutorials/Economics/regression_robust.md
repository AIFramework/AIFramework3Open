# Регрессия с устойчивыми стандартными ошибками

МНК с поправками HC0–HC3 на гетероскедастичность, Ньюи — Уэста на
автокорреляцию и кластерными ошибками — когда коэффициенты верны, а
классические ошибки лгут.

## Постановка задачи

Дано: матрица регрессоров и отклик; данные могут быть гетероскедастичны,
автокоррелированы или сгруппированы (по регионам, фирмам, магазинам).

Требуется: оценки коэффициентов и — главное — честные стандартные ошибки, по
которым можно строить доверительные интервалы и делать выводы о значимости.

Где встречается: оценка эластичности спроса, регрессия на панельных данных,
анализ эксперимента с групповой рандомизацией, любая эконометрическая работа,
идущая на публикацию или в суд.

## Теория

**Оценка МНК** $\hat\beta = (X^{\top}X)^{-1}X^{\top}y$ остаётся несмещённой
при гетероскедастичности и автокорреляции — ломается не она, а формула
дисперсии. Классическая оценка $\hat\sigma^2 (X^{\top}X)^{-1}$ верна только
при сферических ошибках.

**Сэндвич-оценка** заменяет её общей конструкцией:

$$
\widehat{\mathrm{Var}}(\hat\beta) \;=\; (X^{\top}X)^{-1}\,\hat\Omega\,(X^{\top}X)^{-1},
$$

где «начинка» $\hat\Omega$ зависит от предполагаемого вида нарушения.

**Гетероскедастичность** (Уайт, Эйкер): $\hat\Omega = \sum_i \omega_i
e_i^2 x_i x_i^{\top}$. Варианты различаются поправкой на рычаг
$h_i = x_i^{\top}(X^{\top}X)^{-1}x_i$:

| Вариант | Множитель $\omega_i$ | Когда применять |
|---------|----------------------|-----------------|
| HC0 | $1$ | большие выборки |
| HC1 | $n/(n-k)$ | базовый выбор |
| HC2 | $1/(1-h_i)$ | есть влиятельные наблюдения |
| HC3 | $1/(1-h_i)^2$ | малые выборки, рекомендуется по умолчанию |

HC3 наиболее консервативен и в симуляциях лучше всех держит номинальный
уровень при $n < 250$.

**Автокорреляция** (Ньюи — Уэст): к диагонали добавляются взвешенные
автоковариации с треугольными весами Бартлетта:

$$
\hat\Omega = \sum_i e_i^2 x_i x_i^{\top}
+ \sum_{l=1}^{L}\left(1 - \frac{l}{L+1}\right)\sum_{i>l}
e_i e_{i-l}\left(x_i x_{i-l}^{\top} + x_{i-l}x_i^{\top}\right).
$$

Веса Бартлетта гарантируют положительную определённость; число лагов обычно
берут $L \approx 4(n/100)^{2/9}$.

**Кластеры.** Когда наблюдения сгруппированы, ошибки коррелированы внутри
групп произвольно. Начинка суммируется по группам:
$\hat\Omega = \sum_g X_g^{\top}e_g e_g^{\top}X_g$ с поправкой
$\frac{G}{G-1}\cdot\frac{n-1}{n-k}$. Игнорирование кластеров — самая
распространённая причина ложных открытий в прикладной экономике: истинные
ошибки бывают втрое больше классических.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(nk^2 + k^3)$; $O(Lnk^2)$ для Ньюи — Уэста | $k$ регрессоров |
| Память | $O(nk + k^2)$ | матрица плана и ковариация |

## API

| Метод | Описание |
|-------|----------|
| `LinearRegression.Fit(x, y, names, options)` | Оценка с выбранным типом ошибок |
| `RegressionOptions.Variance` | `Classical`, `Hc0`–`Hc3`, `NeweyWest`, `Clustered` |
| `RegressionOptions.Clusters` / `Lags` / `Weights` | Группы, лаги, веса для ВНК |
| `RegressionResult.Coefficients` | Оценка, ошибка, t, p, интервал, звёздочки |
| `RegressionResult.FStatistic` / `RSquared` / `Aic` | Качество модели |
| `RegressionResult.CovarianceMatrix` | Ковариация для линейных тестов |

Исходники: `src/AI.Economics/Econometrics/LinearRegression.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(101);
int n = 600;

var x = new Matrix(n, 2);
var y = new Vector(n);
var clusters = new List<int>(n);

for (int i = 0; i < n; i++)
{
    double a = RandomEngine.NextGaussian(rng);
    double b = RandomEngine.NextGaussian(rng);

    x[i, 0] = a;
    x[i, 1] = b;
    clusters.Add(i % 30);

    // Разброс ошибки растёт с первым регрессором
    y[i] = 1 + (2 * a) - (0.5 * b) + RandomEngine.NextGaussian(rng, 0, 0.4 * Math.Exp(0.6 * a));
}

RegressionResult robust = LinearRegression.Fit(x, y, ["a", "b"],
    new RegressionOptions { Variance = RobustVariance.Hc3 });

foreach (Coefficient coefficient in robust.Coefficients)
{
    Console.WriteLine($"{coefficient.Name}: {coefficient.Estimate:F4} " +
                      $"± {coefficient.StandardError:F4} " +
                      $"(t = {coefficient.TStatistic:F2}, p = {coefficient.PValue:F4}){coefficient.Stars}");
}

Console.WriteLine($"R² {robust.RSquared:F4}, F = {robust.FStatistic:F2} (p = {robust.FPValue:F5})");
```

Оценки не меняются, меняются только ошибки — это и есть суть поправок:

```csharp
foreach (RobustVariance kind in Enum.GetValues<RobustVariance>())
{
    RegressionResult variant = LinearRegression.Fit(x, y, ["a", "b"], new RegressionOptions
    {
        Variance = kind,
        Clusters = kind == RobustVariance.Clustered ? clusters : null,
    });

    Coefficient slope = variant.Coefficients[1];
    Console.WriteLine($"{kind}: оценка {slope.Estimate:F4}, ошибка {slope.StandardError:F4}, " +
                      $"p = {slope.PValue:F4}");
}
```

Взвешенный МНК применяется, когда вид гетероскедастичности известен:

```csharp
var weights = new List<double>(n);
for (int i = 0; i < n; i++) weights.Add(1.0 / Math.Exp(0.6 * x[i, 0]));

RegressionResult weighted = LinearRegression.Fit(x, y, ["a", "b"],
    new RegressionOptions { Weights = weights, Variance = RobustVariance.Hc1 });

Console.WriteLine($"ВНК: коэффициент при a {weighted.Coefficients[1].Estimate:F4}");
Console.WriteLine(robust.Interpret().ToLlmText());
```

## Ограничения

- Устойчивые ошибки асимптотические. При числе кластеров меньше 30–40 они
  занижены, и нужен бутстрап по кластерам (wild cluster bootstrap).
- Поправки не лечат смещение от пропущенных переменных или эндогенности.
  Устойчивая ошибка при смещённой оценке лишь придаёт уверенности в неверном
  числе — для эндогенности нужны [инструменты](iv_2sls.md).
- HC3 может быть слишком консервативен при $n > 1000$: там HC1 точнее и
  экономичнее.
- Число лагов у Ньюи — Уэста задаётся исследователем и заметно влияет на
  результат. Слишком много лагов раздувает ошибку, слишком мало — не убирает
  автокорреляцию.
- Кластеризацию надо выбирать по уровню назначения воздействия, а не по
  удобству. Если политика вводилась по регионам, кластеры — регионы, даже
  если наблюдения по фирмам.

## См. также

- [Диагностика регрессии](regression_diagnostics.md) — тесты, определяющие нужный тип ошибок
- [Инструментальные переменные](iv_2sls.md) — лечение эндогенности
- [Панельные данные](panel_data.md) — регрессия с индивидуальными эффектами
