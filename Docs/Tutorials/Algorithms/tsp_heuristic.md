# TSP-эвристики

## Постановка задачи

Задача коммивояжёра (TSP): дан полный граф с $n$ вершинами и матрицей расстояний. Требуется найти гамильтонов цикл минимальной длины. TSP — NP-трудная задача, поэтому для больших $n$ используются эвристики.

## Алгоритм Кристофидеса (3/2-приближение)

Лучший из классических приближённых алгоритмов для метрического TSP:

1. Построить MST графа
2. Найти множество вершин нечётной степени $O$
3. Построить минимальное совершенное паросочетание на $O$
4. Объединить MST и паросочетание → эйлеров граф
5. Найти эйлеров обход и убрать повторы → гамильтонов цикл

$$\text{cost}(C) \leq \frac{3}{2} \cdot \text{OPT}$$

## Локальный поиск

### 2-opt

Удалить два ребра, перевернуть сегмент тура:

$$\Delta = d(i, j) + d(i', j') - d(i, i') - d(j, j')$$

Если $\Delta < 0$ — выполнить обмен.

### 3-opt

Удалить три ребра и рассмотреть все способы переподключения трёх сегментов ($8$ вариантов).

### Or-opt

Перемещение последовательности из $1$, $2$ или $3$ вершин в другую позицию тура.

### Lin—Kernighan

Адаптивный $k$-opt: на каждом шаге динамически определяется глубина перестановки. Критерий Лина—Кернигана:

$$G = \sum_{i=1}^{k} g_i > 0, \quad g_i = d(x_i) - d(y_i)$$

## Сложность (одна итерация)

| Метод | Время |
|-------|-------|
| 2-opt | $O(n^2)$ |
| 3-opt | $O(n^3)$ |
| Or-opt | $O(n^2)$ |
| Lin—Kernighan | $O(n^2)$ – $O(n^3)$ |

## API

Пространство имён `AI.Algorithms.VRP`. Единого `TSPSolver` нет: TSP задаётся как VRP с одним транспортом и неограниченной вместимостью.

| Член | Описание |
|------|----------|
| `LocalSearch(VRPInstance inst)` | Локальный поиск поверх готового решения |
| `.TwoOpt(sol)`, `.ThreeOpt(sol)`, `.OrOpt(sol)` | Соответствующие окрестности; возвращают улучшенное `VRPSolution` |
| `LinKernighan(VRPInstance inst)` | Переменная глубина обмена |
| `.Solve(VRPSolution initial = null)` | Улучшает `initial` или строит решение с нуля |
| `Christofides(VRPInstance inst)` | Приближение с гарантией 3/2 |
| `.SolveTSP()` | `List<int>` — тур; в `VRPSolution` его нужно завернуть вручную |

`Christofides.SolveTSP()` возвращает **список вершин**, а не `VRPSolution`, — это единственный метод с таким возвратом.

Исходники: `src/AI.Algorithms/VRP/`.

## Код

```csharp
using AI.Algorithms.VRP;

var rng = new Random(42);
int n = 20;
var cx = new double[n];
var cy = new double[n];
var demand = new double[n];
for (int i = 0; i < n; i++)
{
    cx[i] = rng.NextDouble() * 20 - 10;
    cy[i] = rng.NextDouble() * 20 - 10;
    demand[i] = 1;
}

// TSP = VRP с одним транспортом и заведомо избыточной вместимостью
var inst = new VRPInstance(0, 0, cx, cy, demand, vehicleCapacity: 1e9, numVehicles: 1);

var initial = new ClarkeWright(inst).Solve();
Console.WriteLine($"Начальное:      {initial.TotalDistance(inst):F1}");

var twoOpt = new LocalSearch(inst).TwoOpt(initial);
Console.WriteLine($"После 2-opt:    {twoOpt.TotalDistance(inst):F1}");

var lk = new LinKernighan(inst).Solve(initial);
Console.WriteLine($"Lin-Kernighan:  {lk.TotalDistance(inst):F1}");
```

Christofides даёт доказанную границу 3/2 от оптимума — но только для метрики, удовлетворяющей неравенству треугольника:

```csharp
var tour = new Christofides(inst).SolveTSP();
var sol  = new VRPSolution { Routes = new List<List<int>> { tour } };

Console.WriteLine($"Christofides:   {sol.TotalDistance(inst):F1}");
```

