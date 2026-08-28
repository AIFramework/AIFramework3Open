# Модели ограниченного отклика

Логит и пробит для бинарного исхода, тобит для цензурированных данных,
Пуассон и отрицательная биномиальная для счётных — с предельными эффектами и
оценкой методом максимального правдоподобия.

## Постановка задачи

Дано: отклик, который не может быть произвольным числом, — «купил или нет»,
«сумма расхода, но у многих ноль», «число обращений в поддержку».

Требуется: оценка влияния регрессоров, корректная для такого отклика, и
предельные эффекты в единицах, понятных бизнесу.

Где встречается: скоринг, отклик на рекламную кампанию, спрос при
ограничении по складу, частота страховых случаев, число отказов оборудования.

## Теория

**Почему не МНК.** Линейная вероятностная модель предсказывает вероятности вне
$[0,1]$, гетероскедастична по построению и даёт неверные предельные эффекты
на краях. Для цензурированного отклика МНК смещён вниз, поскольку ноль
трактуется как настоящее наблюдение.

**Логит и пробит.** Наблюдаемый бинарный отклик порождается латентной
переменной $y^{*} = x^{\top}\beta + \varepsilon$; наблюдается $y = 1$ при
$y^{*} > 0$:

$$
\Pr(y=1\mid x) = \Lambda(x^{\top}\beta) = \frac{1}{1+e^{-x^{\top}\beta}}
\qquad\text{или}\qquad
\Phi(x^{\top}\beta).
$$

Модели различаются хвостами: логистическое распределение толще нормального,
поэтому коэффициенты логита примерно в 1,6 раза больше пробитовских. На
предсказанных вероятностях разница почти незаметна.

**Предельные эффекты** — то, что интересует бизнес. Коэффициент логита
измеряется в логарифмах отношения шансов; средний предельный эффект переводит
его в проценты вероятности:

$$
\overline{\mathrm{ME}}_j \;=\; \frac{1}{n}\sum_i \lambda(x_i^{\top}\beta)\,\beta_j .
$$

**Тобит** описывает цензурирование: отклик наблюдается только выше порога,
а ниже фиксируется значение порога. Правдоподобие объединяет плотность для
незацензурированных и вероятность для остальных:

$$
\ln L = \sum_{y_i > c}\ln\frac{1}{\sigma}\varphi\!\left(\frac{y_i - x_i^{\top}\beta}{\sigma}\right)
+ \sum_{y_i = c}\ln\Phi\!\left(\frac{c - x_i^{\top}\beta}{\sigma}\right).
$$

**Пуассон** для счётных данных: $\mathbb E[y\mid x] = \exp(x^{\top}\beta)$.
Его ограничение — равенство среднего и дисперсии, которое почти никогда не
выполняется. **Отрицательная биномиальная** добавляет параметр сверхдисперсии
$\alpha$: $\mathrm{Var}(y) = \mu + \alpha\mu^2$. Значимое $\alpha$ означает,
что Пуассон неадекватен и его стандартные ошибки занижены.

**Псевдо-$R^2$ Макфаддена** $1 - \ln L/\ln L_0$ — не доля объяснённой
дисперсии; значения 0,2–0,4 соответствуют отличной подгонке.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(Ink^2 + k^3)$ | $I$ итераций Ньютона или IRLS |
| Память | $O(nk + k^2)$ | матрица плана и гессиан |

## API

| Метод | Описание |
|-------|----------|
| `LimitedDependent.Fit(x, y, model, names, censorPoint)` | Оценка максимального правдоподобия |
| `LimitedDependentModel` | `Logit`, `Probit`, `Tobit`, `Poisson`, `NegativeBinomial` |
| `LimitedDependentResult.MarginalEffects` | Средние предельные эффекты |
| `LimitedDependentResult.McFaddenRSquared` | Псевдокоэффициент детерминации |
| `LimitedDependentResult.ScaleParameter` | $\sigma$ тобита или $\alpha$ сверхдисперсии |
| `LimitedDependentResult.Accuracy` / `CensoredShare` | Точность и доля цензурированных |

