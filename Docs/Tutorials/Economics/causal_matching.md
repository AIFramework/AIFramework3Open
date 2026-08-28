# Сопоставление по склонности к воздействию

Оценка эффекта по наблюдательным данным: логит-модель вероятности попасть под
воздействие, подбор контрольных объектов по близости этой вероятности,
проверка баланса ковариат и общей поддержки.

## Постановка задачи

Дано: объекты, часть которых подверглась воздействию не случайно, а по
собственному выбору или по решению, зависящему от наблюдаемых характеристик;
эти характеристики известны.

Требуется: эффект воздействия на подвергшихся ему, очищенный от различий в
наблюдаемых характеристиках между группами.

Где встречается: оценка эффекта программы лояльности, влияние обучения на
производительность, отдача от участия в госпрограмме, эффект перехода
клиента на новый тариф.

## Теория

**Проблема отбора.** Участники программы отличаются от неучастников. Простая
разность средних смешивает эффект программы с этими различиями: клиенты,
подключившие подписку, и без неё покупали бы больше.

**Ключевой результат Розенбаума и Рубина.** Если воздействие условно
независимо от исходов при заданных ковариатах $X$, то оно условно независимо
и при заданной **скалярной** склонности:

$$
e(X) = \Pr(D = 1\mid X).
$$

Это сводит сопоставление по многим признакам к сопоставлению по одному числу —
иначе при десяти ковариатах точных совпадений просто не найдётся.

**Процедура.** Склонность оценивается логитом; каждому объекту под
воздействием подбираются $k$ ближайших контрольных объектов, при условии что
расстояние не превышает калипер:

$$
|e_i - e_j| \le \delta\cdot\sigma_{\mathrm{logit}(e)} .
$$

Стандартное значение $\delta = 0{,}2$. Объекты, которым пары не нашлось,
исключаются — доля оставшихся и есть **общая поддержка**.

**Эффект** — среднее по парам:

$$
\widehat{\mathrm{ATT}} = \frac{1}{M}\sum_{i\in \text{сопоставленные}}
\left(y_i - \frac{1}{k}\sum_{j\in \mathcal N(i)} y_j\right).
$$

**Проверка баланса** — главный критерий качества, важнее самого эффекта.
Стандартизованная разность средних:

$$
d_j = \frac{\bar x_j^{\text{возд}} - \bar x_j^{\text{контр}}}
{\sqrt{(s_{j,\text{возд}}^2 + s_{j,\text{контр}}^2)/2}} .
$$

Порог приемлемости $|d_j| < 0{,}1$. Если после сопоставления баланс не
достигнут, метод не сработал, и результат публиковать нельзя.

**Фундаментальное ограничение.** Метод контролирует только **наблюдаемые**
различия. Ненаблюдаемая мотивация, скрытое качество, частная информация
остаются источником смещения — и никакой статистикой это не проверяется.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(Ink^2 + N_1 N_0)$ | оценка логита и поиск пар |
| Память | $O(nk)$ | ковариаты и склонности |

## API

| Метод | Описание |
|-------|----------|
| `PropensityScoreMatching.Estimate(covariates, treatment, outcome, names, caliper, neighbours)` | Полная процедура |
| `MatchingResult.AverageTreatmentEffectOnTreated` | Эффект на подвергшихся воздействию |
| `MatchingResult.NaiveDifference` | Разность средних без сопоставления |
| `MatchingResult.Balance` | Стандартизованные разности до и после |
| `MatchingResult.CommonSupport` / `Matched` | Доля и число сопоставленных |
| `MatchingResult.PropensityModel` | Логит-модель склонности |

