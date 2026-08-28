# Динамические панели: оценка Ареллано — Бонда

Модель с лагом отклика среди регрессоров: смещение Никелла, разностный GMM с
инструментами из уровней, тесты Саргана и автокорреляции второго порядка.

## Постановка задачи

Дано: панель «объект × период», в которой сегодняшнее значение отклика
зависит от вчерашнего — продажи, доля рынка, уровень долга, занятость.

Требуется: несмещённая оценка коэффициента инерции и коэффициентов при
остальных регрессорах.

Где встречается: модели корректировки структуры капитала, инерция спроса,
динамика занятости, сходимость регионов по доходу, привыкание к рекламе.

## Теория

**Динамическая модель:**

$$
y_{it} \;=\; \rho\,y_{i,t-1} + x_{it}^{\top}\beta + u_i + \varepsilon_{it}.
$$

**Смещение Никелла.** Внутригрупповое преобразование вычитает средние по
объекту, но среднее $\bar y_i$ содержит $y_{it}$, а значит, коррелирует с
$\varepsilon_{it}$. Возникает смещение порядка $-1/T$: при $T = 5$ оценка
$\rho$ занижена примерно на 0,2. Объединённый МНК, наоборот, завышает $\rho$,
поскольку лаг отклика вбирает влияние $u_i$.

Это даёт практическое правило: **истинное значение лежит между двумя
оценками**. Оценка, вышедшая за эти границы, почти наверняка ошибочна.

**Разностный GMM.** Ареллано и Бонд предложили взять первые разности,
устранив $u_i$:

$$
\Delta y_{it} \;=\; \rho\,\Delta y_{i,t-1} + \Delta x_{it}^{\top}\beta + \Delta\varepsilon_{it},
$$

и инструментировать $\Delta y_{i,t-1}$ **уровнями** с лагом два и глубже.
Уровень $y_{i,t-2}$ коррелирует с $\Delta y_{i,t-1}$ и ортогонален
$\Delta\varepsilon_{it}$ при отсутствии автокорреляции исходных ошибок.

Число доступных инструментов растёт с $t$: для периода $t$ пригодны
$y_{i1},\dots,y_{i,t-2}$. Матрица инструментов блочно-диагональна, а
одношаговая весовая матрица имеет вид $H$ с двойками на диагонали и
минус единицами рядом — она соответствует ковариации разностей независимых
ошибок.

**Тест на автокорреляцию.** В разностях $\Delta\varepsilon_{it}$ автоматически
коррелирован первого порядка — это нормально. А вот значимая автокорреляция
**второго** порядка означает, что исходные ошибки коррелированы, и
инструменты недействительны. Тест AR(2) — главная проверка модели.

**Тест Саргана** проверяет валидность всего набора инструментов. Его
слабость — низкая мощность при большом числе инструментов; при
$\#\text{инструментов} > \#\text{объектов}$ он почти всегда проходит
независимо от истины. Отсюда правило ограничивать глубину лагов.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(NTm^2 + m^3)$ | $m$ — число инструментов, растёт как $T^2$ |
| Память | $O(NTm)$ | блочная матрица инструментов |

## API

| Метод | Описание |
|-------|----------|
| `DynamicPanel.ArellanoBond(dataset, maxLags)` | Разностный GMM |
| `DynamicPanelResult.Persistence` | Коэффициент при лаге отклика |
| `DynamicPanelResult.WithinPersistence` / `PooledPersistence` | Границы правдоподобного диапазона |
| `DynamicPanelResult.IsInBounds` | Попала ли оценка между границами |
| `DynamicPanelResult.SarganPValue` / `Ar2PValue` | Валидность инструментов |
| `DynamicPanelResult.LongRunMultiplier` | Долгосрочный эффект $\beta/(1-\rho)$ |

