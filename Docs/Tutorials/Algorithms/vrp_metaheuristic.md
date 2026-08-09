# Метаэвристики для VRP

## Постановка задачи

Задачи маршрутизации транспорта (VRP и варианты) — NP-трудные. Метаэвристики позволяют находить высококачественные решения за приемлемое время, управляя балансом между **интенсификацией** (улучшение текущего решения) и **диверсификацией** (исследование новых областей).

## ALNS (Adaptive Large Neighbourhood Search)

Итеративно разрушает и восстанавливает решение с помощью набора операторов:

- **Операторы разрушения**: Random Removal, Worst Removal, Shaw Removal
- **Операторы восстановления**: Greedy Insertion, Regret-$k$ Insertion

Вероятности выбора операторов адаптируются на основе их успешности:

$$w_i \leftarrow (1 - \rho) \cdot w_i + \rho \cdot \pi_i$$

где $\rho$ — скорость обучения, $\pi_i$ — награда оператора $i$.

## Генетический алгоритм (GA)

- **Хромосома**: перестановка клиентов + разбиение на маршруты
- **Кроссовер**: OX (Order Crossover), PMX
- **Мутация**: 2-opt, Or-opt, перемещение клиента между маршрутами
- **Селекция**: турнирная или рулеточная

## Табу-поиск

Локальный поиск с запретом (табу) на возврат к недавним решениям:

- **Табу-список** фиксированного размера $L$
- **Критерий аспирации**: снятие запрета при улучшении лучшего решения

## ACO (Ant Colony Optimization)

Муравьи строят решения вероятностно, основываясь на феромонах $\tau_{ij}$ и эвристике $\eta_{ij}$:

$$p_{ij} = \frac{\tau_{ij}^\alpha \cdot \eta_{ij}^\beta}{\sum_k \tau_{ik}^\alpha \cdot \eta_{ik}^\beta}$$

## Имитация отжига (SA)

Принятие ухудшающего решения с вероятностью $e^{-\Delta / T}$, где $T$ — температура, снижающаяся по расписанию.

## API

Пространство имён `AI.Algorithms.VRP`. Ограничения по времени в API нет — бюджет задаётся **числом итераций** в конструкторе.

| Член | Описание |
|------|----------|
| `GeneticVRP(inst, populationSize = 100, generations = 500, seed = 42)` | Генетический алгоритм; `.Solve()` |
| `TabuSearchVRP(inst, maxIterations = 3000, tabuTenure = 15, seed = 42)` | Табу-поиск; `.Solve(initial = null)` |
| `AntColony(inst, numAnts = 30, maxIterations = 200, …)` | Муравьиный алгоритм; `.Solve()` |
| `SimulatedAnnealingVRP(inst, initialTemp = 1000, …)` | Отжиг; `.Solve(initial = null)` |
| `ALNS(inst, maxIterations = 5000, seed = 42)` | Адаптивный поиск с разрушением/восстановлением; `.Solve(initial = null)` |

Все принимают `seed` — результат воспроизводим. Те, у кого `Solve` принимает `initial`, стартуют с готового решения: это обычно заметно лучше старта с нуля.

Исходники: `src/AI.Algorithms/VRP/`.

## Код

```csharp
using AI.Algorithms.VRP;

var rng = new Random(42);
int n = 25;
var cx = new double[n];
var cy = new double[n];
var demand = new double[n];
for (int i = 0; i < n; i++)
{
    cx[i] = rng.NextDouble() * 30 - 15;
    cy[i] = rng.NextDouble() * 30 - 15;
    demand[i] = rng.Next(1, 12);
}

var inst = new VRPInstance(0, 0, cx, cy, demand, vehicleCapacity: 60, numVehicles: 4);

// Конструктивная эвристика как стартовая точка для метаэвристик
var initial = new ClarkeWright(inst).Solve();
Console.WriteLine($"Clarke-Wright: {initial.TotalDistance(inst):F1}");

var alns = new ALNS(inst, maxIterations: 3000, seed: 42).Solve(initial);
var tabu = new TabuSearchVRP(inst, maxIterations: 2000, tabuTenure: 15, seed: 42).Solve(initial);
var ga   = new GeneticVRP(inst, populationSize: 60, generations: 300, seed: 42).Solve();

foreach (var (name, sol) in new[] { ("ALNS", alns), ("Tabu", tabu), ("GA", ga) })
    Console.WriteLine($"{name,-6} {sol.TotalDistance(inst),8:F1}  " +
                      $"маршрутов={sol.Routes.Count}  допустимо={sol.IsValid(inst)}");
```

Один и тот же `seed` даёт один и тот же результат — на этом строится честное сравнение алгоритмов:

```csharp
var a = new ALNS(inst, 1000, seed: 7).Solve(initial);
var b = new ALNS(inst, 1000, seed: 7).Solve(initial);
Console.WriteLine($"Совпадают: {Math.Abs(a.TotalDistance(inst) - b.TotalDistance(inst)) < 1e-9}");
```

