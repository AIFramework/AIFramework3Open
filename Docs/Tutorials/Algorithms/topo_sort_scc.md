# Топологическая сортировка и компоненты сильной связности

## Постановка задачи

Для ориентированного ациклического графа (DAG) требуется упорядочить вершины так, чтобы для каждого ребра $(u, v)$ вершина $u$ шла раньше $v$. Для произвольного ориентированного графа — выделить компоненты сильной связности (SCC). Эти алгоритмы также лежат в основе решения задачи 2-SAT.

## Топологическая сортировка (алгоритм Кана)

```
Kahn(G):
  inDeg[v] ← число входящих рёбер для каждого v
  queue ← все v с inDeg[v] = 0
  while queue не пуста:
    u ← queue.dequeue(), вывести u
    for v ∈ adj(u):
      inDeg[v] -= 1
      if inDeg[v] = 0: queue.enqueue(v)
  if выведено < |V|: граф содержит цикл
```

## Компоненты сильной связности

**Алгоритм Тарьяна** — один проход DFS с использованием стека и массивов `low[]`, `disc[]`.

**Алгоритм Косарайю** — два прохода DFS: первый по исходному графу, второй по транспонированному $G^T$.

## Точки сочленения и мосты

Вершина $v$ — **точка сочленения**, если её удаление увеличивает число компонент связности. Ребро $(u, v)$ — **мост**, если его удаление разъединяет граф. Находятся модификацией DFS Тарьяна.

## 2-SAT через SCC

Формула $\varphi$ в 2-КНФ выполнима тогда и только тогда, когда ни одна переменная $x$ не находится в одной SCC со своим отрицанием $\neg x$:

$$\forall x: \; x \text{ и } \neg x \text{ в разных SCC}$$

## Сложность

Все алгоритмы: $O(V + E)$.

## API

Пространство имён `AI.Algorithms.GraphStructure`; сам граф — `AI.Algorithms.EWG.Graph`.
Класса `BridgeFinder` в библиотеке нет: мосты и точки сочленения ищет `ArticulationBridges`.

| Член | Описание |
|------|----------|
| `TopologicalSort(Graph g)` | Топологическая сортировка орграфа |
| `.Order` | `int[]`: порядок вершин |
| `.HasCycle` | Граф содержит цикл — порядок не определён |
| `TarjanSCC(Graph g)` | Сильно связные компоненты за один проход |
| `.ComponentId` | `int[]`: номер компоненты для каждой вершины |
| `.Count` | Число компонент |
| `.StronglyConnected(u, v)` | Лежат ли вершины в одной компоненте |
| `ArticulationBridges(Graph g)` | Точки сочленения и мосты неориентированного графа |
| `.ArticulationPoints` | `List<int>` |
| `.Bridges` | `List<(int U, int V)>` |

Исходники: `src/AI.Algorithms/GraphStructure/`.

## Код

```csharp
using AI.Algorithms.EWG;
using AI.Algorithms.GraphStructure;

// Орграф с циклом 1->2->3->1
var dir = new Graph(5);
dir.AddArc(0, 1); dir.AddArc(1, 2);
dir.AddArc(2, 3); dir.AddArc(3, 1); dir.AddArc(3, 4);

var topo = new TopologicalSort(dir);
Console.WriteLine(topo.HasCycle
    ? "Цикл есть — топологический порядок не существует"
    : string.Join("->", topo.Order));

var scc = new TarjanSCC(dir);
Console.WriteLine($"Компонент сильной связности: {scc.Count}");
Console.WriteLine($"1 и 3 в одной компоненте: {scc.StronglyConnected(1, 3)}");   // true

// Неориентированный граф: 2—3 — мост, вершина 2 — точка сочленения
var und = new Graph(5);
und.AddEdge(0, 1); und.AddEdge(1, 2);
und.AddEdge(0, 2); und.AddEdge(2, 3); und.AddEdge(3, 4);

var ab = new ArticulationBridges(und);
Console.WriteLine($"Точки сочленения: {string.Join(", ", ab.ArticulationPoints)}");
foreach (var (u, v) in ab.Bridges)
    Console.WriteLine($"Мост: {u} — {v}");
```

