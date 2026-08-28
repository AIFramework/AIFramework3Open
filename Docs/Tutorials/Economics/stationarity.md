# Проверка стационарности: ADF и KPSS

Расширенный тест Дики — Фуллера и тест KPSS с противоположными нулевыми
гипотезами: определение порядка интегрирования ряда и защита от ложной
регрессии.

## Постановка задачи

Дано: временной ряд — курс, объём продаж, индекс цен, ставка.

Требуется: ответ на вопрос, стационарен ли ряд, и если нет — сколько раз его
надо продифференцировать. Без этого ответа регрессия одного ряда на другой
даёт ложные результаты.

Где встречается: подготовка данных для любой модели временных рядов,
проверка перед [коинтеграционным анализом](cointegration.md), обоснование
работы с приростами вместо уровней, тестирование гипотезы случайного
блуждания.

## Теория

**Опасность нестационарности.** Регрессия одного случайного блуждания на
другое даёт $R^2$ около 0,5 и высокозначимый коэффициент — при полном
отсутствии связи. Это явление ложной регрессии, и именно из-за него проверка
стационарности обязательна.

**Тест Дики — Фуллера** проверяет наличие единичного корня. Модель
записывается в разностях:

$$
\Delta y_t \;=\; \alpha + \delta t + \gamma\,y_{t-1}
+ \sum_{i=1}^{p}\phi_i\,\Delta y_{t-i} + \varepsilon_t .
$$

Нулевая гипотеза $\gamma = 0$ означает единичный корень (нестационарность).
Лаги $\Delta y_{t-i}$ добавляются, чтобы очистить остатки от автокорреляции —
отсюда «расширенный».

Критическая особенность: при $\gamma = 0$ t-статистика **не** распределена по
Стьюденту. Её распределение получено моделированием, и критические значения
существенно левее обычных — около $-2{,}86$ вместо $-1{,}96$ на уровне 5% для
спецификации с константой. Использование обычных таблиц ведёт к слишком
частому отклонению нулевой гипотезы.

**Тест KPSS** ставит противоположную нулевую гипотезу — ряд стационарен.
Статистика строится по частичным суммам остатков:

$$
\mathrm{KPSS} \;=\; \frac{1}{n^2\hat\sigma^2_{LR}}\sum_{t=1}^{n} S_t^2,
\qquad S_t = \sum_{i=1}^{t} e_i,
$$

где $\hat\sigma^2_{LR}$ — долгосрочная дисперсия с поправкой Ньюи — Уэста.
Если ряд нестационарен, частичные суммы растут, и статистика велика.

**Совместное чтение** — главная практика. Тесты дополняют друг друга:

| ADF | KPSS | Вывод |
|-----|------|-------|
| отвергает | не отвергает | ряд стационарен |
| не отвергает | отвергает | ряд интегрирован, дифференцируйте |
| оба отвергают | | возможен структурный сдвиг |
| ни один не отвергает | | данных недостаточно |

**Детерминированные компоненты.** Спецификация («ничего», «константа»,
«константа и тренд») меняет критические значения. Ряд с очевидным трендом
нужно тестировать со спецификацией тренда, иначе тренд маскируется под
единичный корень.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(np^2 + p^3)$ | $p$ лагов в расширенной регрессии |
| Память | $O(np)$ | матрица лагов |

## API

| Метод | Описание |
|-------|----------|
| `StationarityTests.Analyze(series, terms, lags, name)` | Оба теста и итоговый вердикт |
| `StationarityTests.DickeyFuller(series, terms, lags)` | Только ADF |
| `StationarityTests.Kpss(series, terms, lags)` | Только KPSS |
| `StationarityReport.IntegrationOrder` | Порядок интегрирования |
| `StationarityReport.Verdict` | Согласованный вывод словами |
| `UnitRootTest.CriticalFivePercent` и соседние | Критические значения |

