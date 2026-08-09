# Аукционные методы распределения задач

## Постановка задачи

Дано множество агентов $A = \{a_1, \ldots, a_N\}$ и множество задач $T = \{t_1, \ldots, t_M\}$. Каждый агент $a_i$ оценивает стоимость выполнения задачи $t_j$ как $c_{ij}$. Требуется распределить задачи между агентами, минимизируя суммарную стоимость. Аукционные методы решают эту задачу **децентрализованно**.

## Contract Net Protocol (CNP)

Классический протокол (Smith, 1980):

1. **Менеджер** объявляет задачу (call for proposals)
2. **Подрядчики** отправляют заявки (bids) со своими оценками
3. Менеджер выбирает лучшую заявку и заключает контракт
4. Подрядчик выполняет задачу и отчитывается

### Псевдокод

```
CNP(manager, task, contractors):
  manager.broadcast(task)
  bids ← собрать заявки от contractors
  winner ← argmin(bids)
  manager.award(winner, task)
  winner.execute(task)
```

## SSI (Sequential Single-Item Auction)

Задачи распределяются по одной:

1. Объявить одну задачу
2. Собрать ставки, назначить победителю
3. Повторить для следующей задачи

Маржинальная стоимость агента $a_i$ на задачу $t_j$:

$$\text{bid}_{ij} = c_i(B_i \cup \{t_j\}) - c_i(B_i)$$

где $B_i$ — текущий пакет задач агента $a_i$.

## Параллельные аукционы

Несколько задач объявляются одновременно. Агенты ставят на комбинации задач (комбинаторный аукцион), что позволяет учитывать синергию.

## Свойства

| Метод | Коммуникация | Оптимальность | Децентрализация |
|-------|-------------|---------------|----------------|
| CNP | $O(NM)$ | Нет | Полная |
| SSI | $O(NM)$ | Приближённая | Полная |
| Комбинаторный | $O(N \cdot 2^M)$ | Лучше | Полная |

## API

Пространство имён `AI.Algorithms.TaskAllocation`. Единого `AuctionTaskAllocator` нет — каждый протокол отдельный класс с одинаковой сигнатурой.

| Член | Описание |
|------|----------|
| `AgentDef` | `Id`, `X`, `Y`, `Capacity` (по умолчанию 1), `Capabilities` |
| `TaskDef` | `Id`, `X`, `Y`, `Value` |
| `ContractNet(List<AgentDef>, List<TaskDef>)` | Протокол контрактных сетей |
| `SSIAuction(List<AgentDef>, List<TaskDef>)` | Последовательный однопредметный аукцион |
| `SequentialAuction(List<AgentDef>, List<TaskDef>)` | Последовательный аукцион |
| `.Solve()` | `AllocationResult` |
| `AllocationResult.Assignments` | `List<(int AgentId, int TaskId)>` |
| `.TotalCost`, `.TotalValue`, `.UnassignedTasks` | Итоговые показатели |

Исходники: `src/AI.Algorithms/TaskAllocation/`.

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
        Capacity = 3,
    })
    .ToList();

var tasks = Enumerable.Range(0, 8)
    .Select(i => new TaskDef
    {
        Id = i,
        X = rng.NextDouble() * 10,
        Y = rng.NextDouble() * 10,
        Value = rng.Next(1, 10),
    })
    .ToList();

var result = new ContractNet(agents, tasks).Solve();

Console.WriteLine($"Назначено: {result.Assignments.Count} из {tasks.Count}");
Console.WriteLine($"Не назначено: {result.UnassignedTasks}");
Console.WriteLine($"Суммарная стоимость: {result.TotalCost:F2}");

foreach (var (agentId, taskId) in result.Assignments)
    Console.WriteLine($"  агент {agentId} <- задача {taskId}");
```

Три протокола на одних данных: разница в качестве и в числе «раундов торгов» — именно она определяет выбор для реальной сети со связью.

```csharp
foreach (var (name, r) in new (string, AllocationResult)[]
{
    ("ContractNet",       new ContractNet(agents, tasks).Solve()),
    ("SSI Auction",       new SSIAuction(agents, tasks).Solve()),
    ("SequentialAuction", new SequentialAuction(agents, tasks).Solve()),
})
    Console.WriteLine($"{name,-18} стоимость={r.TotalCost,7:F2}  " +
                      $"назначено={r.Assignments.Count}  без исполнителя={r.UnassignedTasks}");
```

