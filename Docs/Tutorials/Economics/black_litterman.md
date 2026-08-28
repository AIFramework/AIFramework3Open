# Модель Блэка — Литтермана

Равновесные доходности, обратно выведенные из рыночных весов, смешиваются со
взглядами инвестора по правилу Байеса — и дают устойчивый портфель вместо
экстремальных весов классической оптимизации.

## Постановка задачи

Дано: рыночные веса активов (капитализация или веса эталона), ковариационная
матрица и набор взглядов инвестора вида «актив A обгонит актив B на 3%» или
«актив C даст 12% годовых», каждый со своей уверенностью.

Требуется: скорректированный вектор ожидаемых доходностей и оптимальный
портфель, отклоняющийся от рынка ровно там, где есть основания.

Где встречается: тактическая аллокация, портфель под мандат с эталоном,
интеграция мнения аналитиков в количественный процесс, объяснение отклонений
от эталона инвестиционному комитету.

## Теория

**Проблема, которую решает модель.** Оптимизация Марковица на исторических
доходностях выдаёт нелепые портфели: 90% в одном активе, короткая позиция в
другом. Причина — не в оптимизаторе, а во входных данных: историческая
средняя доходность оценена с ошибкой того же порядка, что и сама величина.

**Обратная оптимизация.** Блэк и Литтерман предложили считать точкой отсчёта
не историю, а рынок. Если рыночный портфель оптимален, то доходности,
которые его порождают, восстанавливаются обращением условия оптимальности:

$$
\Pi \;=\; \lambda\,\Sigma\,w_{\mathrm{mkt}},
$$

где $\lambda$ — коэффициент неприятия риска. Эти равновесные доходности не
надо оценивать — они выведены из наблюдаемых весов.

**Взгляды** записываются линейными ограничениями $P\,\mu = Q + \varepsilon$,
где строка $P$ задаёт комбинацию активов. Абсолютный взгляд имеет одну
единицу в строке, относительный — $+1$ и $-1$. Неуверенность взгляда задаётся
диагональной матрицей $\Omega$.

**Апостериорные доходности** — байесовское смешение:

$$
\hat\mu \;=\; \left[(\tau\Sigma)^{-1} + P^{\top}\Omega^{-1}P\right]^{-1}
\left[(\tau\Sigma)^{-1}\Pi + P^{\top}\Omega^{-1}Q\right].
$$

Параметр $\tau$ отражает неуверенность в самих равновесных доходностях;
обычно берётся 0,025–0,05.

**Ключевое свойство.** Без взглядов $\hat\mu = \Pi$, и оптимальный портфель в
точности совпадает с рыночным. Каждый взгляд сдвигает портфель от рынка
пропорционально своей уверенности — и, что важно, сдвигает **все**
коррелированные активы, а не только упомянутые. Именно поэтому веса остаются
разумными.

**Активная доля** — сумма модулей отклонений от рынка, делённая пополам, —
измеряет агрессивность позиционирования.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(n^3 + k^3)$ | обращение матриц; $k$ — число взглядов |
| Память | $O(n^2)$ | ковариация и матрицы взглядов |

## API

| Метод | Описание |
|-------|----------|
| `BlackLitterman.Blend(marketWeights, covariance, views, assets, riskAversion, tau)` | Смешение и портфель |
| `BlackLitterman.Relative(n, better, worse, excess, confidence, text)` | Относительный взгляд |
| `BlackLitterman.Absolute(n, asset, expected, confidence, text)` | Абсолютный взгляд |
| `BlackLittermanResult.ImpliedReturns` / `PosteriorReturns` | До и после смешения |
| `BlackLittermanResult.ActiveWeights` / `ActiveShare` | Отклонение от рынка |

