# `TextTokenizer.cs` — класс `TextTokenizer`

## Теория

**Словарь индексов по корпусу.** На этапе **`Train`** текст предобрабатывается (`Preproc`: удаление символов из `DelChars`, замена разделителей `Separaters` на пробелы, приведение к нижнему регистру по флагу). Затем строится **`ProbabilityDictionary(false, false, IsStem)`** — без фильтра стоп-слов и цифр в смысле флагов конструктора, с опциональным стеммингом. По убыванию частот берутся слова; им назначаются индексы $0 \ldots$ (либо первые `WordCount` слов, если `WordCount >= 0`).

**Токенизация последовательности** (`GetSeq2Tokens`): вектор длины `Count` (по умолчанию 50), заполняется индексами слов из словаря; неизвестные слова получают **-1**. Начальное заполнение: `new Vector(Count) - 1` (в коде библиотеки).

**Одно слово** (`GetWord2Token`): индекс в словаре или значение `dictionary.Count` как метка «неизвестно» (размерность one-hot = `dictionary.Count + 1`).

## API: `TextTokenizer`

| Член | Описание | Пример |
|------|----------|--------|
| `IsLower`, `IsStem`, `DelChars`, `Separaters` | Параметры предобработки. | Задать до `Train` |
| `Count` | Длина вектора в `GetSeq2Tokens`. | `tok.Count = 32;` |
| `WordCount` | Лимит слов в словаре; `-1` — все из `Run`. | `tok.WordCount = 100;` |
| `Words` | Массив слов по индексу после `Train`. | `tok.Words[i]` |
| `TextTokenizer(isLower, isStem, deleted, separaters)` | Конструктор; значения по умолчанию для `DelChars` / `Separaters`. | `new TextTokenizer()` |
| `Train(string text)` | Построить словарь. | `tok.Train(корпус);` |
| `GetSeq2Tokens(string seq)` | Вектор индексов фрагмента. | `Vector v = tok.GetSeq2Tokens("фраза");` |
| `GetWord2Token(string word)` | Индекс одного слова. | `int id = tok.GetWord2Token("слово");` |
| `GetWord2OneHot(string word)` | One-hot в размерности `GetDimWithUnKnowWord()`. | `Vector oh = tok.GetWord2OneHot(word);` |
| `GetDimWithUnKnowWord()` | Размерность с учётом неизвестного класса. | `int dim = tok.GetDimWithUnKnowWord();` |

Исходник: `src/AI.NLP/TextTokenizer.cs` (в коде указано намерение перенести функционал в токенизаторы `AI.DataPrepaire`).
