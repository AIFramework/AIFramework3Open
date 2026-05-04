# Метрики сходства строк (AI.DataPrepaire)

## Расстояние Левенштейна

Минимальное число операций (вставка, удаление, замена) для преобразования одной строки в другую:

$$
d(a, b) = \begin{cases}
|a| & \text{if } |b| = 0 \\
|b| & \text{if } |a| = 0 \\
d(a_{1:}, b_{1:}) & \text{if } a_0 = b_0 \\
1 + \min(d(a_{1:}, b), d(a, b_{1:}), d(a_{1:}, b_{1:})) & \text{иначе}
\end{cases}
$$

Нормализованное сходство: $\text{sim} = 1 - \frac{d(a,b)}{\max(|a|, |b|)}$

## Гистограммный косинус

Строит n-граммные гистограммы обеих строк и вычисляет косинусное сходство:

$$
\text{hcos}(a, b) = \frac{\langle h_a, h_b \rangle}{\|h_a\|\,\|h_b\|}
$$

где $h_s[n\text{-gram}] = \text{count}(n\text{-gram} \in s)$

## Корреляция слов

Доля символов, присутствующих в обеих строках (мера Жаккара на символах):

$$
\text{WordCorrelation}(a, b) = \frac{|set(a) \cap set(b)|}{|set(a) \cup set(b)|}
$$

## API

```csharp
using AI.DataPrepaire.NLPUtils;

// Расстояние Левенштейна
float dist = CompareStringMethods.LevenshteinDistance("кот", "код");
// → 1 (одна замена: т→д)

// Нормализованная Левенштейн на int[] (закодированных токенах)
int[] a = { 1, 2, 3, 4 };
int[] b = { 1, 2, 4 };
float tokDist = CompareStringMethods.LevenshteinDistance(a, b);

// Корреляция слов (символьный Жаккар)
float wc = CompareStringMethods.WordCorellation("машина", "машины");

// Гистограммный косинус (char n-gram, авто-n)
float hcos = CompareStringMethods.HistogramCos("нейронная сеть", "нейронные сети");

// С явным n (размер n-граммы)
float hcos3 = CompareStringMethods.HistogramCos("hello", "helo", 3);

// Гистограммная кросс-энтропия
float hce = CompareStringMethods.HistogramCrossEntropy("text1", "text2");
```
