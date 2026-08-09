# Максимальный поток

## Постановка задачи

Дана сеть — ориентированный граф $G = (V, E)$ с функцией пропускной способности $c(u,v) \geq 0$, источником $s$ и стоком $t$. Требуется найти максимальный поток из $s$ в $t$, не превышающий пропускные способности рёбер.

## Теорема Форда—Фалкерсона

$$\text{max flow} = \text{min cut}$$

Величина максимального потока равна минимальной пропускной способности разреза, разделяющего $s$ и $t$.

## Алгоритмы

### Ford—Fulkerson (метод)

Итеративно ищет увеличивающие пути в остаточной сети $G_f$ и увеличивает поток вдоль них.

### Edmonds—Karp

Ford—Fulkerson с BFS для поиска увеличивающего пути. Гарантирует полиномиальное время.

### Алгоритм Диница

Использует слоистую сеть (layered network) и блокирующие потоки.

### Push—Relabel

Вместо увеличивающих путей использует операции **push** (проталкивание) и **relabel** (подъём метки).

## Сложность

| Алгоритм | Время |
|----------|-------|
| Ford—Fulkerson | $O(E \cdot f^*)$ |
| Edmonds—Karp | $O(V E^2)$ |
| Dinic | $O(V^2 E)$ |
| Push—Relabel | $O(V^2 E)$ |

где $f^*$ — величина максимального потока.

## Условие сохранения потока

$$\forall v \in V \setminus \{s, t\}: \quad \sum_{u} f(u,v) = \sum_{w} f(v,w)$$

## API

Пространство имён `AI.Algorithms.NetworkFlow`. Единого `MaxFlowSolver` нет — каждый алгоритм отдельный класс с одинаковой сигнатурой конструктора; поток считается прямо в нём.

| Член | Описание |
|------|----------|
| `FlowNetwork(int v)` | Сеть на `v` вершинах |
| `FlowNetwork.AddEdge(FlowEdge e)` | Добавить дугу |
| `FlowNetwork.AllEdges()` | Все дуги — после расчёта у них заполнено `Flow` |
| `FlowEdge(int from, int to, double capacity)` | Дуга; `.Flow`, `.Capacity`, `.ResidualCapacityTo(v)` |
| `FordFulkerson(net, s, t)` | Метод дополняющих путей; `.MaxFlow`, `.InCut(v)` |
| `EdmondsKarp(net, s, t)` | Дополняющие пути через BFS; `.MaxFlow` |
| `Dinic(net, s, t)` | Блокирующие потоки по слоистой сети; `.MaxFlow` |
| `PushRelabel(net, s, t)` | Проталкивание предпотока; `.MaxFlow` |

`InCut(v)` есть только у `FordFulkerson` — им удобно получить минимальный разрез сразу после расчёта потока.

Исходники: `src/AI.Algorithms/NetworkFlow/`.

## Код

```csharp
using AI.Algorithms.NetworkFlow;

var net = new FlowNetwork(6);
net.AddEdge(new FlowEdge(0, 1, 16));
net.AddEdge(new FlowEdge(0, 2, 13));
net.AddEdge(new FlowEdge(1, 3, 12));
net.AddEdge(new FlowEdge(2, 1, 4));
net.AddEdge(new FlowEdge(2, 4, 14));
net.AddEdge(new FlowEdge(3, 5, 20));
net.AddEdge(new FlowEdge(4, 3, 7));
net.AddEdge(new FlowEdge(4, 5, 4));

var dinic = new Dinic(net, s: 0, t: 5);
Console.WriteLine($"Максимальный поток: {dinic.MaxFlow:F0}");

// Насыщенные дуги — узкие места сети
foreach (var e in net.AllEdges())
    if (e.Flow >= e.Capacity - 1e-9)
        Console.WriteLine($"насыщена: {e.From}->{e.To} ({e.Flow}/{e.Capacity})");
```

Минимальный разрез по теореме Форда—Фалкерсона равен максимальному потоку:

```csharp
var ff = new FordFulkerson(net, s: 0, t: 5);

// InCut(v) == true — вершина осталась достижимой из истока
// по остаточной сети, то есть лежит на стороне истока
foreach (var e in net.AllEdges())
    if (ff.InCut(e.From) && !ff.InCut(e.To))
        Console.WriteLine($"в разрезе: {e.From}->{e.To}, вклад {e.Capacity}");
```

