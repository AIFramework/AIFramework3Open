# LaCAM и LaCAM*

## Постановка задачи

MAPF для большого числа агентов ($k > 100$). LaCAM (Lazy Constraints Addition for MAPF) — быстрый и масштабируемый алгоритм, использующий ленивое добавление ограничений для поиска бесконфликтных путей.

## Идея алгоритма (Okumura, 2023)

LaCAM работает в пространстве **конфигураций** — совместных позиций всех агентов $\pi = (v_1, v_2, \ldots, v_k)$.

### Ключевые концепции

- **Генератор конфигураций**: для текущей конфигурации генерирует следующую по одному агенту
- **Ленивые ограничения**: ограничения добавляются только при обнаружении конфликтов
- **Поиск в глубину**: обход дерева конфигураций с возвратами

### Псевдокод

```
LaCAM(start, goal):
  open ← {start}
  while open не пуста:
    π ← open.top()
    π' ← getNextConfig(π, constraints)
    if π' = null:
      open.pop(), backtrack
    else if hasConflict(π, π'):
      добавить ограничение
    else:
      open.push(π')
      if π' = goal: return путь
```

## LaCAM*

Расширение LaCAM с гарантией оптимальности:
- Использует OPEN/CLOSED списки как в A*
- Поддерживает $f$-значения для конфигураций:

$$f(\pi) = g(\pi) + h(\pi), \quad h(\pi) = \sum_{i=1}^{k} h_i(v_i)$$

## Сравнение

| Метод | Оптимальность | Масштабируемость |
|-------|--------------|-----------------|
| CBS | Да | Низкая (до ~50 агентов) |
| LaCAM | Нет | Высокая (1000+ агентов) |
| LaCAM* | Да | Средняя |

## Сложность

LaCAM: экспоненциален в теории, но на практике работает за секунды для тысяч агентов.

## API

Пространство имён `AI.Algorithms.MAPF`. Параметра `optimal` нет — LaCAM* это **отдельный класс** `LaCAMStar`.

| Член | Описание |
|------|----------|
| `LaCAM(GridMap map, List<MAPFAgent> agents, int maxIter = 10000)` | Быстрый поиск первого решения |
| `LaCAMStar(GridMap map, List<MAPFAgent> agents, int maxIter = 10000)` | Anytime-вариант: улучшает решение, пока не исчерпан `maxIter` |
| `.Solve()` | `MAPFSolution` |

`maxIter` — предел числа раскрытых конфигураций. Для `LaCAM` он ограничивает поиск первого решения, для `LaCAMStar` — ещё и бюджет на улучшение.

Исходники: `src/AI.Algorithms/MAPF/LaCAM.cs`, `LaCAMStar.cs`.

## Код

```csharp
using AI.Algorithms.MAPF;

var map = new GridMap(20, 20);
var rng = new Random(42);
for (int i = 0; i < 40; i++)
    map.SetBlocked(rng.Next(20), rng.Next(20), true);

var agents = new List<MAPFAgent>();
for (int i = 0; i < 10; i++)
{
    int sx, sy, gx, gy;
    do { sx = rng.Next(20); sy = rng.Next(20); } while (map.IsBlocked(sx, sy));
    do { gx = rng.Next(20); gy = rng.Next(20); } while (map.IsBlocked(gx, gy));
    agents.Add(new MAPFAgent { Id = i, StartX = sx, StartY = sy, GoalX = gx, GoalY = gy });
}

var fast = new LaCAM(map, agents, maxIter: 10000).Solve();
Console.WriteLine($"LaCAM:  sumOfCosts={fast.SumOfCosts}, makespan={fast.Makespan}");

// LaCAM* тратит тот же бюджет, но не останавливается на первом решении
var better = new LaCAMStar(map, agents, maxIter: 10000).Solve();
Console.WriteLine($"LaCAM*: sumOfCosts={better.SumOfCosts}, makespan={better.Makespan}");
Console.WriteLine($"Выигрыш: {fast.SumOfCosts - better.SumOfCosts}");
```

