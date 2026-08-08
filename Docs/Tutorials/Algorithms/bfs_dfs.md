# BFS и DFS: обход графа в ширину и в глубину

## Постановка задачи

Дан граф $G = (V, E)$ и стартовая вершина $s$. Необходимо обойти все достижимые вершины, определив расстояния (BFS) или порядок обхода (DFS). Оба алгоритма являются фундаментальными строительными блоками для большинства графовых задач.

## Теория

**BFS (поиск в ширину)** использует очередь FIFO. Вершины обрабатываются слоями — сначала все соседи на расстоянии 1, затем на расстоянии 2 и т.д.

**DFS (поиск в глубину)** использует стек (или рекурсию). Алгоритм идёт «вглубь» по одной ветви, пока не достигнет тупика, затем возвращается.

### Псевдокод BFS

```
BFS(G, s):
  queue ← {s}, dist[s] ← 0
  while queue не пуста:
    u ← queue.dequeue()
    for v ∈ adj(u):
      if v не посещена:
        dist[v] ← dist[u] + 1
        queue.enqueue(v)
```

### Псевдокод DFS

```
DFS(G, u):
  visited[u] ← true
  for v ∈ adj(u):
    if not visited[v]:
      DFS(G, v)
```

## Сложность

| Алгоритм | Время | Память |
|----------|-------|--------|
| BFS | $O(V + E)$ | $O(V)$ |
| DFS | $O(V + E)$ | $O(V)$ |

Расстояние, вычисляемое BFS:

$$d(s, v) = \text{минимальное число рёбер от } s \text{ до } v$$

## API

Пространство имён `AI.Algorithms.EWG`. Обход выполняется в конструкторе — результат доступен сразу после создания объекта.

| Член | Описание |
|------|----------|
| `Graph(int numV)` | Невзвешенный граф на `numV` вершинах |
| `Graph.AddEdge(i, j)` | Неориентированное ребро |
| `Graph.AddArc(i, j)` | Ориентированная дуга |
| `BFS(Graph g, int start)` | Обход в ширину из вершины `start` |
| `BFS.Visited` | `bool[]`: достижима ли вершина |
| `BFS.DistanceTo` | `int[]`: число рёбер до вершины |
| `BFS.PathTo(v)` | Последовательность вершин пути |
| `DFS(Graph g, int start)` | Обход в глубину |
| `DFS.Visited`, `DFS.EdgeTo`, `DFS.PathTo(v)` | Аналогично BFS, но без `DistanceTo` |

Исходники: `src/AI.Algorithms/EWG/BFS.cs`, `src/AI.Algorithms/EWG/DFS.cs`.

## Код

```csharp
using AI.Algorithms.EWG;

var g = new Graph(6);
g.AddEdge(0, 1); g.AddEdge(0, 2); g.AddEdge(1, 3);
g.AddEdge(2, 4); g.AddEdge(3, 5); g.AddEdge(4, 5);

var bfs = new BFS(g, 0);
var dfs = new DFS(g, 0);

// BFS даёт кратчайший путь по числу рёбер, DFS — произвольный
Console.WriteLine($"BFS: {string.Join("->", bfs.PathTo(5))}, рёбер: {bfs.DistanceTo[5]}");
Console.WriteLine($"DFS: {string.Join("->", dfs.PathTo(5))}");

// Компонента связности вершины 0
int reachable = bfs.Visited.Count(v => v);
Console.WriteLine($"Достижимо вершин: {reachable} из {g.V}");
```

