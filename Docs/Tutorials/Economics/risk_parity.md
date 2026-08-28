# Паритет риска и иерархический паритет

Обратная волатильность, равный вклад в риск и иерархический паритет (HRP) —
три способа построить портфель, не прогнозируя доходности.

## Постановка задачи

Дано: ковариационная матрица доходностей активов. Ожидаемые доходности не
требуются.

Требуется: веса портфеля, в котором риск распределён между активами
осознанно, а не как побочный результат распределения капитала.

Где встречается: стратегическая аллокация фондов, портфели «всепогодного»
типа, распределение лимитов между торговыми стратегиями, случаи, когда
прогнозу доходности доверять нельзя.

## Теория

Мотивация проста: оценка ожидаемой доходности имеет стандартную ошибку
порядка самой доходности, а оценка ковариации — на порядок точнее. Значит,
надо строить портфель, используя только то, что мы умеем оценивать.

**Обратная волатильность** — простейший вариант: $w_i \propto 1/\sigma_i$.
Он игнорирует корреляции и потому концентрирует риск в группе связанных
активов.

**Равный вклад в риск** решает уравнение, в котором все вклады равны:

$$
\mathrm{RC}_i \;=\; \frac{w_i(\Sigma w)_i}{w^{\top}\Sigma w} \;=\; \frac{1}{n}
\qquad\text{для всех } i .
$$

Задача решается мультипликативными итерациями:

$$
w_i \;\leftarrow\; w_i\,\sqrt{\frac{1/n}{\mathrm{RC}_i}},
$$

с нормировкой на единицу после каждого шага. Сходимость быстрая, решение при
положительно определённой $\Sigma$ единственно.

**Иерархический паритет риска** (López de Prado) избегает обращения
ковариационной матрицы вовсе — именно оно является источником неустойчивости
Марковица. Алгоритм состоит из трёх шагов:

1. Корреляции переводятся в расстояния
   $d_{ij} = \sqrt{(1-\rho_{ij})/2}$.
2. Активы упорядочиваются так, чтобы похожие оказались рядом (сериация по
   дереву кластеров).
3. Вес распределяется рекурсивным делением: на каждом шаге группа делится
   пополам, и веса половин обратно пропорциональны их дисперсиям:
   $\alpha = 1 - V_1/(V_1+V_2)$.

HRP не требует ни обращения матрицы, ни положительной определённости, и
эмпирически показывает лучшую устойчивость вне выборки, чем оба
предшественника.

**Коэффициент диверсификации** — отношение средневзвешенной волатильности к
волатильности портфеля. Чем он выше, тем больше риска «съедено»
диверсификацией.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(In^2)$ для ERC, $O(n^2\log n)$ для HRP | $I$ итераций |
| Память | $O(n^2)$ | ковариация и матрица расстояний |

## API

| Метод | Описание |
|-------|----------|
| `RiskParity.Build(covariance, assets, method)` | Веса выбранным методом |
| `RiskParityResult.RiskBudget` | Вес и вклад в риск по активам |
| `RiskParityResult.MaximumDeviation` | Максимальное отклонение от равного вклада |
| `RiskParityResult.Clusters` | Кластеры, найденные HRP |
| `RiskParityResult.DiversificationRatio` | Выигрыш от диверсификации |
| `RiskParityResult.EffectiveAssets` | Эффективное число позиций |

Исходники: `src/AI.Economics/Portfolio/RiskParity.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Portfolio;
using AI.Statistics;

Random gen = RandomEngine.Create(41);
var sample = new Matrix(240, 5);

double[] sigma = [0.008, 0.050, 0.045, 0.030, 0.020];

for (int t = 0; t < sample.Height; t++)
{
    double equityFactor = RandomEngine.NextGaussian(gen);
    double rateFactor = RandomEngine.NextGaussian(gen);

    // Активы 1 и 2 сильно связаны между собой — HRP должен их сгруппировать
    sample[t, 0] = sigma[0] * ((0.8 * rateFactor) + (0.6 * RandomEngine.NextGaussian(gen)));
    sample[t, 1] = sigma[1] * ((0.9 * equityFactor) + (0.4 * RandomEngine.NextGaussian(gen)));
    sample[t, 2] = sigma[2] * ((0.85 * equityFactor) + (0.5 * RandomEngine.NextGaussian(gen)));
    sample[t, 3] = sigma[3] * ((0.4 * equityFactor) + (0.9 * RandomEngine.NextGaussian(gen)));
    sample[t, 4] = sigma[4] * ((0.5 * rateFactor) + (0.85 * RandomEngine.NextGaussian(gen)));
}

Matrix cov = MeanVariance.Covariance(sample, shrinkage: 0.1);
string[] names = ["Облигации", "Акции РФ", "Акции США", "Сырьё", "Золото"];

RiskParityResult parity = RiskParity.Build(cov, names, RiskParityMethod.EqualRiskContribution);

foreach ((string asset, double weight, double contribution) in parity.RiskBudget)
    Console.WriteLine($"{asset}: вес {weight:P1}, вклад в риск {contribution:P1}");

Console.WriteLine($"Риск портфеля {parity.Risk:P2}");
Console.WriteLine($"Максимальное отклонение от паритета {parity.MaximumDeviation:P2}");
Console.WriteLine($"Коэффициент диверсификации {parity.DiversificationRatio:F2}");
```

Три метода дают заметно разные портфели — сравнение стоит показывать вместе:

```csharp
foreach (RiskParityMethod method in Enum.GetValues<RiskParityMethod>())
{
    RiskParityResult variant = RiskParity.Build(cov, names, method);
    Console.WriteLine($"{method}: риск {variant.Risk:P2}, " +
                      $"отклонение {variant.MaximumDeviation:P2}, " +
                      $"эффективных активов {variant.EffectiveAssets:F2}");
}
```

Кластеры, найденные HRP, объясняют структуру портфеля лучше самих весов:

```csharp
RiskParityResult hierarchical = RiskParity.Build(cov, names, RiskParityMethod.Hierarchical);

foreach (RiskCluster cluster in hierarchical.Clusters)
{
    Console.WriteLine($"Кластер [{string.Join(", ", cluster.Assets)}]: " +
                      $"вес {cluster.Weight:P1}, дисперсия {cluster.Variance:F5}");
}

Console.WriteLine(hierarchical.Interpret().ToLlmText());
```

## Ограничения

- Паритет риска игнорирует доходность. Он даёт большой вес облигациям, и без
  плеча ожидаемая доходность такого портфеля низка — классические реализации
  используют кредитное плечо, что добавляет риск ликвидности.
- Метод оптимален лишь при равных коэффициентах Шарпа у всех активов. Это
  сильное неявное предположение, а не отсутствие предположений.
- Равный вклад в риск считается по ковариации, которая недооценивает
  зависимость в кризис. При росте корреляций фактический вклад перестаёт быть
  равным ровно тогда, когда это важно.
- HRP чувствителен к способу сериации: разные правила связывания дают разные
  деревья и разные веса. Проверяйте устойчивость на подвыборках.
- Ни один из методов не учитывает транзакционные издержки и лотность —
  для этого добавьте ограничения через [оптимизацию Марковица](mean_variance.md)
  или отдельный слой округления.

## См. также

- [Марковиц и эффективная граница](mean_variance.md) — оптимизация с прогнозом доходности
- [Модель Блэка — Литтермана](black_litterman.md) — компромисс между рынком и взглядами
- [Ребалансировка с издержками](rebalancing.md) — поддержание целевых весов
