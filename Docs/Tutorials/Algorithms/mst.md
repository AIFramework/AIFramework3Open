# Минимальные остовные деревья (MST)

## Постановка задачи

Дан связный неориентированный взвешенный граф $G = (V, E)$. Необходимо найти подмножество рёбер $T \subseteq E$, образующее дерево, соединяющее все вершины, с минимальным суммарным весом. MST применяется в проектировании сетей, кластеризации и аппроксимации TSP.

## Свойство разреза

Для любого разреза графа $(S, V \setminus S)$ минимальное ребро, пересекающее разрез, принадлежит некоторому MST.

**Лемма о безопасном ребре**: если $A$ — подмножество рёбер MST и $(u, v)$ — минимальное ребро, пересекающее разрез, не нарушающий $A$, то $A \cup \{(u,v)\}$ также является подмножеством некоторого MST.

## Алгоритмы

### Kruskal

```
Kruskal(G):
  Отсортировать рёбра по весу
  T ← ∅
  for (u, v, w) в порядке возрастания:
    if Find(u) ≠ Find(v):
      T ← T ∪ {(u,v)}
      Union(u, v)
```

### Prim

```
Prim(G, s):
  key[s] ← 0, key[v] ← ∞
  PQ ← все вершины
  while PQ не пуста:
    u ← PQ.extractMin()
    for (v, w) ∈ adj(u):
      if v ∈ PQ и w < key[v]:
        key[v] ← w, prev[v] ← u
```

## Сложность

| Алгоритм | Время |
|----------|-------|
| Kruskal | $O(E \log E)$ |
| Prim (бинарная куча) | $O(E \log V)$ |
| Borůvka | $O(E \log V)$ |

## API

Пространство имён `AI.Algorithms.MST`. Дерево строится в конструкторе.

| Член | Описание |
|------|----------|
| `Kruskal<Edge>(GraphW<Edge> g)` | Алгоритм Краскала (сортировка рёбер + СНМ) |
| `.MSTEdges` | `List<Edge>`: рёбра остова |
| `.TotalWeight` | Суммарный вес |
| `Prim<Edge>(GraphW<Edge> g)` | Алгоритм Прима |
| `.MSTEdges()` | Метод (не свойство!): перечисление рёбер остова |
| `.EdgeTo`, `.KeyTo` | Массивы дерева: входящее ребро и ключ вершины |
| `Boruvka<Edge>(GraphW<Edge> g)` | Алгоритм Борувки; `.MSTEdges`, `.TotalWeight` |

Обратите внимание: у `Prim` рёбра отдаёт **метод** `MSTEdges()`, у `Kruskal` и `Boruvka` — **свойство** `MSTEdges`.

Исходники: `src/AI.Algorithms/MST/`.

## Код

```csharp
using AI.Algorithms.EWG;
using AI.Algorithms.MST;

var g = new GraphW<Edge>(5);
g.AddEdge(0, 1, 2); g.AddEdge(0, 3, 6);
g.AddEdge(1, 2, 3); g.AddEdge(1, 3, 8);
g.AddEdge(1, 4, 5); g.AddEdge(2, 4, 7);
g.AddEdge(3, 4, 9);

var kruskal = new Kruskal<Edge>(g);
var prim    = new Prim<Edge>(g);
var boruvka = new Boruvka<Edge>(g);

foreach (var e in kruskal.MSTEdges)
    Console.WriteLine($"{e.StartV} — {e.EndV}  (w = {e.W})");

// Все три алгоритма дают одинаковый суммарный вес: MST единственно
// по весу, даже если наборы рёбер при равных весах различаются
Console.WriteLine($"Kruskal: {kruskal.TotalWeight}");
Console.WriteLine($"Prim:    {prim.TotalWeight}");
Console.WriteLine($"Borůvka: {boruvka.TotalWeight}");
Console.WriteLine($"Рёбер в остове: {kruskal.MSTEdges.Count} (ожидается {g.V - 1})");
```

