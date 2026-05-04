# BERT-эмбеддинги (AI.ONNX)

## Текстовые эмбеддинги

**Эмбеддинг** — плотный вектор фиксированной размерности, представляющий текст в семантическом пространстве. Семантически похожие тексты имеют близкие векторы.

## Косинусное сходство

Мера близости двух эмбеддингов:

$$
\text{cos}(\theta) = \frac{\langle u, v \rangle}{\|u\|\,\|v\|} \in [-1, 1]
$$

- $\approx 1$ — тексты очень похожи
- $\approx 0$ — тексты не связаны
- $\approx -1$ — тексты противоположны

## Стратегии пулинга

| Метод | Описание | Применение |
|-------|----------|------------|
| **Mean pooling** | Среднее по всем токенам | Sentence-BERT |
| **Max pooling** | Максимум по каждому измерению | Инвариантность к порядку |
| **CLS-token** | Вектор `[CLS]` (первый токен) | BERT-Base |

## Sentence-BERT (SBERT)

Mean pooling — стандарт для симметричного семантического поиска:

$$
\text{sent\_emb} = \frac{1}{T} \sum_{t=1}^{T} h_t
$$

где $h_t$ — скрытое состояние $t$-го токена в последнем слое BERT.

## API

```csharp
using AI.ONNX.NLP.Bert;

// Загрузка предобученной модели из папки
// (vocab.txt, tokenizer_config.json, model.onnx, config.json)
var embedder = BertEmbedder.FromPretrained("sentence-transformers/all-MiniLM-L6-v2/");

// Получение эмбеддинга предложения (mean pooling)
Vector v1 = embedder.ForwardSBert("Привет, как дела?");
Vector v2 = embedder.ForwardSBert("Добрый день, что нового?");

// Косинусное сходство
double sim = v1.CosineSimilarity(v2);
Console.WriteLine($"Сходство: {sim:F4}");

// Блоковый пулинг для длинных текстов
var blocks = new[] { "Часть 1...", "Часть 2...", "Часть 3..." };
var weights = new double[] { 1.0, 1.0, 0.5 };
Vector docEmb = embedder.ForwardBlockPooling(blocks, weights);
```

## BertInfer (низкоуровневый API)

```csharp
using AI.ONNX.NLP.Bert;

using var infer = new BertInfer("model.onnx");

// Три входа: input_ids, attention_mask, token_type_ids
var inputIds      = new int[] { 101, 7592, 2088, 102 };
var attentionMask = new int[] { 1,   1,    1,    1   };
var tokenTypes    = new int[] { 0,   0,    0,    0   };

Vector[] hiddenStates = infer.Forward(inputIds, attentionMask, tokenTypes);
// hiddenStates[i] — вектор i-го выходного тензора модели
```
