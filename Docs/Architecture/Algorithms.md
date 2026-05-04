# Алгоритмы на графах и комбинаторная оптимизация — `AI.Algorithms`

Сборка **`AI.Algorithms`** (`AI.Algorithms.dll`, **.NET 9.0**) объединяет классические и современные алгоритмы на графах, потоки в сетях, задачи назначения и паросочетания, многоагентный поиск путей (MAPF), транспортную задачу, маршрутизацию (VRP/TSP) и распределение задач. Библиотека **не имеет зависимостей** от других проектов фреймворка и может использоваться автономно.

---

## Зависимости

| Проект | Зачем |
|--------|--------|
| _(нет)_ | Сборка полностью автономна — не ссылается на другие проекты решения. |

---

## Ключевые области

| Пространство имён | Назначение |
|--------------------|------------|
| `AI.Algorithms.EWG` | Графы и кратчайшие пути: BFS, DFS, Dijkstra, A*, IDA*, Fringe Search, Bellman–Ford, Floyd–Warshall, Johnson, Yen K-Shortest, двунаправленный Dijkstra. |
| `AI.Algorithms.GraphStructure` | Структурный анализ графов: топологическая сортировка, компоненты сильной связности (Kosaraju), точки сочленения и мосты. |
| `AI.Algorithms.MST` | Минимальные остовные деревья: Kruskal, Prim, Borůvka. |
| `AI.Algorithms.DynamicPathfinding` | Инкрементальный поиск путей: D* Lite, LPA*, ARA*. |
| `AI.Algorithms.NetworkFlow` | Потоки в сетях: Ford–Fulkerson, Edmonds–Karp, Dinic, Push–Relabel; потоки минимальной стоимости (SSP, Cycle-Canceling, Cost Scaling); минимальный разрез (Stoer–Wagner); дерево Гомори—Ху. |
| `AI.Algorithms.Matching` | Назначение и паросочетание: венгерский алгоритм (Kuhn–Munkres), двудольное паросочетание (Kuhn, Hopcroft–Karp), общие паросочетания (Edmonds Blossom), устойчивый брак (Gale–Shapley), аукционный алгоритм, SSP-assignment. |
| `AI.Algorithms.MAPF` | Multi-Agent Path Finding: CBS / ECBS / ICBS / PBS, PIBT, Token Passing, LaCAM / LaCAM*, WHCA* / HCA*, SIPP, Push & Swap / Push & Rotate; вспомогательные типы `GridMap`, `MAPFSolution`. |
| `AI.Algorithms.TransportTask` | Классическая транспортная задача: метод потенциалов, аппроксимация Фогеля, северо-западный угол. |
| `AI.Algorithms.VRP` | Маршрутизация транспорта и TSP: конструктивные (Clarke–Wright, Sweep, Solomon I1), улучшающие (2-opt, Lin–Kernighan, Christofides), метаэвристики (GA, Tabu, ACO, SA, ALNS). |
| `AI.Algorithms.TaskAllocation` | Распределение задач между агентами: аукционы (CNP, SSI, Sequential), DCOP-решатели (ADOPT, DPOP, Max-Sum, DSA, MGM), консенсусные протоколы (CBBA, Greedy). |
| `AI.Algorithms.PriorityQueues` | Очереди с приоритетом (min/max), используемые внутренне другими алгоритмами. |

---

## Роль в решении

- Автономная библиотека алгоритмов — подключается в любой .NET-проект без дополнительных зависимостей.
- Используется в демонстрационном модуле **`AlgorithmsModule`** (WebUI) для интерактивной визуализации всех категорий алгоритмов.
- Классы `VRPInstance` / `VRPSolution`, `MAPFSolution`, `AllocationResult` и другие предоставляют типизированные результаты для интеграции с внешним кодом.

---

## Категории алгоритмов (демо-модуль)

| Категория | Примеры алгоритмов |
|-----------|--------------------|
| Графы и кратчайшие пути | BFS/DFS, Dijkstra, A*/IDA*, Bellman–Ford, Floyd–Warshall, Yen K-Shortest, топология/SCC/мосты, MST |
| Потоки в сетях | Ford–Fulkerson, Edmonds–Karp, Dinic, Push–Relabel, SSP/Cycle-Cancel/Cost-Scale, Stoer–Wagner, Gomory–Hu |
| Назначение и паросочетание | Hungarian, Kuhn, Hopcroft–Karp, Edmonds Blossom, Gale–Shapley |
| MAPF | CBS/ECBS/PBS, PIBT, Token Passing, Push & Swap/Rotate, WHCA*/HCA*, LaCAM/LaCAM* |
| Транспортная задача | Метод потенциалов, аппроксимация Фогеля |
| VRP / TSP | Clarke–Wright, Sweep, Solomon I1, 2-opt, Lin–Kernighan, Christofides, GA, Tabu, ACO, SA, ALNS |
| Распределение задач | CNP, SSI, ADOPT, DPOP, Max-Sum, DSA, MGM, CBBA, Greedy |

---

## Сборка

```bash
dotnet build src/AI.Algorithms/AI.Algorithms.csproj -c Release
```

Метаданные NuGet задаются в корневом **`Directory.Build.props`**.
