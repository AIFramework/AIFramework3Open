# A* и IDA*

## Постановка задачи

Дан взвешенный граф и пара вершин (старт, цель). Необходимо найти кратчайший путь, используя эвристическую функцию для ускорения поиска. A* — один из наиболее популярных алгоритмов поиска пути в играх, робототехнике и навигации.

## Теория

A* комбинирует стоимость пути от старта $g(n)$ с эвристической оценкой до цели $h(n)$:

$$f(n) = g(n) + h(n)$$

На каждом шаге раскрывается вершина с минимальным значением $f(n)$.

### Свойства эвристики

- **Допустимость**: $h(n) \leq h^*(n)$ — эвристика не переоценивает реальное расстояние
- **Монотонность** (консистентность): $h(n) \leq c(n, n') + h(n')$ — гарантирует оптимальность без повторного раскрытия вершин

### Псевдокод A*

```
AStar(start, goal, h):
  open ← приоритетная очередь {start}, g[start] ← 0
  while open не пуста:
    n ← open.extractMin() по f(n)
    if n = goal: return восстановить путь
    for n' ∈ neighbors(n):
      tentative ← g[n] + cost(n, n')
      if tentative < g[n']:
        g[n'] ← tentative
        f[n'] ← g[n'] + h(n')
        open.insertOrUpdate(n')
```

## IDA* (Iterative Deepening A*)

Использует итеративное углубление по порогу $f$-значения. Не требует приоритетной очереди — расход памяти $O(bd)$.

## Сложность

| Алгоритм | Время (худший) | Память |
|----------|---------------|--------|
| A* | $O(b^d)$ | $O(b^d)$ |
| IDA* | $O(b^d)$ | $O(bd)$ |

где $b$ — фактор ветвления, $d$ — глубина решения.

## API

Пространство имён `AI.Algorithms.EWG`. Эвристика передаётся делегатом `Func<int, double>` — интерфейса `IHeuristic` в библиотеке нет.

| Член | Описание |
|------|----------|
| `AStarSearch<Edge>(GraphW<Edge> g, int start, int goal, Func<int, double> h)` | Поиск с эвристикой `h` |
| `.Found` | Путь найден |
| `.PathCost` | Стоимость найденного пути |
| `.GetPath()` | Список вершин от старта к цели |
| `.GScore` | `double[]`: фактические стоимости от старта |
| `.CameFrom` | `Edge[]`: ребро, по которому пришли в вершину |

Для инкрементального перепланирования при изменении весов см. [D* Lite, LPA* и ARA*](dynamic_pathfinding.md).

Исходник: `src/AI.Algorithms/EWG/AStar.cs`.

## Код

Сетка 10×10 с препятствиями; эвристика — манхэттенское расстояние, допустимое для 4-связной сетки с единичными весами:

```csharp
using AI.Algorithms.EWG;

const int W = 10, H = 10;
var g = new GraphW<Edge>(W * H);
var blocked = new bool[W, H];
for (int y = 3; y < 8; y++) blocked[5, y] = true;   // стена

int Idx(int x, int y) => y * W + x;

for (int x = 0; x < W; x++)
    for (int y = 0; y < H; y++)
    {
        if (blocked[x, y]) continue;
        int[] dx = { 1, 0, -1, 0 }, dy = { 0, 1, 0, -1 };
        for (int d = 0; d < 4; d++)
        {
            int nx = x + dx[d], ny = y + dy[d];
            if (nx >= 0 && nx < W && ny >= 0 && ny < H && !blocked[nx, ny])
                g.AddArce(Idx(x, y), Idx(nx, ny), 1);
        }
    }

// Манхэттенская эвристика: никогда не переоценивает — значит A* оптимален
double Heuristic(int v) => Math.Abs(v % W - (W - 1)) + Math.Abs(v / W - (H - 1));

var astar = new AStarSearch<Edge>(g, Idx(0, 0), Idx(W - 1, H - 1), Heuristic);

if (astar.Found)
    Console.WriteLine($"Длина пути: {astar.PathCost:F0}, вершин: {astar.GetPath().Count}");
else
    Console.WriteLine("Путь не найден");
```