Исходники: `src/AI.Economics/Econometrics/StationarityTests.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(163);
int n = 400;

var randomWalk = new Vector(n);
var stationary = new Vector(n);
double level = 0, meanReverting = 0;

for (int t = 0; t < n; t++)
{
    level += RandomEngine.NextGaussian(rng);
    meanReverting = (0.3 * meanReverting) + RandomEngine.NextGaussian(rng);

    randomWalk[t] = level + 100;
    stationary[t] = meanReverting + 100;
}

StationarityReport walkReport = StationarityTests.Analyze(
    randomWalk, DeterministicTerms.Constant, lags: -1, name: "случайное блуждание");

StationarityReport stableReport = StationarityTests.Analyze(
    stationary, DeterministicTerms.Constant, lags: -1, name: "стационарный ряд");

Console.WriteLine($"Блуждание: {walkReport.Verdict}, порядок {walkReport.IntegrationOrder}");
Console.WriteLine($"  ADF {walkReport.AugmentedDickeyFuller.Statistic:F3} " +
                  $"при критическом {walkReport.AugmentedDickeyFuller.CriticalFivePercent:F3}");
Console.WriteLine($"  KPSS {walkReport.Kpss.Statistic:F3} " +
                  $"при критическом {walkReport.Kpss.CriticalFivePercent:F3}");

Console.WriteLine($"Стационарный: {stableReport.Verdict}, порядок {stableReport.IntegrationOrder}");
Console.WriteLine($"  ADF {stableReport.AugmentedDickeyFuller.Statistic:F3}, " +
                  $"KPSS {stableReport.Kpss.Statistic:F3}");
```

Дифференцирование — стандартное лечение нестационарности:

```csharp
var differenced = new Vector(n - 1);
for (int t = 1; t < n; t++) differenced[t - 1] = randomWalk[t] - randomWalk[t - 1];

UnitRootTest adfDiff = StationarityTests.DickeyFuller(differenced, DeterministicTerms.Constant);
UnitRootTest kpssDiff = StationarityTests.Kpss(differenced, DeterministicTerms.Constant);

Console.WriteLine($"После дифференцирования: ADF {adfDiff.Statistic:F3} " +
                  $"({(adfDiff.Rejected ? "стационарен" : "всё ещё нет")}), " +
                  $"KPSS {kpssDiff.Statistic:F3} " +
                  $"({(kpssDiff.Rejected ? "нестационарен" : "стационарен")})");
```

Ряд с трендом нужно тестировать в правильной спецификации:

```csharp
var trending = new Vector(n);
double drift = 0;
for (int t = 0; t < n; t++)
{
    drift = (0.4 * drift) + RandomEngine.NextGaussian(rng);
    trending[t] = 50 + (0.2 * t) + drift;
}

foreach (DeterministicTerms terms in Enum.GetValues<DeterministicTerms>())
{
    UnitRootTest test = StationarityTests.DickeyFuller(trending, terms);
    Console.WriteLine($"{terms}: ADF {test.Statistic:F3} при критическом " +
                      $"{test.CriticalFivePercent:F3} — " +
                      (test.Rejected ? "стационарен" : "единичный корень"));
}

Console.WriteLine(walkReport.Interpret().ToLlmText());
```

## Ограничения

- Тест ADF имеет низкую мощность: он плохо отличает единичный корень от
  корня 0,95. На выборке меньше 100 наблюдений его вывод ненадёжен.
- Структурный сдвиг маскируется под единичный корень. Ряд со сломом тренда
  почти всегда «нестационарен» по ADF; для таких случаев нужны тесты
  Перрона с эндогенной точкой сдвига.
- Число лагов заметно влияет на результат. Автоматический выбор по правилу
  Швета — компромисс; проверяйте устойчивость к $p \pm 2$.
- Спецификация детерминированных компонент важнее числа лагов. Включение
  лишнего тренда снижает мощность, его отсутствие при реальном тренде ведёт
  к ложному выводу о нестационарности.
- Дифференцирование стационарного ряда вредно: оно вносит неинвертируемую
  скользящую среднюю и портит оценки. Не дифференцируйте «на всякий случай».

## См. также

- [Коинтеграция и модель коррекции ошибок](cointegration.md) — связь нестационарных рядов
- [Векторная авторегрессия](var_model.md) — модель на стационарных рядах
- [Диагностика регрессии](regression_diagnostics.md) — общие проверки предпосылок
