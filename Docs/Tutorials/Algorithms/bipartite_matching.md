# Паросочетание в двудольных графах

## Постановка задачи

Дан двудольный граф $G = (L \cup R, E)$. Требуется найти максимальное паросочетание — наибольшее по мощности множество рёбер $M \subseteq E$ такое, что никакие два ребра из $M$ не имеют общей вершины.

## Алгоритм Куна

Для каждой вершины из левой доли пытается найти увеличивающий путь с помощью DFS:

```
Kuhn(G):
  match ← пустое паросочетание
  for u ∈ L:
    visited ← ∅
    TryKuhn(u, visited, match)

TryKuhn(u, visited, match):
  for v ∈ adj(u):
    if v ∉ visited:
      visited ← visited ∪ {v}
      if match[v] = null или TryKuhn(match[v], visited, match):
        match[v] ← u
        return true
  return false
```

## Алгоритм Хопкрофта—Карпа

Использует BFS для нахождения **кратчайших** увеличивающих путей одновременно для нескольких вершин, что даёт лучшую асимптотику.

## Теорема Кёнига

В двудольном графе:

$$\text{максимальное паросочетание} = \text{минимальное вершинное покрытие}$$

## Сложность

| Алгоритм | Время |
|----------|-------|
| Kuhn | $O(VE)$ |
| Hopcroft—Karp | $O(E\sqrt{V})$ |

## Связанные задачи

- **Минимальное рёберное покрытие**: $|V| - |M^*|$ (по теореме Галлаи)
- **Максимальное независимое множество**: $|V| - |M^*|$ (в двудольных графах)

## API

Пространство имён `AI.Algorithms.Matching`. Граф задаётся прямо в решателе; классы называются `KuhnMatching` и `HopcroftKarp`.

| Член | Описание |
|------|----------|
| `KuhnMatching(int leftSize, int rightSize)` | Алгоритм Куна |
| `.AddEdge(int l, int r)` | Ребро между долями |
| `.Solve()` | Мощность максимального паросочетания |
| `HopcroftKarp(int leftSize, int rightSize)` | Алгоритм Хопкрофта—Карпа |
| `.AddEdge(int left, int right)` | Ребро |
| `.MaxMatching()` | Мощность максимального паросочетания |
| `.MatchLeft`, `.MatchRight` | `int[]`: пара для вершины, `−1` если не сопоставлена |

Разные имена методов — `Solve()` у Куна и `MaxMatching()` у Хопкрофта—Карпа — легко перепутать.

Исходники: `src/AI.Algorithms/Matching/`.

## Код

```csharp
using AI.Algorithms.Matching;

// 4 работника, 4 задачи; ребро = работник умеет выполнять задачу
var kuhn = new KuhnMatching(leftSize: 4, rightSize: 4);
kuhn.AddEdge(0, 0); kuhn.AddEdge(0, 1);
kuhn.AddEdge(1, 0);
kuhn.AddEdge(2, 1); kuhn.AddEdge(2, 2);
kuhn.AddEdge(3, 2); kuhn.AddEdge(3, 3);

int size = kuhn.Solve();
Console.WriteLine($"Максимальное паросочетание: {size}");

for (int l = 0; l < 4; l++)
    Console.WriteLine(kuhn.MatchLeft[l] >= 0
        ? $"работник {l} -> задача {kuhn.MatchLeft[l]}"
        : $"работник {l} без задачи");
```

На плотных графах Хопкрофт—Карп заметно быстрее за счёт обработки нескольких дополняющих путей за фазу:

```csharp
var hk = new HopcroftKarp(leftSize: 4, rightSize: 4);
hk.AddEdge(0, 0); hk.AddEdge(0, 1); hk.AddEdge(1, 0);
hk.AddEdge(2, 1); hk.AddEdge(2, 2); hk.AddEdge(3, 2); hk.AddEdge(3, 3);

Console.WriteLine($"Hopcroft-Karp: {hk.MaxMatching()}");   // тот же результат
```

