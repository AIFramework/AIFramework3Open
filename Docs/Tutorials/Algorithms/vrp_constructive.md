# Конструктивные эвристики VRP

## Постановка задачи

Vehicle Routing Problem (VRP): дан депо, $n$ клиентов с координатами и спросом, $m$ транспортных средств с ограниченной вместимостью $Q$. Требуется построить маршруты минимальной суммарной длины, обслуживающие всех клиентов. Конструктивные эвристики строят начальное допустимое решение.

## Clarke—Wright Savings

Основная идея — объединение маршрутов по принципу максимальной экономии.

### Формула экономии

$$S_{ij} = d_{0i} + d_{0j} - d_{ij}$$

где $d_{0i}$ — расстояние от депо до клиента $i$, $d_{ij}$ — расстояние между клиентами $i$ и $j$.

### Псевдокод

```
ClarkeWright(clients, depot, Q):
  Вычислить S_ij для всех пар (i, j)
  Отсортировать S по убыванию
  routes ← по одному маршруту (depot → i → depot) для каждого i
  for (i, j) в порядке убывания S_ij:
    if i и j — концевые клиенты разных маршрутов:
      if объединение не нарушает Q:
        объединить маршруты через ребро (i, j)
```

## Sweep (метод развёртки)

1. Присвоить каждому клиенту полярный угол относительно депо
2. Сканировать клиентов по возрастанию угла
3. Добавлять в текущий маршрут, пока не нарушена вместимость $Q$
4. Оптимизировать каждый маршрут (TSP)

## Solomon I1 (для VRPTW)

Вставочная эвристика для VRP с временными окнами. Критерий вставки:

$$c_1(i, u, j) = \alpha_1 (d_{iu} + d_{uj} - \mu \cdot d_{ij}) + \alpha_2 \cdot \text{PushForward}(u)$$

Клиент $u^*$ с максимальным значением $c_2(u) = \lambda \cdot d_{0u} - c_1(u)$ вставляется первым.

## Сложность

| Алгоритм | Время |
|----------|-------|
| Clarke—Wright | $O(n^2 \log n)$ |
| Sweep | $O(n \log n)$ |
| Solomon I1 | $O(n^2 \cdot r)$ |

где $r$ — число маршрутов.

## API

Пространство имён `AI.Algorithms.VRP`. Экземпляр задачи передаётся в конструктор, решение возвращает `Solve()`.

| Член | Описание |
|------|----------|
| `VRPInstance(double depotX, double depotY, double[] custX, double[] custY, double[] demand, double vehicleCapacity, int numVehicles)` | Экземпляр задачи |
| `.Distance(i, j)`, `.DistanceMatrix` | Расстояния между клиентами |
| `ClarkeWright(VRPInstance inst)` | Метод сбережений |
| `Sweep(VRPInstance inst)` | Развёртка по полярному углу |
| `SolomonInsertion(VRPInstance inst, double[] readyTime = null, …)` | Вставка I1, с поддержкой временных окон |
| `.Solve()` | `VRPSolution` |
| `VRPSolution.Routes` | `List<List<int>>` — индексы клиентов по маршрутам, **без депо** |
| `.TotalDistance(inst)` | Суммарная длина с учётом выезда из депо и возврата |
| `.IsValid(inst)` | Проверка вместимости и покрытия всех клиентов |

Исходники: `src/AI.Algorithms/VRP/`.

## Код

```csharp
using AI.Algorithms.VRP;

var rng = new Random(42);
int n = 12;
var cx = new double[n];
var cy = new double[n];
var demand = new double[n];
for (int i = 0; i < n; i++)
{
    cx[i] = rng.NextDouble() * 20 - 10;
    cy[i] = rng.NextDouble() * 20 - 10;
    demand[i] = rng.Next(1, 15);
}

var inst = new VRPInstance(
    depotX: 0, depotY: 0,
    custX: cx, custY: cy, demand: demand,
    vehicleCapacity: 50, numVehicles: 3);

var cw = new ClarkeWright(inst).Solve();

Console.WriteLine($"Маршрутов: {cw.Routes.Count}");
Console.WriteLine($"Длина: {cw.TotalDistance(inst):F1}");
Console.WriteLine($"Допустимо: {cw.IsValid(inst)}");

for (int r = 0; r < cw.Routes.Count; r++)
{
    double load = cw.Routes[r].Sum(c => demand[c]);
    Console.WriteLine($"  депо -> {string.Join(" -> ", cw.Routes[r])} -> депо (загрузка {load})");
}
```

Три конструктивные эвристики на одной задаче дают заметно разные результаты — начальное решение стоит выбирать замером, а не по умолчанию:

```csharp
Console.WriteLine($"Clarke-Wright: {new ClarkeWright(inst).Solve().TotalDistance(inst):F1}");
Console.WriteLine($"Sweep:         {new Sweep(inst).Solve().TotalDistance(inst):F1}");
Console.WriteLine($"Solomon I1:    {new SolomonInsertion(inst).Solve().TotalDistance(inst):F1}");
```

