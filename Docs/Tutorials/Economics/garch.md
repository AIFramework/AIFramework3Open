# Условная волатильность: GARCH, GJR и EGARCH

Моделирование кластеризации волатильности и эффекта рычага: оценка методом
максимального правдоподобия с таргетированием дисперсии, прогноз волатильности
и тест ARCH на остатках.

## Постановка задачи

Дано: ряд доходностей — актива, портфеля, валютной пары.

Требуется: оценка текущей условной волатильности, её прогноз на несколько
дней вперёд и параметры, описывающие инерцию риска.

Где встречается: динамический расчёт VaR, ценообразование опционов,
маржинальные требования, размер позиции в торговой стратегии, оценка риска
портфеля в реальном времени.

## Теория

**Наблюдаемый факт.** Доходности почти непредсказуемы, но их **квадраты**
сильно автокоррелированы: спокойные дни следуют за спокойными, бурные — за
бурными. Постоянная волатильность противоречит данным.

**Модель GARCH(1,1):**

$$
r_t = \mu + \varepsilon_t,\qquad \varepsilon_t = \sigma_t z_t,\qquad z_t\sim\mathcal N(0,1),
$$
$$
\sigma_t^2 \;=\; \omega + \alpha\,\varepsilon_{t-1}^2 + \beta\,\sigma_{t-1}^2 .
$$

Параметр $\alpha$ отвечает за реакцию на свежий шок, $\beta$ — за память.
Их сумма — **инерция** $\pi = \alpha + \beta$; условие стационарности
$\pi < 1$. Типичные оценки на дневных данных дают $\pi \approx 0{,}95$–$0{,}99$:
шок волатильности рассасывается неделями.

**Долгосрочная дисперсия** $\bar\sigma^2 = \omega/(1-\pi)$, а период
полураспада шока $\ln 0{,}5/\ln\pi$.

**Таргетирование дисперсии.** Параметр $\omega$ на несколько порядков меньше
остальных (при дневной волатильности 1% он около $10^{-6}$), что делает
оптимизацию численно неустойчивой. Решение — выразить его через выборочную
дисперсию: $\omega = \hat\sigma^2(1-\pi)$, оставив свободными только $\alpha$
и $\beta$. Это заметно улучшает сходимость и точность оценки инерции.

**Эффект рычага.** Падения повышают волатильность сильнее, чем рост той же
величины. GJR-GARCH добавляет асимметричное слагаемое:

$$
\sigma_t^2 = \omega + (\alpha + \gamma\,\mathbf 1\{\varepsilon_{t-1}<0\})\varepsilon_{t-1}^2 + \beta\sigma_{t-1}^2 .
$$

EGARCH моделирует логарифм дисперсии, что автоматически гарантирует её
положительность и допускает произвольные знаки параметров.

**Прогноз** сходится к долгосрочному уровню геометрически:

$$
\mathbb E[\sigma_{t+h}^2] \;=\; \bar\sigma^2 + \pi^{h-1}\left(\sigma_{t+1}^2 - \bar\sigma^2\right).
$$

**Проверка.** Тест ARCH-LM на стандартизованных остатках $z_t =
\varepsilon_t/\sigma_t$ должен **не** отвергать нулевую гипотезу: если в них
остаётся эффект ARCH, модель не описала динамику волатильности.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(In)$ | $I$ итераций оптимизации правдоподобия |
| Память | $O(n)$ | ряды волатильности и остатков |

## API

| Метод | Описание |
|-------|----------|
| `Garch.Fit(returns, model, horizon)` | Оценка и прогноз волатильности |
| `Garch.ArchTest(series, lags, out pValue)` | Тест на наличие эффекта ARCH |
| `GarchModel` | `Garch`, `GjrGarch`, `EGarch` |
| `GarchResult.ConditionalVolatility` | Ряд условной волатильности |
| `GarchResult.Persistence` / `HalfLife` / `LongRunVolatility` | Инерция риска |
| `GarchResult.ArchPValue` | Проверка адекватности модели |

