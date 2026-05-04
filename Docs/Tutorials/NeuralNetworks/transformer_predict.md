# Transformer для прогноза временных рядов

**Transformer** — архитектура на основе механизма самовнимания (self-attention), предложенная Vaswani et al. в 2017 г. («Attention is All You Need»). В отличие от RNN/LSTM, все шаги последовательности обрабатываются параллельно, что ускоряет обучение и позволяет напрямую моделировать дальние зависимости.

## Позиционное кодирование

Поскольку Transformer не имеет встроенного понятия порядка, к каждому вектору позиции $t$ прибавляется позиционный эмбеддинг:

$$
\text{PE}(t, 2k)   = \sin\!\left(\frac{t}{10000^{2k/d}}\right)
$$

$$
\text{PE}(t, 2k+1) = \cos\!\left(\frac{t}{10000^{2k/d}}\right)
$$

где $d$ — размер модели (`d_model`), $k = 0, 1, \ldots, d/2 - 1$.

## Multi-Head Self-Attention

Для матриц запросов $Q$, ключей $K$ и значений $V$ (линейные проекции входа):

$$
\text{Attention}(Q, K, V) = \text{softmax}\!\left(\frac{QK^{\top}}{\sqrt{d_k}}\right) V
$$

При $h$ головах внимания результаты конкатенируются и проецируются линейным слоем:

$$
\text{MultiHead}(Q, K, V) = \text{concat}(\text{head}_1, \ldots, \text{head}_h) W^O
$$

## Encoder-блок

Каждый `TransformerEncoderLayer` состоит из:

1. Multi-Head Self-Attention + Add & LayerNorm
2. Feed-Forward Network (`Linear → ReLU → Linear`) + Add & LayerNorm

## Архитектура для прогноза рядов

```
Вход (окно длины w) → Linear(1, d_model) → SinusoidalPositionalEncoding
    → TransformerEncoderLayer(d_model, nHead) → Linear(d_model, 1) → предсказание
```

## Параметры демо

| Параметр | Смысл |
|----------|-------|
| trainLen | Длина обучающего ряда |
| window   | Размер входного окна $w$ |
| predLen  | Горизонт прогноза |
| freq     | Частота базовой синусоиды |
| dModel   | Размер эмбеддинга / размерность модели $d$ |
| nHead    | Число голов внимания |
| epochs   | Число эпох обучения |
| lr       | Learning rate (Adam) |

## Transformer vs RNN для рядов

| Свойство | RNN / LSTM / GRU | Transformer |
|----------|------------------|-------------|
| Параллелизм обучения | Нет (последовательный) | Да |
| Длинные зависимости | Затухание градиентов | Прямое внимание $O(w^2)$ |
| Число параметров | Меньше | Больше |
| Требует больших данных | Нет | Да |
| Малые окна $w$ | Эффективен | Избыточен |

## Ограничения

- При малых наборах данных Transformer склонен к переобучению.
- Квадратичная сложность по окну $w$: $O(w^2 d)$ для вычисления внимания.
- Позиционное кодирование фиксированное — не адаптируется к частоте ряда.

## API

```csharp
using AI.ML.NeuralNetworks.V2.Nn;

var model = new Sequential(
    new Linear(1, dModel),
    new SinusoidalPositionalEncoding(dModel, maxLen: window),
    new TransformerEncoderLayer(dModel, nHead, ffDim: dModel * 4),
    new Linear(dModel, 1)
);
```
