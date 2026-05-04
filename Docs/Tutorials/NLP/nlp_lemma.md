# Лемматизация (AI.NLP)

## Что такое лемматизация?

Лемматизация — приведение слова к его словарной (нормальной) форме с учётом морфологии:
- Существительное → именительный падеж, единственное число
- Глагол → инфинитив
- Прилагательное → мужской род, единственное число, именительный падеж

## Иерархия лемматизаторов

```
ILemmatizer
├── LemmatizerBase (abstract)
│   ├── RussianLemmatizer      — встроенные правила для русского
│   ├── DictionaryLemmatizer   — словарный (форма → лемма)
│   └── IdentityLemmatizer     — тождественный (слово = лемма)
└── CachingLemmatizer          — обёртка с LRU-кешем
```

## Стемминг vs Лемматизация

| Критерий | StemmerRus | RussianLemmatizer |
|----------|-----------|-------------------|
| Результат | Псевдооснова | Словарная форма |
| Скорость | Очень быстро | Быстро |
| Точность | Средняя | Высокая |
| Словарь | Не нужен | Встроен |
| Применение | Поиск, хеши | Семантика, NLU |

## CachingLemmatizer

Кеширует ранее вычисленные результаты для повторно встречающихся слов (LRU-кеш):

$$
\text{Ускорение} \approx \frac{\text{словарь}}{\text{уникальных запросов}} \times \text{коэффициент повторений}
$$

## API

```csharp
using AI.NLP.Lemmatization;

// Быстрое создание (рекомендуется)
ILemmatizer lem = Lemmatizer.CreateRussian(withCache: true);
// withCache: true автоматически оборачивает в CachingLemmatizer

// Напрямую через синглтон
var rus = RussianLemmatizer.Instance;

// Лемматизация
string lemma = lem.Lemmatize("учатся");      // → "учиться"
string sent  = lem.LemmatizeSentence("Ученики учатся в школах");
// → "ученик учиться в школа"

string[] all = lem.LemmatizeAll(words);      // массово

// Кеширующий лемматизатор
var base_lem = new RussianLemmatizer();
var cached   = new CachingLemmatizer(base_lem, maxSize: 10_000);
cached.Lemmatize("машинного");
Console.WriteLine(cached.CacheSize);  // → 1

// Словарный лемматизатор из файла (TSV: форма\tлемма)
var dictLem = DictionaryLemmatizer.LoadFromFile("dict.tsv", fallback: rus);
```
