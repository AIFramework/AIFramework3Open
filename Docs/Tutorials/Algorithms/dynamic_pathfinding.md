# D* Lite, LPA* и ARA*

## Постановка задачи

Дан граф, в котором веса рёбер могут изменяться во время выполнения. Требуется эффективно поддерживать кратчайший путь без полного пересчёта при каждом изменении. Такие задачи возникают в робототехнике и навигации в реальном времени.

## LPA* (Lifelong Planning A*)

Инкрементальная версия A*. Для каждой вершины $v$ поддерживаются два значения:

- $g(v)$ — текущая оценка кратчайшего расстояния
- $rhs(v)$ — одношаговая lookahead-оценка:

$$rhs(v) = \min_{u \in pred(v)} \big(g(u) + c(u, v)\big)$$

Вершина **согласована** (consistent), если $g(v) = rhs(v)$. Несогласованные вершины помещаются в приоритетную очередь с ключом:

$$key(v) = \big[\min(g(v), rhs(v)) + h(v),\; \min(g(v), rhs(v))\big]$$

## D* Lite

Оптимизированная версия LPA* для подвижного агента. Поиск ведётся **от цели к старту**, что позволяет эффективно обновлять путь при движении агента и обнаружении новых препятствий.

## ARA* (Anytime Repairing A*)

Начинает с быстрого субоптимального решения (с завышенной эвристикой $\varepsilon \cdot h(n)$) и итеративно улучшает его, уменьшая $\varepsilon$:

$$f(n) = g(n) + \varepsilon \cdot h(n), \quad \varepsilon \geq 1$$

При $\varepsilon = 1$ гарантируется оптимальность.

## Сложность

Инкрементальные алгоритмы пересчитывают только затронутую часть графа. Амортизированная сложность значительно ниже полного пересчёта, особенно при малых изменениях.

## API

Пространство имён `AI.Algorithms.DynamicPathfinding`. `DStarLite` и `LPAStar` работают **по сетке** (не по произвольному графу), `ARAStar` — по `GraphW<T>`. Метода `UpdateEdge(u, v, newWeight)` нет: изменения задаются через блокировку клетки.

| Член | Описание |
|------|----------|
| `DStarLite(int width, int height, (int X, int Y) start, (int X, int Y) goal)` | Планировщик по сетке |
| `.SetBlocked(x, y, blocked)` | Изменить проходимость клетки |
| `.Replan()` | Инкрементальный пересчёт после изменений |
| `.GetPath()` | `List<(int X, int Y)>` — текущий путь |
| `LPAStar(int width, int height, start, goal)` | То же для неподвижного наблюдателя |
| `.UpdateEdgeCost(x, y, blocked)` | Изменить клетку |
| `.ComputeShortestPath()` | Пересчёт |
| `.GetPath()` | Путь |
| `ARAStar<Edge>(GraphW<Edge> g, int start, int goal, Func<int,double> h, double initialEpsilon)` | Anytime-поиск |
| `.ImprovePath()` | Улучшить решение при текущем $\varepsilon$ |
| `.DecreaseEpsilon(delta)` | Понизить $\varepsilon$ — путь становится ближе к оптимальному |
| `.Epsilon`, `.PathCost`, `.GetPath()` | Текущая гарантия, стоимость и путь |

Разница между `DStarLite` и `LPAStar` в API — только в названии метода пересчёта: `Replan()` против `ComputeShortestPath()`.

Исходники: `src/AI.Algorithms/DynamicPathfinding/`.

## Код

D* Lite: робот узнаёт о препятствии уже в пути и перепланирует, не пересчитывая всё с нуля.

```csharp
using AI.Algorithms.DynamicPathfinding;

var dstar = new DStarLite(20, 20, start: (0, 0), goal: (19, 19));
var initial = dstar.GetPath();
Console.WriteLine($"Исходный путь: {initial.Count} клеток");

// Сенсор обнаружил стену поперёк маршрута
for (int y = 0; y < 15; y++)
    dstar.SetBlocked(10, y, true);

dstar.Replan();
var replanned = dstar.GetPath();
Console.WriteLine($"После перепланирования: {replanned.Count} клеток");
```

ARA*: сначала быстрый путь с гарантией $\varepsilon$, затем последовательное улучшение, пока есть время.

```csharp
using AI.Algorithms.DynamicPathfinding;
using AI.Algorithms.EWG;

var g = new GraphW<Edge>(100);
// ... заполнение графа ...

var ara = new ARAStar<Edge>(g, start: 0, goal: 99,
    heuristic: v => Math.Abs(v - 99), initialEpsilon: 3.0);

ara.ImprovePath();
Console.WriteLine($"ε = {ara.Epsilon:F1}, стоимость = {ara.PathCost:F1}");

// Есть ещё время — сужаем гарантию
while (ara.Epsilon > 1.0)
{
    ara.DecreaseEpsilon(0.5);
    ara.ImprovePath();
    Console.WriteLine($"ε = {ara.Epsilon:F1}, стоимость = {ara.PathCost:F1}");
}
// При ε = 1 путь оптимален
```

