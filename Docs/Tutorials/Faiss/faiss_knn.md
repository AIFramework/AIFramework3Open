# KNN-поиск (FAISS)

## Задача поиска ближайших соседей

Дано множество векторов $\mathcal{X} = \{x_1, \ldots, x_n\} \subset \mathbb{R}^d$ и запрос $q \in \mathbb{R}^d$. Требуется найти $K$ векторов, минимально удалённых от $q$:

$$
\text{KNN}(q) = \underset{S \subseteq \mathcal{X},\, |S|=K}{\arg\min} \sum_{x \in S} \text{dist}(q, x)
$$

## Метрики расстояния

**Евклидово расстояние (L2):**
$$
d_{L2}(q, x) = \|q - x\|_2 = \sqrt{\sum_{i=1}^d (q_i - x_i)^2}
$$

**Скалярное произведение (Inner Product):**
$$
\text{score}(q, x) = \langle q, x \rangle = \sum_{i=1}^d q_i \cdot x_i
$$

Чем **больше** score — тем ближе вектор (максимальное паросочетание).

## Типы индексов FAISS

| Индекс | Тип поиска | Сложность запроса | Использование |
|--------|-----------|-------------------|---------------|
| `Flat` | Точный | $O(n \cdot d)$ | Высокая точность, малые $n$ |
| `HNSW32` | Приближённый | $O(\log n)$ | Скорость, большие $n$ |
| `IVF` | Приближённый | $O(n / \text{nlist})$ | Баланс скорость/точность |

## HNSW (Hierarchical Navigable Small World)

HNSW строит многоуровневый граф, в котором каждый узел связан с $M$ ближайшими соседями. Поиск начинается с верхнего уровня и жадно спускается к ближайшему соседу запроса.

Сложность построения: $O(n \cdot M \cdot \log n)$  
Сложность запроса: $O(\log n)$

## API

```csharp
// Точный поиск L2
using var idx = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
idx.Add(vectors);  // float[][]

var (dists, labels) = idx.Search(query, k);
// dists[i][j]  — расстояние j-го соседа для i-го запроса
// labels[i][j] — индекс j-го соседа для i-го запроса

// Приближённый поиск (HNSW + IDMap2)
using var hnsw = FaissIndex.CreateDefault(dim, MetricType.METRIC_L2);
hnsw.Add(vectors);
var (d2, l2) = hnsw.Search(query, k);
```
