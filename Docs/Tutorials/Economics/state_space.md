# Модель пространства состояний и фильтр Калмана

Разделение наблюдаемого ряда на ненаблюдаемый уровень и шум измерения:
локальный уровень и локальный линейный тренд, фильтрация и сглаживание по
рекурсиям Дурбина — Купмана, прогноз с интервалами.

## Постановка задачи

Дано: зашумлённый временной ряд — продажи с учётной погрешностью, показания
датчика, оценка спроса по неполным данным.

Требуется: оценка истинного уровня, скрытого под шумом, его тренд, прогноз с
доверительными интервалами и разложение дисперсии на сигнал и шум.

Где встречается: очистка операционных метрик от помех, выделение базового
уровня спроса, отслеживание меняющейся конверсии, восстановление показателя
при пропусках, оценка ненаблюдаемых компонент.

## Теория

**Модель локального уровня** — простейшая структурная модель:

$$
y_t \;=\; \mu_t + \varepsilon_t,\qquad \varepsilon_t\sim\mathcal N(0,\sigma_\varepsilon^2),
$$
$$
\mu_t \;=\; \mu_{t-1} + \eta_t,\qquad \eta_t\sim\mathcal N(0,\sigma_\eta^2).
$$

Первое уравнение — наблюдение, второе — переход состояния. **Локальный
линейный тренд** добавляет наклон $\nu_t$, который сам блуждает: так
моделируется меняющийся темп роста.

**Отношение сигнал-шум** $q = \sigma_\eta^2/\sigma_\varepsilon^2$ полностью
определяет поведение фильтра. При $q\to 0$ уровень постоянен и оценка — среднее
всего ряда; при $q\to\infty$ уровень следует за каждым наблюдением. Оптимальное
$q$ оценивается максимальным правдоподобием.

**Фильтр Калмана** проходит по ряду вперёд, обновляя оценку состояния:

$$
v_t = y_t - a_{t|t-1},\qquad F_t = P_{t|t-1} + \sigma_\varepsilon^2,\qquad K_t = \frac{P_{t|t-1}}{F_t},
$$
$$
a_{t|t} = a_{t|t-1} + K_t v_t,\qquad P_{t|t} = P_{t|t-1}(1 - K_t).
$$

Коэффициент усиления $K_t$ — вес нового наблюдения. Он автоматически меньше,
когда прошлая оценка точна, и больше, когда неопределённость велика.

**Сглаживание** проходит назад и учитывает **все** наблюдения, включая
будущие. Рекурсия Дурбина — Купмана через вспомогательную переменную $r_t$:

$$
r_{t-1} = \frac{v_t}{F_t} + L_t^{\top}r_t,\qquad L_t = T - K_t Z,
\qquad
\hat\mu_t = a_{t|t-1} + P_{t|t-1}\,r_{t-1}.
$$

Сглаженная оценка всегда точнее фильтрованной — это плата за то, что она
недоступна в реальном времени.

**Правдоподобие** вычисляется попутно из ошибок предсказания:

$$
\ln L = -\frac{1}{2}\sum_t\left(\ln 2\pi F_t + \frac{v_t^2}{F_t}\right),
$$

что делает оценку параметров стандартной задачей оптимизации.

**Проверка.** Ошибки предсказания $v_t/\sqrt{F_t}$ должны быть белым шумом —
проверяется тестом Люнга — Бокса.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(In)$ | $I$ итераций оптимизации; один проход фильтра — $O(n)$ |
| Память | $O(n)$ | сохранённые состояния для сглаживания |

## API

| Метод | Описание |
|-------|----------|
| `StateSpace.Fit(series, model, horizon)` | Оценка дисперсий, сглаживание, прогноз |
| `StateSpaceModel` | `LocalLevel`, `LocalLinearTrend` |
| `StateSpaceResult.Level` / `FilteredLevel` | Сглаженный и фильтрованный уровень |
| `StateSpaceResult.Slope` | Оценка наклона для модели с трендом |
| `StateSpaceResult.SignalToNoise` | Отношение дисперсий состояния и наблюдения |
| `StateSpaceResult.Forecast` / `ForecastLower` / `ForecastUpper` | Прогноз с интервалами |
| `StateSpaceResult.LjungBoxPValue` | Проверка ошибок предсказания |

