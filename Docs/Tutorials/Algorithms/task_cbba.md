# CBBA (Consensus-Based Bundle Algorithm)

## Постановка задачи

Дано $N$ агентов и $M$ задач. Каждый агент может выполнить несколько задач (пакет). Требуется распределить задачи так, чтобы максимизировать суммарную полезность, используя только **локальные** коммуникации между агентами. CBBA обеспечивает согласованное распределение без центрального координатора.

## Идея алгоритма (Choi, Brunet, How, 2009)

CBBA состоит из двух чередующихся фаз:

### Фаза 1: Построение пакета (Bundle Build)

Каждый агент $i$ жадно добавляет задачу $j^*$ в свой пакет $B_i$:

$$j^* = \arg\max_{j \notin B_i} c_{ij}(B_i \cup \{j\})$$

где $c_{ij}$ — маржинальная ценность задачи $j$ при текущем пакете $B_i$.

### Фаза 2: Консенсус

Агенты обмениваются информацией о ставках и побеждающих агентах:

- Вектор побеждающих ставок: $y_j$ — лучшая известная ставка на задачу $j$
- Вектор победителей: $z_j$ — агент с лучшей ставкой

Правила обновления при получении информации от агента $k$:

$$\text{if } y_j^k > y_j^i: \quad y_j^i \leftarrow y_j^k, \; z_j^i \leftarrow z_j^k$$

Если агент $i$ потерял задачу — удалить её из пакета и перестроить.

## Сходимость

CBBA гарантирует сходимость за $O(N \cdot M)$ итераций при условии **убывающей маржинальности** (Diminishing Marginal Gain, DMG):

$$c_{ij}(S \cup \{j\}) \leq c_{ij}(S' \cup \{j\}) \quad \text{если } S \supseteq S'$$

## Свойства

| Свойство | Значение |
|----------|----------|
| Оптимальность | 50% от OPT (при DMG) |
| Коммуникация | Локальная (соседи) |
| Масштабируемость | $O(NM)$ за итерацию |

## API

Пространство имён `AI.Algorithms.TaskAllocation`. Класс называется `CBBA`; **граф связи в API не передаётся** — реализация считает связность полной.

| Член | Описание |
|------|----------|
| `CBBA(List<AgentDef>, List<TaskDef>, int maxIterations = 100)` | Consensus-Based Bundle Algorithm |
| `GreedyAllocation(List<AgentDef>, List<TaskDef>)` | Жадное назначение — базовая линия для сравнения |
| `.Solve()` | `AllocationResult` |

Размер пакета задаётся полем `AgentDef.Capacity`: именно оно ограничивает, сколько задач может взять один агент.

Исходники: `src/AI.Algorithms/TaskAllocation/CBBA.cs`, `GreedyAllocation.cs`.

## Код

```csharp
using AI.Algorithms.TaskAllocation;

var rng = new Random(42);

var agents = Enumerable.Range(0, 4)
    .Select(i => new AgentDef
    {
        Id = i,
        X = rng.NextDouble() * 10,
        Y = rng.NextDouble() * 10,
        Capacity = 2,          // размер пакета: не более двух задач на агента
    })
    .ToList();

var tasks = Enumerable.Range(0, 8)
    .Select(i => new TaskDef { Id = i, X = rng.NextDouble() * 10, Y = rng.NextDouble() * 10, Value = rng.Next(1, 10) })
    .ToList();

var cbba   = new CBBA(agents, tasks, maxIterations: 100).Solve();
var greedy = new GreedyAllocation(agents, tasks).Solve();

Console.WriteLine($"CBBA:   стоимость={cbba.TotalCost:F2}, назначено={cbba.Assignments.Count}");
Console.WriteLine($"Greedy: стоимость={greedy.TotalCost:F2}, назначено={greedy.Assignments.Count}");

// Пакеты по агентам
foreach (var group in cbba.Assignments.GroupBy(a => a.AgentId))
    Console.WriteLine($"  агент {group.Key}: [{string.Join(", ", group.Select(g => g.TaskId))}]");

// Суммарная вместимость 4×2 = 8 равна числу задач: при меньшей
// вместимости часть задач останется в UnassignedTasks
Console.WriteLine($"Без исполнителя: {cbba.UnassignedTasks}");
```

