# TF-IDF (AI.NLP)

## Term Frequency — Inverse Document Frequency

TF-IDF — классическая схема взвешивания терминов в документах, применяемая в информационном поиске и NLP.

## Формулы

**Term Frequency (TF)** — доля вхождений термина в документе:

$$
\text{TF}(t, d) = \frac{C(t, d)}{\sum_{t'} C(t', d)}
$$

**Inverse Document Frequency (IDF)** — обратная документная частота:

$$
\text{IDF}(t) = \log\frac{N}{1 + |\{d : t \in d\}|}
$$

где $N$ — число документов.

**TF-IDF**:

$$
\text{TF-IDF}(t, d) = \text{TF}(t, d) \times \text{IDF}(t)
$$

Высокий TF-IDF → термин часто встречается в данном документе, но редко в других → хороший дескриптор темы.

## Применения

| Задача | Использование TF-IDF |
|--------|---------------------|
| Поиск документов | Ранжирование по сумме TF-IDF запроса |
| Тематическое моделирование | Ключевые слова темы = топ TF-IDF |
| Классификация текста | Вектор TF-IDF как признаковое пространство |
| Суммаризация | Вес предложения = сумма TF-IDF его слов |

## API

```csharp
using AI.NLP;

string[] documents = { "нейронная сеть обучение", "рынок акции биржа", "спорт футбол матч" };
var tfidf = new TFIDF(documents);

// Метрики для конкретного термина и документа
double tf    = tfidf.TFWord("нейронная", docIndex: 0);
double idf   = tfidf.IDFWord("нейронная");
double score = tfidf.TF_IDF("нейронная", docIndex: 0);

// TF-IDF строки (суммирует по всем словам строки)
double strScore = tfidf.TF_IDF_Str("нейронная сеть", docIndex: 0);

// Поиск: возвращает индекс наиболее релевантного документа
int bestDoc = tfidf.Search("нейронная обучение");
```

