# Приоритетные методы MAPF

## Постановка задачи

В задаче MAPF для $k$ агентов на графе приоритетные методы назначают каждому агенту приоритет и планируют пути последовательно: агенты с более высоким приоритетом рассматриваются как динамические препятствия для остальных. Это жертвует оптимальностью ради скорости.

## Алгоритмы

### PBS (Priority-Based Search)

Двухуровневый поиск: верхний уровень определяет частичное упорядочение агентов, нижний планирует пути с учётом приоритетов. При конфликте $(a_i, a_j)$ ветвление: $a_i \succ a_j$ или $a_j \succ a_i$.

### ICTS (Increasing Cost Tree Search)

Двухуровневый поиск: верхний уровень перебирает распределения стоимостей по агентам, нижний проверяет совместимость путей с заданными стоимостями.

Стоимость $k$ агентов представляется вектором $(\Delta_1, \ldots, \Delta_k)$, где $\Delta_i$ — удлинение пути $i$-го агента.

### PIBT (Priority Inheritance with Backtracking)

Одношаговый итеративный алгоритм. На каждом временном шаге агенты выбирают следующую вершину в порядке приоритетов. При конфликте приоритет **наследуется** блокирующему агенту.

### Token Passing

Агенты планируют по одному, передавая «токен». Агент с токеном планирует путь, избегая текущих позиций других агентов.

## Сравнение

| Метод | Полнота | Оптимальность | Масштабируемость |
|-------|---------|---------------|-----------------|
| PBS | Да | Нет | Средняя |
| ICTS | Да | Да | Низкая |
| PIBT | Да* | Нет | Высокая |
| Token Passing | Да* | Нет | Высокая |

\* При определённых условиях на граф.

## API

Пространство имён `AI.Algorithms.MAPF`. Класса `PriorityBasedSearch` нет — приоритетный поиск в дереве это `PBS`, а однотактовые алгоритмы вынесены отдельно.

| Член | Описание |
|------|----------|
| `PIBT(GridMap map, List<MAPFAgent> agents, int maxTimesteps = 200)` | Priority Inheritance with Backtracking |
| `TokenPassing(GridMap map, List<MAPFAgent> agents)` | Передача токена |
| `.Solve()` | `MAPFSolution` |

Оба алгоритма планируют на **один такт вперёд**: приоритеты пересчитываются каждый шаг, поэтому решение получается быстро, но без гарантии оптимальности.

Исходники: `src/AI.Algorithms/MAPF/PIBT.cs`, `TokenPassing.cs`.

## Код

```csharp
using AI.Algorithms.MAPF;

var map = new GridMap(12, 12);
map.SetBlocked(6, 6, true);

var agents = new List<MAPFAgent>();
for (int i = 0; i < 6; i++)
    agents.Add(new MAPFAgent
    {
        Id = i,
        StartX = i, StartY = 0,
        GoalX = 11 - i, GoalY = 11,
    });

var pibt  = new PIBT(map, agents, maxTimesteps: 300).Solve();
var token = new TokenPassing(map, agents).Solve();

Console.WriteLine($"PIBT:         makespan={pibt.Makespan}, валидно={pibt.IsValid(map, agents)}");
Console.WriteLine($"TokenPassing: makespan={token.Makespan}, валидно={token.IsValid(map, agents)}");

// maxTimesteps — предохранитель от зацикливания: если агенты
// не разошлись за это число тактов, решение придёт неполным
```