Исходники: `src/AI.Economics/Econometrics/Garch.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(179);
int n = 2000;

const double alpha = 0.10, beta = 0.87, longRun = 0.014;
double omega = longRun * longRun * (1 - alpha - beta);

var returns = new Vector(n);
double variance = longRun * longRun;

for (int t = 0; t < n; t++)
{
    double shock = RandomEngine.NextGaussian(rng) * Math.Sqrt(variance);
    returns[t] = shock;
    variance = omega + (alpha * shock * shock) + (beta * variance);
}

GarchResult garch = Garch.Fit(returns, GarchModel.Garch, horizon: 10);

Console.WriteLine($"Истинные параметры: α = {alpha:F3}, β = {beta:F3}, " +
                  $"инерция {alpha + beta:F3}");
Console.WriteLine($"Оценки: α = {garch.Alpha:F4}, β = {garch.Beta:F4}, " +
                  $"инерция {garch.Persistence:F4}");
Console.WriteLine($"Долгосрочная волатильность {garch.LongRunVolatility:P3} " +
                  $"(истинная {longRun:P3})");
Console.WriteLine($"Период полураспада шока {garch.HalfLife:F1} дней");
Console.WriteLine($"Текущая волатильность {garch.ConditionalVolatility[^1]:P3}");
```

Адекватность модели проверяется по стандартизованным остаткам:

```csharp
Console.WriteLine($"ARCH-LM на остатках: {garch.ArchStatistic:F2}, p = {garch.ArchPValue:F4}");
Console.WriteLine(garch.ArchPValue > 0.05
    ? "Эффект ARCH снят — модель адекватна"
    : "В остатках остался эффект ARCH — нужна другая спецификация");

double rawArch = Garch.ArchTest(returns, lags: 5, out double rawP);
Console.WriteLine($"До моделирования: {rawArch:F2}, p = {rawP:F6} — эффект присутствует");
```

Прогноз волатильности — то, ради чего строится модель:

```csharp
for (int h = 0; h < garch.Forecast.Count; h++)
    Console.WriteLine($"День {h + 1}: волатильность {garch.Forecast[h]:P3}");

Console.WriteLine($"Однодневный VaR 99%: {2.326 * garch.Forecast[0]:P2}");
```

Спецификация выбирается по информационным критериям:

```csharp
foreach (GarchModel model in Enum.GetValues<GarchModel>())
{
    GarchResult candidate = Garch.Fit(returns, model, horizon: 1);
    Console.WriteLine($"{model}: логарифм правдоподобия {candidate.LogLikelihood:F1}, " +
                      $"AIC {candidate.Aic:F1}, асимметрия {candidate.Gamma:F4}");
}

Console.WriteLine(garch.Interpret().ToLlmText());
```

## Ограничения

- Модель предполагает нормальность стандартизованных остатков, что почти
  никогда не выполняется: у них остаётся эксцесс 4–6. Оценки при этом
  состоятельны (квазимаксимальное правдоподобие), но стандартные ошибки
  требуют робастной поправки.
- Инерция, оценённая близко к единице, часто отражает структурный сдвиг в
  уровне волатильности, а не истинную долгую память. Проверяйте устойчивость
  оценки на подвыборках.
- Для оценки нужно не менее 500–1000 наблюдений. На квартальных данных
  GARCH бессмыслен.
- Модель описывает волатильность, но не хвост распределения. Для расчёта VaR
  комбинируйте её со стандартизованными остатками из истории — это и есть
  фильтрованная историческая симуляция.
- Прогноз на длинном горизонте вырождается в долгосрочный уровень. Полезная
  информация содержится в первых пяти-десяти шагах.

## См. также

- [Value at Risk](value_at_risk.md) — главный потребитель прогноза волатильности
- [Бэктестирование VaR](var_backtest.md) — проверка получившейся модели риска
- [Модель состояния и фильтр Калмана](state_space.md) — альтернативный способ выделить динамику
