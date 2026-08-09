# DCOP-решатели

## Постановка задачи

Distributed Constraint Optimization Problem (DCOP): $N$ агентов, каждый контролирует переменную $x_i$ из области $D_i$. Заданы функции стоимости $f_{ij}(x_i, x_j)$ между парами связанных агентов. Требуется найти назначение, минимизирующее суммарную стоимость:

$$\min \sum_{(i,j) \in E} f_{ij}(x_i, x_j)$$

Агенты обмениваются сообщениями, не имея глобального доступа к задаче.

## Алгоритмы

### ADOPT (Asynchronous Distributed OPTimization)

- Агенты организованы в дерево (DFS-дерево ограничений)
- Каждый агент передаёт **нижние границы** (COST-сообщения) вверх и **пороги** вниз
- Гарантирует оптимальность при завершении

### DPOP (Distributed Pseudotree Optimization)

1. **UTIL-фаза**: снизу вверх передаются утилитарные таблицы
2. **VALUE-фаза**: сверху вниз передаются оптимальные значения

Оптимален, но экспоненциален по ширине дерева.

### Max-Sum (передача сообщений)

Основан на **факторном графе** и алгоритме belief propagation:

$$q_{i \to f}(x_i) = \sum_{g \in N(i) \setminus f} r_{g \to i}(x_i)$$

$$r_{f \to i}(x_i) = \max_{x_{N(f) \setminus i}} \left[ f(x_{N(f)}) + \sum_{j \in N(f) \setminus i} q_{j \to f}(x_j) \right]$$

### DSA (Distributed Stochastic Algorithm)

На каждом шаге агент с вероятностью $p$ переключается на значение, минимизирующее локальную стоимость.

### MGM (Maximum Gain Message)

Каждый агент вычисляет максимальный выигрыш от смены значения и делится им с соседями. Меняет значение, только если его выигрыш максимален среди соседей.

## Сравнение

| Алгоритм | Оптимальность | Сообщений | Память |
|----------|--------------|-----------|--------|
| ADOPT | Да | Экспоненциально | $O(1)$ |
| DPOP | Да | $O(N)$ | Экспоненциальная |
| Max-Sum | Приближённо | Полиномиально | $O(|D|)$ |
| DSA / MGM | Нет | $O(|E|)$ за итерацию | $O(1)$ |

## API

Пространство имён `AI.Algorithms.TaskAllocation` (не `AI.DCOP`), классы без суффикса `Solver`. Граф ограничений отдельным типом не передаётся: задача формулируется теми же `AgentDef`/`TaskDef`, что и в аукционных методах.

| Член | Описание |
|------|----------|
| `ADOPT(List<AgentDef>, List<TaskDef>, int maxCycles = 200)` | Асинхронный поиск с оценками; полный |
| `DPOP(List<AgentDef>, List<TaskDef>)` | Динамическое программирование по псевдодереву; полный |
| `MaxSum(List<AgentDef>, List<TaskDef>, int maxIterations = 50)` | Передача сообщений по факторному графу; неполный |
| `DSA(List<AgentDef>, List<TaskDef>, …)` | Стохастический локальный поиск; неполный |
| `MGM(List<AgentDef>, List<TaskDef>, int maxIterations = 100)` | Максимальный выигрыш; неполный |
| `.Solve()` | `AllocationResult` |

Полные алгоритмы (ADOPT, DPOP) гарантируют оптимум, но растут экспоненциально по ширине дерева; неполные (Max-Sum, DSA, MGM) работают за фиксированное число итераций без гарантии.

Исходники: `src/AI.Algorithms/TaskAllocation/`.

## Код

```csharp
using AI.Algorithms.TaskAllocation;

var rng = new Random(42);

var agents = Enumerable.Range(0, 4)
    .Select(i => new AgentDef { Id = i, X = rng.NextDouble() * 10, Y = rng.NextDouble() * 10, Capacity = 2 })
    .ToList();

var tasks = Enumerable.Range(0, 6)
    .Select(i => new TaskDef { Id = i, X = rng.NextDouble() * 10, Y = rng.NextDouble() * 10, Value = rng.Next(1, 10) })
    .ToList();

// Полный алгоритм: оптимум, но цена растёт экспоненциально
var exact = new DPOP(agents, tasks).Solve();
Console.WriteLine($"DPOP (полный): стоимость={exact.TotalCost:F2}");

// Неполные: фиксированный бюджет итераций
foreach (var (name, r) in new (string, AllocationResult)[]
{
    ("Max-Sum", new MaxSum(agents, tasks, maxIterations: 50).Solve()),
    ("MGM",     new MGM(agents, tasks, maxIterations: 50).Solve()),
    ("DSA",     new DSA(agents, tasks).Solve()),
})
{
    double gap = exact.TotalCost > 0 ? (r.TotalCost - exact.TotalCost) / exact.TotalCost : 0;
    Console.WriteLine($"{name,-8} стоимость={r.TotalCost,7:F2}  отставание от оптимума={gap:P1}");
}
```