Исходники: `src/AI.Economics/Econometrics/PropensityScoreMatching.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(149);
int n = 4000;
const double trueEffect = 1.0;

var covariates = new Matrix(n, 3);
var treatment = new Vector(n);
var outcome = new Vector(n);

for (int i = 0; i < n; i++)
{
    double tenure = RandomEngine.NextGaussian(rng);
    double spend = RandomEngine.NextGaussian(rng);
    double activity = RandomEngine.NextGaussian(rng);

    covariates[i, 0] = tenure;
    covariates[i, 1] = spend;
    covariates[i, 2] = activity;

    // Подключаются активные и много тратящие — источник смещения
    double index = (0.9 * tenure) + (0.7 * spend) + (0.3 * activity);
    bool joined = rng.NextDouble() < 1.0 / (1.0 + Math.Exp(-index));
    treatment[i] = joined ? 1 : 0;

    outcome[i] = (1.5 * tenure) + (1.2 * spend) + (0.4 * activity)
        + (joined ? trueEffect : 0) + RandomEngine.NextGaussian(rng, 0, 0.5);
}

MatchingResult matching = PropensityScoreMatching.Estimate(
    covariates, treatment, outcome,
    names: ["стаж", "траты", "активность"], caliperFactor: 0.2, neighbours: 3);

Console.WriteLine($"Истинный эффект {trueEffect:F2}");
Console.WriteLine($"Наивная разность {matching.NaiveDifference:F4} — смещена отбором");
Console.WriteLine($"После сопоставления {matching.AverageTreatmentEffectOnTreated:F4} " +
                  $"± {matching.StandardError:F4} (p = {matching.PValue:F5})");
Console.WriteLine($"Сопоставлено {matching.Matched} из {matching.Treated} " +
                  $"(поддержка {matching.CommonSupport:P1})");
```

Баланс проверяется до интерпретации эффекта, а не после:

```csharp
foreach (BalanceCheck check in matching.Balance)
{
    Console.WriteLine($"{check.Variable}: разность до {check.StandardizedBefore:F3}, " +
                      $"после {check.StandardizedAfter:F3} " +
                      (check.IsBalanced ? "— сбалансировано" : "— НЕ сбалансировано"));
}

bool balanced = matching.Balance.All(b => b.IsBalanced);
Console.WriteLine(balanced
    ? "Баланс достигнут, оценке можно доверять"
    : "Баланс не достигнут — нужна другая спецификация склонности");
```

Калипер и число соседей регулируют компромисс «смещение против дисперсии»:

```csharp
foreach (int neighbours in new[] { 1, 3, 5 })
{
    MatchingResult variant = PropensityScoreMatching.Estimate(
        covariates, treatment, outcome, ["стаж", "траты", "активность"], 0.2, neighbours);

    Console.WriteLine($"{neighbours} соседей: эффект {variant.AverageTreatmentEffectOnTreated:F4} " +
                      $"± {variant.StandardError:F4}, " +
                      $"максимальный дисбаланс " +
                      $"{variant.Balance.Max(b => Math.Abs(b.StandardizedAfter)):F3}");
}

Console.WriteLine(matching.Interpret().ToLlmText());
```

## Ограничения

- Метод не защищает от ненаблюдаемых различий. Если в решение участвовать
  вмешивается мотивация, которой нет в данных, смещение остаётся любой
  величины. Это не техническая проблема, а принципиальная.
- Стандартные ошибки простой формулы занижены, поскольку не учитывают ошибку
  оценки склонности и повторное использование контрольных объектов. Для
  публикации нужен бутстрап всей процедуры.
- Малая общая поддержка означает, что группы почти не пересекаются, и вывод
  делается по нетипичной подвыборке. Ниже 70% поддержки результат сомнителен.
- Баланс достигается подбором спецификации логита — добавлением
  взаимодействий и квадратов. Это итеративный процесс, и его надо честно
  описывать.
- При наличии панельных данных [разность разностей](causal_did.md)
  предпочтительнее: она контролирует и ненаблюдаемые постоянные различия.

## См. также

- [Разность разностей](causal_did.md) — контроль ненаблюдаемых постоянных различий
- [Причинный лес](causal_forest.md) — гетерогенность эффекта
- [Модели ограниченного отклика](limited_dependent.md) — логит для склонности
