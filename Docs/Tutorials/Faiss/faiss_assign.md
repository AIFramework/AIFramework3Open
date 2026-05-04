# Кластеризация через Assign (FAISS)

## Постановка задачи

Дано $n$ точек $\{x_1, \ldots, x_n\} \subset \mathbb{R}^d$ и $K$ центроидов $\{c_1, \ldots, c_K\}$. Метод **Assign** назначает каждой точке ближайший центроид:

$$
\text{assign}(x_i) = \arg\min_{k=1}^{K} \|x_i - c_k\|_2^2
$$

## Связь с K-Means

Assign — это **E-шаг** алгоритма K-Means:

1. **Инициализация** центроидов $c_k$ (случайная или K-Means++)
2. **E-шаг**: $\text{assign}(x_i) = \arg\min_k \|x_i - c_k\|^2$ — используется FAISS Assign
3. **M-шаг**: $c_k \leftarrow \frac{1}{|S_k|} \sum_{x_i \in S_k} x_i$
4. Повторять до сходимости

Сложность одного E-шага: $O(n \cdot K \cdot d)$ для Flat-индекса.

## Применение FAISS Assign

Достоинство FAISS в том, что при большом $K$ и $n$ поиск можно ускорить через приближённый индекс (IVF, HNSW), сохраняя высокое качество назначений.

| $n$ | $K$ | Рекомендуемый индекс |
|-----|-----|---------------------|
| < 10 000 | любой | `Flat` |
| 10 000–1 000 000 | любой | `IVF<nlist>,Flat` |
| > 1 000 000 | любой | `IVF<nlist>,PQ<M>` |

## API

```csharp
// Добавляем центроиды в Flat-индекс
using var idx = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
idx.Add(centroids);  // float[][] или Vector[]

// Для каждой точки данных находим ближайший центроид
float[] flatData = /* n * dim */;
long[] assignments = idx.Assign(n, flatData);

// Группируем точки по кластерам
for (int i = 0; i < n; i++) {
    int cluster = (int)assignments[i];
    Console.WriteLine($"Точка {i} → кластер {cluster}");
}
```

## Инициализация центроидов (K-Means++)

```csharp
// Первый центроид — случайный
var centers = new List<float[]> { data[rng.Next(n)] };

// Каждый следующий — с вероятностью пропорциональной квадрату расстояния
while (centers.Count < k) {
    using var tmp = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
    tmp.Add(centers.ToArray());
    var (dists, _) = tmp.Search(data, 1);
    double total = dists.Sum(d => d[0]);
    double r = rng.NextDouble() * total;
    double acc = 0;
    for (int i = 0; i < n; i++) {
        acc += dists[i][0];
        if (acc >= r) { centers.Add(data[i]); break; }
    }
}
```
