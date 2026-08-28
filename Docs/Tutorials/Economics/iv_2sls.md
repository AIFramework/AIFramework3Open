# Инструментальные переменные: 2SLS и GMM

Оценка причинного эффекта при эндогенности регрессора: двухшаговый МНК,
обобщённый метод моментов, диагностика слабых инструментов и тесты Хаусмана и
Саргана.

## Постановка задачи

Дано: отклик, эндогенный регрессор (цена, реклама, образование), при
необходимости экзогенные контроли и хотя бы один инструмент — переменная,
влияющая на регрессор, но не на отклик напрямую.

Требуется: несмещённая оценка коэффициента при эндогенном регрессоре и
доказательство, что инструменты пригодны.

Где встречается: оценка эластичности спроса по цене, эффект рекламы на
продажи, отдача от образования, влияние регулирования на выпуск — всюду, где
регрессор определяется одновременно с откликом.

## Теория

**Проблема.** Если регрессор коррелирован с ошибкой, МНК смещён и
несостоятелен. Классический пример — эластичность спроса: цена определяется
пересечением спроса и предложения, поэтому наблюдаемая связь цены и
количества смешивает обе кривые.

**Условие исключения.** Инструмент $Z$ должен удовлетворять двум требованиям:
релевантность $\mathrm{Cov}(Z, X) \ne 0$ (проверяема) и экзогенность
$\mathrm{Cov}(Z, \varepsilon) = 0$ (в точно идентифицированном случае
непроверяема — это содержательное допущение).

**Двухшаговый МНК.** Первая ступень — проекция эндогенного регрессора на
инструменты и контроли; вторая — регрессия отклика на расчётные значения:

$$
\hat X = Z(Z^{\top}Z)^{-1}Z^{\top}X,
\qquad
\hat\beta_{\mathrm{IV}} = (\hat X^{\top}X)^{-1}\hat X^{\top}y .
$$

Важная тонкость: остатки для расчёта дисперсии считаются по **фактическим**, а
не по расчётным регрессорам. Пошаговый расчёт «руками» через две регрессии
даёт неверные стандартные ошибки именно из-за этого.

**Слабые инструменты.** Если связь инструмента с регрессором слаба, оценка
смещается к МНК и распределение перестаёт быть нормальным даже в больших
выборках. Правило Стайгера — Стока: F-статистика первой ступени должна
превышать 10.

**Тест Хаусмана** проверяет, нужны ли инструменты вообще:

$$
H \;=\; \frac{(\hat\beta_{\mathrm{IV}} - \hat\beta_{\mathrm{OLS}})^2}
{\mathrm{Var}(\hat\beta_{\mathrm{IV}}) - \mathrm{Var}(\hat\beta_{\mathrm{OLS}})} \sim \chi^2_1 .
$$

Значимая разница подтверждает эндогенность; при её отсутствии МНК
эффективнее.

**Тест Саргана** проверяет сверхидентифицирующие ограничения:
$nR^2$ из регрессии остатков IV на все инструменты. Отклонение означает, что
хотя бы один инструмент не экзогенен. Тест работает, только когда инструментов
больше, чем эндогенных регрессоров.

**Обобщённый метод моментов** обобщает 2SLS, взвешивая моментные условия
оптимальной матрицей — она эффективнее при гетероскедастичности:

$$
\hat\beta_{\mathrm{GMM}} = \left(X^{\top}ZWZ^{\top}X\right)^{-1}X^{\top}ZWZ^{\top}y,
\qquad W = \hat S^{-1}.
$$

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(nm^2 + m^3)$ | $m$ — число инструментов и контролей |
| Память | $O(nm)$ | матрицы плана и инструментов |

## API

| Метод | Описание |
|-------|----------|
| `InstrumentalVariables.TwoStage(endog, exog, instruments, y, names...)` | Двухшаговый МНК |
| `InstrumentalVariables.GeneralizedMethodOfMoments(...)` | GMM с оптимальным весом |
| `IvResult.Coefficients` / `OrdinaryLeastSquares` | Инструментальные и МНК-оценки рядом |
| `IvResult.FirstStages` | F-статистика и частный R² по каждому регрессору |
| `IvResult.HausmanStatistic` / `HausmanPValue` | Подтверждение эндогенности |
| `IvResult.OveridentificationStatistic` | Тест Саргана — Хансена |

