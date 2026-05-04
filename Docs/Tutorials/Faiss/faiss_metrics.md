# Метрики расстояния в FAISS

## L2 (Евклидово расстояние)

$$
d_{L2}(q, x) = \|q - x\|_2^2 = \sum_{i=1}^d (q_i - x_i)^2
$$

FAISS хранит и возвращает **квадрат** расстояния (экономит вычисление корня).

Используется для:
- Кластеризации (k-means)
- Поиска похожих изображений по признакам CNN
- Любого пространства, где важно «геометрическое» расстояние

## Inner Product (Скалярное произведение)

$$
\text{score}(q, x) = \langle q, x \rangle = \sum_{i=1}^d q_i \cdot x_i
$$

Чем **больше** — тем ближе. FAISS возвращает результаты в порядке убывания score.

Для **нормализованных** векторов IP эквивалентен **косинусному сходству**:

$$
\cos\theta = \frac{\langle q, x \rangle}{\|q\|\,\|x\|} = \langle q, x \rangle \quad (\|q\| = \|x\| = 1)
$$

Используется для:
- Семантического поиска (sentence embeddings)
- Рекомендательных систем
- Поиска по эмбеддингам NLP-моделей

## Сравнение метрик

| Свойство | L2 | Inner Product |
|----------|----|---------------|
| Чувствительность к масштабу | Да | Да |
| Работа с ненормализованными векторами | Корректно | Искажение |
| Семантическое сходство | Частично | Да (при норм.) |
| Поддержка в HNSW (FAISS) | Полная | Частичная |

## Нормализация для IP-поиска

```csharp
float[] Normalize(float[] v) {
    double norm = Math.Sqrt(v.Sum(x => x * x));
    return norm > 1e-9 ? v.Select(x => (float)(x / norm)).ToArray() : v;
}
```

## API

```csharp
// L2-индекс
using var l2 = FaissIndex.Create(dim, "Flat", MetricType.METRIC_L2);

// IP-индекс (нормализуйте векторы!)
using var ip = FaissIndex.Create(dim, "Flat", MetricType.METRIC_INNER_PRODUCT);

l2.Add(vectors);
ip.Add(normalizedVectors);

var (l2Dists, l2Labels) = l2.Search(query, k);
var (ipScores, ipLabels) = ip.Search(normalizedQuery, k);
```
