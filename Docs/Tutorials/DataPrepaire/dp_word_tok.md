# WordTokenizer (AI.DataPrepaire)

## Словарный токенизатор

`WordTokenizer` разбивает текст на слова, строит словарь (word → id) и предоставляет методы кодирования/декодирования.

## Закон Ципфа

В естественных языках частота $n$-го по частоте слова обратно пропорциональна его рангу:

$$
f(n) \propto \frac{1}{n^\alpha}, \quad \alpha \approx 1
$$

Это означает, что небольшое число слов покрывает бо́льшую часть текста (принцип Парето: топ-20% токенов ≈ 80% текста).

## Специальные токены

| Токен | ID | Назначение |
|-------|----|-----------|
| `UnknowToken` | -1 | Слово не в словаре |
| `PadToken`    |  0 | Выравнивание последовательности |
| `StartToken`  |  1 | Начало последовательности |
| `EndToken`    |  2 | Конец последовательности |

## API

```csharp
using AI.DataPrepaire.Tokenizers.TextTokenizers;

// Создание и обучение
var tok = new WordTokenizer(isLower: true);
tok.TrainFromText(corpus);
// или из файла:
tok.TrainFromTextFile("corpus.txt");

// Кодирование
int[] ids = tok.Encode("нейронная сеть обучается");
// → [5, 12, 3] (зависит от словаря)

// Декодирование
string text = tok.DecodeObj(ids);
// → "нейронная сеть обучается"

// Батч
int[,] batch = tok.EncodeBatch(sentences);

// Информация
Console.WriteLine($"Словарь: {tok.DictLen} токенов");
```

## Создание с предопределённым словарём

```csharp
string[] decoder = new string[vocabSize]; // id → слово
var encoder = new Dictionary<string, int>(); // слово → id

var tok = new WordTokenizer(decoder, encoder, s => s.ToLower());
```