Исходники: `src/AI.Economics/Portfolio/BlackLitterman.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Portfolio;
using AI.Statistics;

Random maker = RandomEngine.Create(53);
var observations = new Matrix(200, 4);
double[] scale = [0.010, 0.048, 0.035, 0.022];

for (int t = 0; t < observations.Height; t++)
{
    double global = RandomEngine.NextGaussian(maker);
    for (int j = 0; j < observations.Width; j++)
        observations[t, j] = scale[j] * ((0.55 * global) + (0.83 * RandomEngine.NextGaussian(maker)));
}

Matrix sigma = MeanVariance.Covariance(observations, shrinkage: 0.1);
var marketWeights = new Vector(0.45, 0.25, 0.20, 0.10);
string[] universe = ["Облигации", "Акции", "Недвижимость", "Золото"];

BlackLittermanResult neutral = BlackLitterman.Blend(
    marketWeights, sigma, views: null, assets: universe, riskAversion: 2.5, tau: 0.05);

Console.WriteLine("Равновесные доходности (без взглядов):");
for (int i = 0; i < universe.Length; i++)
{
    Console.WriteLine($"  {universe[i]}: {neutral.ImpliedReturns[i]:P2}, " +
                      $"вес {neutral.OptimalWeights[i]:P1}");
}

Console.WriteLine($"Активная доля без взглядов {neutral.ActiveShare:P2}");
```

Взгляды формулируются на языке инвестиционного комитета:

```csharp
var views = new List<InvestorView>
{
    BlackLitterman.Relative(universe.Length, outperformer: 1, underperformer: 0,
        excessReturn: 0.04, confidence: 0.6, description: "Акции обгонят облигации на 4%"),

    BlackLitterman.Absolute(universe.Length, asset: 3,
        expectedReturn: 0.10, confidence: 0.3, description: "Золото даст 10% годовых"),
};

BlackLittermanResult blended = BlackLitterman.Blend(
    marketWeights, sigma, views, universe, riskAversion: 2.5, tau: 0.05);

for (int i = 0; i < universe.Length; i++)
{
    Console.WriteLine($"{universe[i]}: {neutral.ImpliedReturns[i]:P2} → " +
                      $"{blended.PosteriorReturns[i]:P2}, " +
                      $"вес {blended.OptimalWeights[i]:P1} " +
                      $"(отклонение {blended.ActiveWeights[i]:+0.0%;-0.0%;0.0%})");
}
```

Уверенность во взгляде — рычаг, которым регулируется агрессивность:

```csharp
foreach (double confidence in new[] { 0.2, 0.5, 0.9 })
{
    var single = new List<InvestorView>
    {
        BlackLitterman.Relative(universe.Length, 1, 0, 0.04, confidence),
    };

    BlackLittermanResult scenario = BlackLitterman.Blend(marketWeights, sigma, single, universe);
    Console.WriteLine($"Уверенность {confidence:P0}: активная доля {scenario.ActiveShare:P1}");
}

Console.WriteLine(blended.Interpret().ToLlmText());
```

## Ограничения

- Модель предполагает, что рыночный портфель оптимален. Для узкой вселенной
  (пять классов активов вместо всего рынка) это допущение сомнительно, и
  равновесные доходности теряют смысл.
- Параметры $\lambda$ и $\tau$ калибруются по соглашению, а не оцениваются.
  Их изменение в разумных пределах меняет активную долю в разы, поэтому
  фиксируйте их в инвестиционной политике.
- Уверенность во взгляде трудно задать честно. Практическое правило — сначала
  посмотреть на получившийся портфель, а затем откалибровать уверенность так,
  чтобы отклонение от эталона было приемлемым.
- Взгляды, противоречащие друг другу, модель формально примет и усреднит.
  Проверяйте согласованность перед подстановкой.
- Ковариация остаётся исторической со всеми её недостатками — модель
  исправляет только доходности. Сжатие матрицы по-прежнему обязательно.

## См. также

- [Марковиц и эффективная граница](mean_variance.md) — оптимизатор, на который опирается модель
- [Паритет риска](risk_parity.md) — альтернативный отказ от прогноза доходности
- [Факторные модели](factor_model.md) — источник структурированных взглядов