Исходники: `src/AI.Economics/Econometrics/LimitedDependent.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(127);
int n = 3000;

var features = new Matrix(n, 2);
var binary = new Vector(n);

for (int i = 0; i < n; i++)
{
    double income = RandomEngine.NextGaussian(rng);
    double tenure = RandomEngine.NextGaussian(rng);

    features[i, 0] = income;
    features[i, 1] = tenure;

    double index = -0.4 + (1.2 * income) + (0.6 * tenure);
    binary[i] = rng.NextDouble() < 1.0 / (1.0 + Math.Exp(-index)) ? 1 : 0;
}

LimitedDependentResult logit = LimitedDependent.Fit(
    features, binary, LimitedDependentModel.Logit, ["доход", "стаж"]);

foreach (Coefficient coefficient in logit.Coefficients)
{
    Console.WriteLine($"{coefficient.Name}: {coefficient.Estimate:F4} " +
                      $"(p = {coefficient.PValue:F4}){coefficient.Stars}");
}

foreach ((string variable, double effect) in logit.MarginalEffects)
    Console.WriteLine($"Предельный эффект {variable}: {effect:P2} вероятности");

Console.WriteLine($"Псевдо-R² {logit.McFaddenRSquared:F4}, точность {logit.Accuracy:P1}");
```

Тобит нужен, когда часть наблюдений «упирается» в границу:

```csharp
var spending = new Vector(n);
for (int i = 0; i < n; i++)
{
    double latent = 0.3 + (1.2 * features[i, 0]) + (0.6 * features[i, 1])
        + RandomEngine.NextGaussian(rng);
    spending[i] = Math.Max(latent, 0);
}

LimitedDependentResult tobit = LimitedDependent.Fit(
    features, spending, LimitedDependentModel.Tobit, ["доход", "стаж"]);

Console.WriteLine($"Тобит: коэффициент при доходе {tobit.Coefficients[1].Estimate:F4} " +
                  $"(истинный 1,2)");
Console.WriteLine($"Цензурировано {tobit.CensoredShare:P1}, σ = {tobit.ScaleParameter:F3}");
```

Для счётного отклика выбор между Пуассоном и отрицательной биномиальной
решает параметр сверхдисперсии:

```csharp
var counts = new Vector(n);
for (int i = 0; i < n; i++)
{
    double mean = Math.Exp(0.5 + (0.4 * features[i, 0]));
    double heterogeneity = RandomEngine.NextGamma(rng, 2.0, mean / 2.0);
    counts[i] = RandomEngine.NextPoisson(rng, Math.Max(heterogeneity, 1e-9));
}

LimitedDependentResult poisson = LimitedDependent.Fit(
    features, counts, LimitedDependentModel.Poisson, ["доход", "стаж"]);
LimitedDependentResult negBin = LimitedDependent.Fit(
    features, counts, LimitedDependentModel.NegativeBinomial, ["доход", "стаж"]);

Console.WriteLine($"Пуассон: AIC {poisson.Aic:F1}, ошибка {poisson.Coefficients[1].StandardError:F4}");
Console.WriteLine($"Отрицательная биномиальная: AIC {negBin.Aic:F1}, " +
                  $"ошибка {negBin.Coefficients[1].StandardError:F4}, " +
                  $"сверхдисперсия {negBin.ScaleParameter:F3}");

Console.WriteLine(logit.Interpret().ToLlmText());
```

## Ограничения

- Коэффициенты нелинейных моделей не интерпретируются напрямую. Всегда
  показывайте предельные эффекты — коэффициент логита 1,2 ничего не говорит
  бизнесу, а «+18 процентных пунктов вероятности» говорит.
- Тобит требует, чтобы один и тот же механизм определял и участие, и объём.
  Если решение «покупать ли» и «сколько» принимаются по-разному, нужна
  двухчастная модель или модель Хекмана.
- Пуассон с заниженными ошибками — самая частая ошибка в работе со счётными
  данными. Всегда проверяйте сверхдисперсию.
- Максимальное правдоподобие не сходится при полном разделении выборки
  (регрессор идеально предсказывает исход). Симптом — огромные коэффициенты и
  ошибки.
- Псевдо-$R^2$ несопоставим с обычным $R^2$ и между моделями разного типа.
  Для сравнения спецификаций используйте AIC и BIC.

## См. также

- [Скоркарта](scorecard.md) — прикладное применение логита
- [Квантильная регрессия](quantile_regression.md) — ещё один отказ от условного среднего
- [Регрессия с устойчивыми ошибками](regression_robust.md) — линейный случай
