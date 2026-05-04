# TextTokenizer (AI.NLP)

## Словарный токенизатор

`TextTokenizer` строит словарь из обучающего текста и кодирует последовательности в числовые векторы.

## Обучение

1. Разбить текст на токены (слова)
2. Подсчитать частоты и отобрать топ-`Count` слов
3. Присвоить каждому токену числовой ID

## Кодирование последовательностей

Каждое слово заменяется его ID в словаре. OOV (out-of-vocabulary) слова получают специальный ID.

## One-Hot кодирование

Слово $w$ с ID $i$ кодируется как вектор $e_i \in \{0,1\}^{|V|+1}$, где только $e_i[i] = 1$.

Для нейросетей вместо one-hot используют эмбеддинги:
$$
\text{emb}(w) = W_e \cdot \text{OneHot}(w), \quad W_e \in \mathbb{R}^{d \times |V|}
$$

## API

```csharp
using AI.NLP;

var tok = new TextTokenizer(
    isLower:    true,    // нижний регистр
    isStem:     false,   // без стемминга
    deleted:    null,    // дополнительные удаляемые символы
    separaters: null     // разделители (по умолч. пробел)
) { Count = 100 };       // размер словаря

// Обучение
tok.Train(corpusText);

// Доступ к словарю
string[] vocab = tok.Words;           // массив [ID → слово]
int id = tok.GetWord2Token("сеть");   // слово → ID
int dim = tok.GetDimWithUnKnowWord(); // |V| + 1

// Кодирование последовательности
Vector ids = tok.GetSeq2Tokens("нейронная сеть обучается");
// → Vector[3, 12, 7]  (IDs)

// One-Hot
Vector oh = tok.GetWord2OneHot("нейронная");
// → разреженный вектор длиной dim
```
