# Потоки минимальной стоимости

## Постановка задачи

Дана сеть $G = (V, E)$ с пропускными способностями $c(u,v)$, стоимостями $a(u,v)$ за единицу потока и требуемой величиной потока $F$. Требуется найти поток величины $F$ с минимальной суммарной стоимостью:

$$\min \sum_{(u,v) \in E} a(u,v) \cdot f(u,v)$$

при ограничениях $0 \leq f(u,v) \leq c(u,v)$ и условиях сохранения потока.

## Алгоритмы

### Successive Shortest Paths

Итеративно находит кратчайший (по стоимости) увеличивающий путь в остаточной сети и проталкивает поток вдоль него.

```
SSP(G, s, t, F):
  flow ← 0, cost ← 0
  while flow < F:
    path ← кратчайший путь в остаточной сети (Беллман—Форд)
    if path не существует: break
    δ ← min(F - flow, bottleneck(path))
    flow ← flow + δ
    cost ← cost + δ · dist(path)
    обновить остаточную сеть
```

### Cycle-Canceling

Находит допустимый поток, затем итеративно отменяет отрицательные циклы в остаточной сети.

### Cost Scaling

Масштабирование стоимостей с параметром $\varepsilon$. Поддерживает $\varepsilon$-оптимальность и постепенно уменьшает $\varepsilon$.

## Сложность

| Алгоритм | Время |
|----------|-------|
| Successive Shortest Paths | $O(F \cdot VE)$ |
| Cycle-Canceling | $O(V E^2 C)$ |
| Cost Scaling | $O(V^2 E \log(VC))$ |

где $C$ — максимальная стоимость ребра.

## API

Пространство имён `AI.Algorithms.NetworkFlow`. Три класса с одинаковым интерфейсом; сеть задаётся не отдельным объектом, а прямо в решателе.

| Член | Описание |
|------|----------|
| `SuccessiveShortestPaths(int v)` | Последовательные кратчайшие пути |
| `CycleCanceling(int v)` | Устранение отрицательных циклов |
| `CostScaling(int v)` | Масштабирование стоимостей |
| `.AddEdge(int from, int to, int capacity, double cost)` | Дуга: **пропускная способность целая**, стоимость вещественная |
| `.Solve(int s, int t)` | `(double flow, double cost)` — максимальный поток и его минимальная стоимость |

Заданного объёма поставки (`demand`) в API нет: решатели гонят **максимальный** поток и минимизируют его стоимость.

Исходники: `src/AI.Algorithms/NetworkFlow/`.

## Код

```csharp
using AI.Algorithms.NetworkFlow;

var mcf = new SuccessiveShortestPaths(5);
mcf.AddEdge(0, 1, capacity: 4, cost: 1);
mcf.AddEdge(0, 2, capacity: 3, cost: 2);
mcf.AddEdge(1, 3, capacity: 2, cost: 3);
mcf.AddEdge(2, 3, capacity: 5, cost: 1);
mcf.AddEdge(3, 4, capacity: 6, cost: 2);

var (flow, cost) = mcf.Solve(s: 0, t: 4);
Console.WriteLine($"Поток: {flow:F0}, стоимость: {cost:F2}");
Console.WriteLine($"Удельная стоимость: {cost / flow:F2} на единицу");
```

Три алгоритма дают одну и ту же оптимальную стоимость — различаются лишь скоростью сходимости:

```csharp
foreach (var name in new[] { "SSP", "CycleCanceling", "CostScaling" })
{
    dynamic solver = name switch
    {
        "CycleCanceling" => new CycleCanceling(5),
        "CostScaling"    => new CostScaling(5),
        _                => new SuccessiveShortestPaths(5),
    };

    solver.AddEdge(0, 1, 4, 1.0); solver.AddEdge(0, 2, 3, 2.0);
    solver.AddEdge(1, 3, 2, 3.0); solver.AddEdge(2, 3, 5, 1.0);
    solver.AddEdge(3, 4, 6, 2.0);

    var r = solver.Solve(0, 4);
    Console.WriteLine($"{name,-15} поток={r.flow:F0} стоимость={r.cost:F2}");
}
```

