# Пакетный поиск (FAISS Batch Search)

## Мотивация

При обработке одного запроса за раз накладные расходы на вызов P/Invoke превышают время вычислений. FAISS поддерживает **батч-запросы**: $m$ запросов передаются одним вызовом, и результаты возвращаются матрицами.

## Формализация

Для набора запросов $Q = \{q_1, \ldots, q_m\}$ и индекса $\mathcal{X}$ результат батч-поиска:

$$
\text{labels}[i][j] = \arg\min_{x \in \mathcal{X}} \text{dist}(q_i, x), \quad j = 1, \ldots, K
$$

Возвращается матрица расстояний $D \in \mathbb{R}^{m \times K}$ и меток $L \in \mathbb{Z}^{m \times K}$.

## Сложность

| Операция | Flat | HNSW |
|----------|------|------|
| Одиночный запрос | $O(n \cdot d)$ | $O(d \cdot \log n)$ |
| Батч $m$ запросов | $O(m \cdot n \cdot d)$ | $O(m \cdot d \cdot \log n)$ |

FAISS внутренне параллелизует батч-поиск через BLAS/OpenMP, что даёт ускорение относительно $m$ последовательных запросов.

## API

```csharp
// Построение индекса
using var idx = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);
idx.Add(vectors);   // float[][], n векторов

// Батч-запрос: m запросов, K соседей
float[][] queries = /* m × dim */;
var (dists, labels) = idx.Search(queries, k);

// dists[qi][j]  — расстояние до j-го соседа для запроса qi
// labels[qi][j] — id j-го соседа для запроса qi
for (int qi = 0; qi < queries.Length; qi++)
    for (int j = 0; j < k; j++)
        Console.WriteLine($"Q{qi}: сосед[{j}] = id {labels[qi][j]}, dist = {dists[qi][j]}");
```

## Рекомендации по размеру батча

- Для `Flat`: размер батча влияет умеренно (ускорение от BLAS GEMM).
- Для `HNSW`: оптимальный батч-размер ~32–256, выше — убывающая отдача.
- Для `IVF`: рекомендуется батч ≥ 64 для эффективного использования кэша.
