# BertConfig / BERT-архитектура (AI.ONNX)

## Архитектура BERT

BERT (Bidirectional Encoder Representations from Transformers) — стек трансформерных энкодеров:

$$
\text{BERT}(x) = \text{Encoder}_L(\ldots\text{Encoder}_1(\text{Embed}(x))\ldots)
$$

Каждый слой энкодера состоит из:
1. **Multi-Head Self-Attention** — параллельные проекции $h$ «голов» внимания
2. **Feed-Forward Network** (FFN) — два линейных слоя с GELU
3. **Layer Normalization** и **Residual connections**

## Параметры слоя внимания

$$
Q = XW_Q, \quad K = XW_K, \quad V = XW_V
$$
$$
\text{Attention}(Q, K, V) = \text{softmax}\!\left(\frac{QK^\top}{\sqrt{d_k}}\right)V
$$

где $d_k = H / h$ — размер одной головы, $H$ — `hidden_size`, $h$ — `num_heads`.

## Число параметров

| Компонент | Формула | Пример (BERT-Base) |
|-----------|---------|-------------------|
| Embeddings | $V \cdot H + L_{max} \cdot H$ | ~23 M |
| Attention | $4 \cdot H^2$ на слой | ~2.4 M × 12 |
| FFN | $2 \cdot H \cdot H_{ffn}$ на слой | ~7.1 M × 12 |
| **Итого** | ≈ $12 H^2 N_{layers} + VH$ | **≈ 110 M** |

## Класс `BertConfig`

```csharp
using AI.ONNX.NLP.Bert;

// Загрузка из JSON (формат Hugging Face)
var cfg = BertConfig.FromJson("path/to/config.json");
Console.WriteLine($"HiddenSize:      {cfg.HiddenSize}");       // 768
Console.WriteLine($"NumHiddenLayers: {cfg.NumHiddenLayers}");  // 12
Console.WriteLine($"NumAttnHeads:    {cfg.NumAttentionHeads}");// 12
Console.WriteLine($"IntermediateSize:{cfg.IntermediateSize}"); // 3072
Console.WriteLine($"VocabSize:       {cfg.VocabSize}");        // 30522

// Программное создание конфига
var smallCfg = new BertConfig
{
    HiddenSize        = 384,
    NumHiddenLayers   = 6,
    NumAttentionHeads = 6,
    IntermediateSize  = 1536,
    VocabSize         = 30522,
    HiddenAct         = "gelu",
    MaxPositionEmbeddings = 512,
};
```

## Готовые модели (Hugging Face → ONNX)

| Модель | Hidden | Layers | Heads | Параметры |
|--------|--------|--------|-------|-----------|
| `all-MiniLM-L6-v2` | 384 | 6 | 12 | 22 M |
| `bert-base-uncased` | 768 | 12 | 12 | 110 M |
| `bert-large-uncased`| 1024 | 24 | 16 | 340 M |

```bash
# Конвертация в ONNX (optimum)
optimum-cli export onnx \
  --model sentence-transformers/all-MiniLM-L6-v2 \
  ./onnx_model/
```