Исходники: `src/AI.Economics/Econometrics/StateSpace.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(181);
int n = 250;

const double levelSd = 0.4, noiseSd = 2.5;

var series = new Vector(n);
var trueLevel = new Vector(n);
double level = 100;

for (int t = 0; t < n; t++)
{
    level += RandomEngine.NextGaussian(rng, 0, levelSd);
    trueLevel[t] = level;
    series[t] = level + RandomEngine.NextGaussian(rng, 0, noiseSd);
}

StateSpaceResult state = StateSpace.Fit(series, StateSpaceModel.LocalLevel, horizon: 12);

Console.WriteLine($"Дисперсия шума наблюдения {state.ObservationVariance:F4} " +
                  $"(истинная {noiseSd * noiseSd:F4})");
Console.WriteLine($"Дисперсия уровня {state.LevelVariance:F4} " +
                  $"(истинная {levelSd * levelSd:F4})");
Console.WriteLine($"Отношение сигнал-шум {state.SignalToNoise:F4}");
Console.WriteLine($"Логарифм правдоподобия {state.LogLikelihood:F1}, AIC {state.Aic:F1}");
```

Сглаживание убирает шум, не срезая настоящие изменения уровня:

```csharp
double seriesSteps = 0, levelSteps = 0, error = 0;

for (int t = 1; t < n; t++)
{
    seriesSteps += Math.Abs(series[t] - series[t - 1]);
    levelSteps += Math.Abs(state.Level[t] - state.Level[t - 1]);
}

for (int t = 0; t < n; t++) error += Math.Abs(state.Level[t] - trueLevel[t]);

Console.WriteLine($"Средний шаг ряда {seriesSteps / (n - 1):F3}, " +
                  $"сглаженного уровня {levelSteps / (n - 1):F3}");
Console.WriteLine($"Средняя ошибка восстановления уровня {error / n:F3} " +
                  $"при шуме {noiseSd:F2}");
Console.WriteLine($"Люнг — Бокс на ошибках: {state.LjungBox:F2}, p = {state.LjungBoxPValue:F4}");
```

Прогноз идёт с расширяющимся интервалом — это честное отражение блуждания
уровня:

```csharp
for (int h = 0; h < state.Forecast.Count; h++)
{
    Console.WriteLine($"Шаг {h + 1}: {state.Forecast[h]:F2} " +
                      $"[{state.ForecastLower[h]:F2}; {state.ForecastUpper[h]:F2}]");
}
```

Модель с трендом уместна, когда у ряда есть меняющийся наклон:

```csharp
var trending = new Vector(n);
double trendLevel = 100, slope = 0;

for (int t = 0; t < n; t++)
{
    slope += RandomEngine.NextGaussian(rng, 0, 0.02);
    trendLevel += slope + RandomEngine.NextGaussian(rng, 0, levelSd);
    trending[t] = trendLevel + RandomEngine.NextGaussian(rng, 0, noiseSd);
}

StateSpaceResult withTrend = StateSpace.Fit(trending, StateSpaceModel.LocalLinearTrend, 12);

Console.WriteLine($"Дисперсия наклона {withTrend.SlopeVariance:F6}");
Console.WriteLine($"Текущий наклон {withTrend.Slope[^1]:F3} за период");
Console.WriteLine($"AIC: уровень {StateSpace.Fit(trending, StateSpaceModel.LocalLevel, 1).Aic:F1}, " +
                  $"тренд {withTrend.Aic:F1}");

Console.WriteLine(state.Interpret().ToLlmText());
```

## Ограничения

- Обе дисперсии оцениваются из одного ряда, и при коротких выборках задача
  плохо обусловлена: правдоподобие почти плоское по $q$. На выборке меньше 50
  наблюдений оценка ненадёжна.
- Возможно вырожденное решение с нулевой дисперсией уровня — тогда модель
  сводится к постоянному среднему. Это не ошибка расчёта, а честный вывод об
  отсутствии динамики.
- Модель предполагает нормальность обоих шумов. Выбросы в наблюдениях сильно
  сдвигают оценку уровня; для устойчивости нужны робастные варианты фильтра.
- Сглаженный ряд использует будущие наблюдения и потому недоступен в реальном
  времени. Для оперативных решений берите `FilteredLevel`.
- Прогнозный интервал учитывает только оценённую неопределённость состояния,
  но не ошибку оценки самих дисперсий, поэтому он несколько узок.

## См. также

- [Векторная авторегрессия](var_model.md) — динамика нескольких рядов
- [Условная волатильность GARCH](garch.md) — ненаблюдаемая дисперсия
- [Экспоненциальное сглаживание](ets.md) — родственная модель без вероятностной базы
