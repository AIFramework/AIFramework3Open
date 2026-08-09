# Минимальный разрез

## Постановка задачи

Дан неориентированный взвешенный граф $G = (V, E)$. Требуется найти разбиение вершин на два непустых множества $S$ и $V \setminus S$ такое, что суммарный вес рёбер между ними минимален. В отличие от $s$-$t$ разреза, здесь $s$ и $t$ не фиксированы.

## Алгоритм Stoer—Wagner

Итеративно выполняет процедуру **MinimumCutPhase**, которая находит $s$-$t$ разрез для наиболее связной пары вершин, затем объединяет $s$ и $t$:

```
StoerWagner(G):
  best_cut ← ∞
  while |V| > 1:
    (s, t, cut_weight) ← MinimumCutPhase(G)
    best_cut ← min(best_cut, cut_weight)
    merge(s, t)  // объединить вершины
  return best_cut
```

### MinimumCutPhase

Жадно наращивает множество $A$, добавляя на каждом шаге вершину, наиболее плотно связанную с $A$:

$$w(A, v) = \sum_{u \in A} w(u, v)$$

Последние две добавленные вершины дают $s$ и $t$; вес разреза равен $w(A, t)$.

## Сложность

$$O(VE + V^2 \log V)$$

## Дерево Гомори—Ху

Для нахождения минимального $s$-$t$ разреза для **всех пар** вершин строится дерево Гомори—Ху — взвешенное дерево на $V$ вершинах. Минимальный разрез между $s$ и $t$ равен минимальному ребру на пути $s \to t$ в этом дереве.

Построение требует $|V| - 1$ вычислений максимального потока.

## API

Пространство имён `AI.Algorithms.NetworkFlow`. Классы называются `StoerWagner` и `GomoryHu` (без суффиксов `MinCut`/`Tree`).

| Член | Описание |
|------|----------|
| `StoerWagner(int v)` | Глобальный минимальный разрез неориентированного графа |
| `.AddEdge(int u, int v, double w)` | Ребро веса `w` |
| `.Solve()` | `(double MinCut, List<int> Partition)` — вес разреза и одна из долей |
| `GomoryHu(int v)` | Дерево всех попарных минимальных разрезов |
| `.AddEdge(int u, int v, double w)` | Ребро |
| `.Build()` | Построить дерево — **обязательно вызвать** перед запросами |
| `.MinCut(u, v)` | Вес минимального разреза, разделяющего `u` и `v` |
| `.MinCutPartition(u, v)` | Доля, содержащая `u` |

Исходники: `src/AI.Algorithms/NetworkFlow/StoerWagner.cs`, `GomoryHu.cs`.

## Код

```csharp
using AI.Algorithms.NetworkFlow;

var sw = new StoerWagner(6);
sw.AddEdge(0, 1, 3); sw.AddEdge(0, 2, 1);
sw.AddEdge(1, 2, 3); sw.AddEdge(2, 3, 1);   // «перемычка» между кластерами
sw.AddEdge(3, 4, 3); sw.AddEdge(3, 5, 1);
sw.AddEdge(4, 5, 3);

var (minCut, partition) = sw.Solve();

// Разрез пройдёт по слабой перемычке 2—3
Console.WriteLine($"Минимальный разрез: {minCut:F0}");
Console.WriteLine($"Доля A: [{string.Join(", ", partition)}]");
```

Дерево Гомори—Ху отвечает на все $\binom{n}{2}$ запросов, вычислив лишь $n-1$ разрез:

```csharp
var gh = new GomoryHu(5);
gh.AddEdge(0, 1, 4); gh.AddEdge(1, 2, 3);
gh.AddEdge(2, 3, 5); gh.AddEdge(3, 4, 2);
gh.AddEdge(0, 4, 1);
gh.Build();   // без этого вызова MinCut вернёт мусор

for (int i = 0; i < 5; i++)
    for (int j = i + 1; j < 5; j++)
        Console.WriteLine($"mincut({i},{j}) = {gh.MinCut(i, j):F0}");
```

