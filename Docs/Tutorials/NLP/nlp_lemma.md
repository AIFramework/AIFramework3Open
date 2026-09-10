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
│   ├── MorphologicalLemmatizer — определяет часть речи, потом применяет её правила
│   ├── RussianLemmatizer       — только правила по суффиксу, без разбора части речи
│   ├── DictionaryLemmatizer    — словарный (форма → лемма)
│   └── IdentityLemmatizer      — тождественный (слово = лемма)
└── CachingLemmatizer           — обёртка с LRU-кешем
```

`Lemmatizer.CreateRussian()` возвращает `MorphologicalLemmatizer`. Прежнее поведение —
одни суффиксальные правила — доступно как `Lemmatizer.CreateRussianRules()`.

## Стемминг vs Лемматизация

| Критерий | StemmerRus | RussianLemmatizer | MorphologicalLemmatizer |
|----------|-----------|-------------------|-------------------------|
| Результат | Псевдооснова | Словарная форма | Словарная форма |
| Точность на эталонном корпусе | не сравнима | 61.1 % | 82.8 % |
| Существительные | — | 4.4 % | 61.5 % |
| Часть речи | не знает | не знает | 96.2 % |
| Поведение при неудаче | режет всегда | возвращает слово нетронутым | чаще приводит к основе |
| Применение | Поиск, хеши | Показ пользователю | Поиск, индексация, NLU |

Числа измерены на встроенном корпусе из 247 разобранных вручную словоформ
(`LemmaCorpus.Russian`), проверка — `LemmatizerEvaluation.Evaluate`. Подробности
и оговорки: [Docs/Architecture/NLP.md](../../Architecture/NLP.md).

Обратите внимание на предпоследнюю строку: у большей точности есть цена. Не найдя
правила, `RussianLemmatizer` возвращает слово нетронутым — оно остаётся узнаваемым.
`MorphologicalLemmatizer` чаще приводит слово к основе: «книгой» → «книг». Для поиска
это выигрыш (все формы слова сходятся к одному ключу), для показа человеку — нет.

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
// → "ученик учиться в школ"
// «школ», а не «школа»: род существительного по одной форме не восстановить,
// и правило выбирает нулевое окончание — см. соглашение в Docs/Architecture/NLP.md

// Часть речи и полный разбор
MorphAnalysis a = MorphologicalLemmatizer.Instance.Analyze("городами");
// a.Lemma == "город", a.PartOfSpeech == PartOfSpeech.Noun

PartOfSpeech pos = RussianPosTagger.Instance.Tag("читаем");   // Verb

string[] all = lem.LemmatizeAll(words);      // массово

// Кеширующий лемматизатор
var base_lem = new RussianLemmatizer();
var cached   = new CachingLemmatizer(base_lem, maxSize: 10_000);
cached.Lemmatize("машинного");
Console.WriteLine(cached.CacheSize);  // → 1

// Словарный лемматизатор из файла (TSV: форма\tлемма)
var dictLem = DictionaryLemmatizer.LoadFromFile("dict.tsv", fallback: rus);
```

## Из языка сценариев

```
nlp.lemma("столами")                    // "стол"
nlp.pos("столами")                      // "NOUN"
nlp.analyze("городами")                 // { word, lemma, pos, pos_name }
nlp.morph("Я читал книги в городах")    // таблица: слово · лемма · часть речи
nlp.morph_quality()                     // измеренное качество разбора
```

`nlp.morph_quality()` отдаёт не только числа, но и поле `explain` — разбор словами:
что измерено, чего метод не умеет и почему. Разбор ошибается, и тот, кто строит на нём
выводы, вправе знать насколько — до того, как построит.