Исходники: `src/AI.Economics/Econometrics/DynamicPanel.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(113);
int units = 150, periods = 9;

var xs = new List<double>();
var ys = new List<double>();
var unitIds = new List<int>();
var periodIds = new List<int>();

const double truePersistence = 0.55;

for (int u = 0; u < units; u++)
{
    double effect = RandomEngine.NextGaussian(rng, 0, 0.5);
    double level = effect / (1 - truePersistence);

    for (int t = 0; t < periods; t++)
    {
        double driver = RandomEngine.NextGaussian(rng);
        level = (truePersistence * level) + (0.4 * driver) + effect
            + RandomEngine.NextGaussian(rng, 0, 0.3);

        xs.Add(driver);
        ys.Add(level);
        unitIds.Add(u);
        periodIds.Add(t);
    }
}

var regressors = new Matrix(xs.Count, 1);
var response = new Vector(ys.Count);
for (int i = 0; i < xs.Count; i++) { regressors[i, 0] = xs[i]; response[i] = ys[i]; }

var panel = new PanelDataset
{
    Regressors = regressors, Response = response,
    Units = unitIds, Periods = periodIds, Names = ["драйвер"],
};

DynamicPanelResult dynamicPanel = DynamicPanel.ArellanoBond(panel, maxLags: 3);

Console.WriteLine($"Истинная инерция {truePersistence:F2}");
Console.WriteLine($"Фиксированные эффекты (нижняя граница): {dynamicPanel.WithinPersistence:F4}");
Console.WriteLine($"Ареллано — Бонд: {dynamicPanel.Persistence:F4}");
Console.WriteLine($"Объединённый МНК (верхняя граница): {dynamicPanel.PooledPersistence:F4}");
Console.WriteLine(dynamicPanel.IsInBounds
    ? "Оценка внутри правдоподобного диапазона"
    : "Оценка вне границ — модель под подозрением");
```

Валидность инструментов проверяется двумя тестами, и оба обязательны:

```csharp
Console.WriteLine($"Инструментов {dynamicPanel.Instruments} на {dynamicPanel.Units} объектов");
Console.WriteLine($"Сарган {dynamicPanel.SarganStatistic:F2}, p = {dynamicPanel.SarganPValue:F4}");
Console.WriteLine($"AR(2) {dynamicPanel.ArellanoBondAr2:F3}, p = {dynamicPanel.Ar2PValue:F4}");
Console.WriteLine(dynamicPanel.Ar2PValue > 0.05
    ? "Автокорреляции второго порядка нет — инструменты пригодны"
    : "Есть автокорреляция AR(2) — инструменты недействительны");
```

Долгосрочный эффект отличается от краткосрочного тем сильнее, чем выше
инерция:

```csharp
foreach (Coefficient coefficient in dynamicPanel.Coefficients)
    Console.WriteLine($"{coefficient.Name}: {coefficient.Estimate:F4} (p = {coefficient.PValue:F4})");

Console.WriteLine($"Долгосрочный мультипликатор {dynamicPanel.LongRunMultiplier:F3}");
Console.WriteLine(dynamicPanel.Interpret().ToLlmText());
```

## Ограничения

- Разрастание инструментов — главная практическая проблема. При $T = 10$ и
  полной глубине лагов их сотни, тест Саргана теряет мощность, а оценка
  смещается к внутригрупповой. Ограничивайте `maxLags` двумя-тремя.
- Метод рассчитан на «много объектов, мало периодов». При $N < 50$ оценки
  неустойчивы, при большом $T$ уместнее временные ряды.
- При инерции, близкой к единице, уровни слабо коррелируют с разностями:
  инструменты становятся слабыми. Тогда нужен системный GMM (Бланделл — Бонд),
  добавляющий уравнение в уровнях.
- Одношаговая оценка менее эффективна двухшаговой, но её стандартные ошибки
  надёжнее в малых выборках. Двухшаговая требует поправки Виндмейера.
- Требование отсутствия автокорреляции исходных ошибок нетривиально. Тест
  AR(2) — необходимое, но не достаточное условие.

## См. также

- [Панельные данные](panel_data.md) — статические модели и границы оценки
- [Инструментальные переменные](iv_2sls.md) — общая логика инструментирования
- [Векторная авторегрессия](var_model.md) — динамика на длинных рядах