Исходники: `src/AI.Economics/Econometrics/InstrumentalVariables.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(107);
int n = 3000;

var price = new Matrix(n, 1);
var instruments = new Matrix(n, 2);
var quantity = new Vector(n);

for (int i = 0; i < n; i++)
{
    double demandShock = RandomEngine.NextGaussian(rng);

    // Инструменты — сдвиги предложения: влияют на цену, но не на спрос
    double costShock = RandomEngine.NextGaussian(rng);
    double logistics = RandomEngine.NextGaussian(rng);

    instruments[i, 0] = costShock;
    instruments[i, 1] = logistics;

    double p = demandShock + (0.7 * costShock) + (0.5 * logistics)
        + RandomEngine.NextGaussian(rng, 0, 0.3);
    price[i, 0] = p;

    // Истинная эластичность -2, шок спроса поднимает и цену, и количество
    quantity[i] = 1 - (2 * p) + (1.5 * demandShock) + RandomEngine.NextGaussian(rng, 0, 0.3);
}

IvResult iv = InstrumentalVariables.TwoStage(
    price, exogenous: null, instruments, quantity, endogenousNames: ["цена"]);

Coefficient elasticity = iv.Coefficients.First(c => c.Name == "цена");
Coefficient naive = iv.OrdinaryLeastSquares.First(c => c.Name == "цена");

Console.WriteLine($"Истинная эластичность -2");
Console.WriteLine($"МНК: {naive.Estimate:F4} — смещена вверх шоком спроса");
Console.WriteLine($"Инструментальная: {elasticity.Estimate:F4} ± {elasticity.StandardError:F4}");
```

Без диагностики инструментов результат предъявлять нельзя:

```csharp
foreach (FirstStage stage in iv.FirstStages)
{
    Console.WriteLine($"{stage.Variable}: F = {stage.FStatistic:F1}, " +
                      $"частный R² {stage.PartialRSquared:F3}" +
                      (stage.IsWeak ? " — инструменты слабые!" : " — инструменты сильные"));
}

Console.WriteLine($"Хаусман: {iv.HausmanStatistic:F3}, p = {iv.HausmanPValue:F5} — " +
                  (iv.HausmanPValue < 0.05 ? "эндогенность подтверждена" : "МНК допустим"));

Console.WriteLine($"Сарган: {iv.OveridentificationStatistic:F3}, " +
                  $"p = {iv.OveridentificationPValue:F4} " +
                  $"({iv.OveridentifyingRestrictions} ограничений)");
```

При гетероскедастичности GMM эффективнее двухшагового МНК:

```csharp
IvResult gmm = InstrumentalVariables.GeneralizedMethodOfMoments(
    price, null, instruments, quantity, ["цена"]);

Console.WriteLine($"GMM: {gmm.Coefficients.First(c => c.Name == "цена").Estimate:F4}, " +
                  $"ошибка {gmm.Coefficients.First(c => c.Name == "цена").StandardError:F4}");

Console.WriteLine(iv.Interpret().ToLlmText());
```

## Ограничения

- Экзогенность инструмента в точно идентифицированном случае недоказуема.
  Это содержательное допущение, которое обосновывается экономической логикой,
  а не статистикой.
- Слабые инструменты хуже отсутствия инструментов: оценка смещена в сторону
  МНК, а доверительные интервалы обманчиво узки. При F ниже 10 результат
  нельзя интерпретировать.
- Оценка IV менее эффективна, чем МНК. Если эндогенности нет (тест Хаусмана
  не отвергается), инструменты только раздувают дисперсию.
- При гетерогенных эффектах IV оценивает не средний эффект, а локальный —
  для тех наблюдений, чьё поведение изменил инструмент. Обобщать его на всю
  популяцию нельзя.
- Тест Саргана слабо мощен: он часто не отвергает даже при нарушении
  экзогенности. Непрохождение — плохой знак, прохождение — не доказательство.

## См. также

- [Регрессия с устойчивыми ошибками](regression_robust.md) — базовая оценка
- [Динамические панели](dynamic_panel.md) — инструменты из лагов
- [Разность разностей](causal_did.md) — другой способ причинного вывода
