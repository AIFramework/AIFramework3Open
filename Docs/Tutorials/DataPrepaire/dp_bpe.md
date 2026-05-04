# BPE-токенизатор (AI.DataPrepaire)

## Byte-Pair Encoding

BPE — алгоритм, строящий подсловный словарь путём итеративного объединения наиболее частых пар байт/символов.

**Алгоритм обучения:**
1. Инициализировать словарь: все уникальные байты/символы
2. Подсчитать частоту всех пар в корпусе
3. Объединить самую частую пару в новый токен
4. Повторять до достижения `MaxNGrammSize` или нужного размера словаря

**Пример:** `"l o w"` + `"l o w e r"` → частая пара `l,o` → токен `lo` → словарь расширяется.

## Коэффициент сжатия

$$
\text{ratio} = \frac{\text{число BPE-токенов}}{\text{число символов}}
$$

Значение < 1 означает сжатие (меньше токенов, чем символов).

| Модель | Размер словаря | Среднее ratio |
|--------|---------------|--------------|
| GPT-2  | 50 257 | ≈ 0.25 |
| BERT   | 30 522 | ≈ 0.30 |
| Claude | ~100K | ≈ 0.20 |

## API

```csharp
using AI.DataPrepaire.Tokenizers.TextTokenizers;

// BPECore — низкоуровневый байтовый BPE
var bpc = new BPECore { MaxNGrammSize = 8 };

// Обучение на корпусе (массив byte-массивов, по одному на "документ")
var bytes = words.Select(BPECore.GetBytes).ToArray();
bpc.TrainBPE(bytes);

// Токенизация
int[] ids = bpc.Tokenize("hello world");
int[] ids2 = bpc.Tokenize(BPECore.GetBytes("hello world"));

// BPE<T> — generic BPE с кастомным алфавитом
var bpe = new BPE<string>(decoder, encoder);
int[] encoded = bpe.Encode(tokens);
string[] decoded = bpe.Decode(encoded);
```
