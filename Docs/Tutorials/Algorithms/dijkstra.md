# Алгоритм Дейкстры

## Постановка задачи

Дан взвешенный граф $G = (V, E)$ с неотрицательными весами рёбер и стартовая вершина $s$. Требуется найти кратчайшие расстояния от $s$ до всех остальных вершин. Алгоритм Дейкстры — классическое решение этой задачи.

## Теория

На каждом шаге извлекается вершина с минимальной текущей оценкой расстояния и выполняется **релаксация** всех исходящих рёбер.

### Формула релаксации

$$d[v] = \min(d[v],\; d[u] + w(u, v))$$

### Псевдокод

```
Dijkstra(G, s):
  d[s] ← 0, d[v] ← ∞ для всех v ≠ s
  PQ ← приоритетная очередь с (d[s], s)
  while PQ не пуста:
    u ← PQ.extractMin()
    for (v, w) ∈ adj(u):
      if d[u] + w < d[v]:
        d[v] ← d[u] + w
        prev[v] ← u
        PQ.decreaseKey(v, d[v])
```

## Сложность

| Реализация | Время |
|------------|-------|
| Наивная (массив) | $O(V^2)$ |
| Бинарная куча | $O((V + E) \log V)$ |
| Фибоначчиева куча | $O(V \log V + E)$ |

## Ограничения

Алгоритм **не работает** с отрицательными весами рёбер. Для таких случаев используйте алгоритм Беллмана—Форда.

## API

Пространство имён `AI.Algorithms.EWG`. Расчёт идёт в конструкторе.

| Член | Описание |
|------|----------|
| `GraphW<Edge>(int numV)` | Взвешенный граф |
| `GraphW.AddEdge(i, j, w)` | Неориентированное ребро веса `w` |
| `GraphW.AddArce(i, j, w)` | Ориентированная дуга веса `w` |
| `DijkstraSPath<Edge>(GraphW<Edge> g, int source)` | Кратчайшие пути из `source` |
| `.Distances` | `double[]`: расстояния; `double.MaxValue` — недостижима |
| `.Edges` | `Edge[]`: ребро, по которому пришли в вершину (`null` для источника) |

Путь восстанавливается обратным ходом по `Edges` — готового `GetPath` у класса нет.

Исходник: `src/AI.Algorithms/EWG/Dijkstra.cs`.

## Код

```csharp
using AI.Algorithms.EWG;

var g = new GraphW<Edge>(5);
g.AddEdge(0, 1, 4); g.AddEdge(0, 2, 1);
g.AddEdge(2, 1, 2); g.AddEdge(1, 3, 5);
g.AddEdge(2, 3, 8); g.AddEdge(3, 4, 3);

var dijkstra = new DijkstraSPath<Edge>(g, 0);

for (int v = 0; v < g.V; v++)
{
    double d = dijkstra.Distances[v];
    Console.WriteLine($"d[{v}] = {(d < double.MaxValue ? d.ToString("F1") : "∞")}");
}

// Восстановление пути 0 -> 4 обратным ходом по Edges
var path = new List<int>();
for (int v = 4; ; v = dijkstra.Edges[v].StartV)
{
    path.Insert(0, v);
    if (v == 0 || dijkstra.Edges[v] == null) break;
}
Console.WriteLine(string.Join("->", path));
```

