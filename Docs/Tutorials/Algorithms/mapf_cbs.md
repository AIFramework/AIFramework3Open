# Conflict-Based Search (CBS) для MAPF

## Постановка задачи

Multi-Agent Path Finding (MAPF): даны $k$ агентов на графе $G$, каждый с начальной и целевой вершинами. Необходимо найти бесконфликтные пути для всех агентов, минимизируя суммарную стоимость (Sum-of-Costs) или makespan. Агенты не могут одновременно занимать одну вершину или менять местами.

## Алгоритм CBS (Sharon et al., 2015)

Двухуровневый поиск:

**Верхний уровень** — дерево конфликтов (Constraint Tree, CT):
- Каждый узел CT содержит набор ограничений и соответствующие пути
- При обнаружении конфликта узел разветвляется на два потомка с новыми ограничениями

**Нижний уровень** — A* с ограничениями:
- Для каждого агента ищется кратчайший путь, удовлетворяющий ограничениям

### Типы конфликтов

- **Вершинный**: агенты $a_i$ и $a_j$ в вершине $v$ в момент $t$
- **Рёберный**: агенты пересекают ребро $(u,v)$ в противоположных направлениях в момент $t$

### Ветвление

При конфликте $(a_i, a_j, v, t)$ создаются два узла CT:
- Ограничение $\langle a_i, v, t \rangle$ — запрет для $a_i$ на $v$ в момент $t$
- Ограничение $\langle a_j, v, t \rangle$ — запрет для $a_j$ на $v$ в момент $t$

## Варианты

| Метод | Оптимальность | Особенность |
|-------|--------------|-------------|
| CBS | Оптимальный | Базовый |
| ICBS | Оптимальный | Улучшенные эвристики CT |
| ECBS | $\varepsilon$-субоптимальный | Focal search, быстрее CBS |

## Сложность

CBS оптимален, но в худшем случае экспоненциален по числу агентов.

## API

Пространство имён `AI.Algorithms.MAPF`. Класс называется `CBS` (не `ConflictBasedSearch`), карта задаётся сеткой `GridMap`, а не графом.

| Член | Описание |
|------|----------|
| `GridMap(int width, int height)` | Сетка; `.SetBlocked(x, y, true)` — препятствие |
| `MAPFAgent` | `Id`, `StartX`, `StartY`, `GoalX`, `GoalY` |
| `CBS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)` | Conflict-Based Search |
| `ECBS(GridMap map, List<MAPFAgent> agents, double suboptimalityBound = 1.5, …)` | Ограниченно-субоптимальный вариант |
| `PBS(GridMap map, List<MAPFAgent> agents, int timeLimit = 1000)` | Приоритетный поиск |
| `.Solve()` | `MAPFSolution` |
| `MAPFSolution.Paths` | `List<List<(int X, int Y)>>` — путь каждого агента по тактам |
| `.Makespan` | Длина самого долгого пути |
| `.SumOfCosts` | Сумма длин путей |
| `.IsValid(map, agents)` | Проверка бесконфликтности решения |

`timeLimit` — предел числа раскрытых узлов дерева ограничений, а не время в миллисекундах.

Исходники: `src/AI.Algorithms/MAPF/`.

## Код

```csharp
using AI.Algorithms.MAPF;

var map = new GridMap(10, 10);
for (int y = 2; y < 8; y++) map.SetBlocked(5, y, true);   // стена с проходом

var agents = new List<MAPFAgent>
{
    new() { Id = 0, StartX = 0, StartY = 0, GoalX = 9, GoalY = 9 },
    new() { Id = 1, StartX = 9, StartY = 0, GoalX = 0, GoalY = 9 },
    new() { Id = 2, StartX = 0, StartY = 9, GoalX = 9, GoalY = 0 },
};

var solution = new CBS(map, agents, timeLimit: 5000).Solve();

Console.WriteLine($"Makespan: {solution.Makespan}, SumOfCosts: {solution.SumOfCosts}");
Console.WriteLine($"Бесконфликтно: {solution.IsValid(map, agents)}");

foreach (var path in solution.Paths)
    Console.WriteLine(string.Join(" ", path.Select(c => $"({c.X},{c.Y})")));
```

ECBS торгует оптимальность на скорость: решение не хуже, чем в `suboptimalityBound` раз от оптимума.

```csharp
var optimal   = new CBS(map, agents, 5000).Solve();
var bounded   = new ECBS(map, agents, suboptimalityBound: 1.5).Solve();

Console.WriteLine($"CBS  SumOfCosts = {optimal.SumOfCosts}");
Console.WriteLine($"ECBS SumOfCosts = {bounded.SumOfCosts} (гарантия ≤ 1.5×)");
```

