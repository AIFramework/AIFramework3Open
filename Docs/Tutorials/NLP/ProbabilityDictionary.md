# `ProbabilityDictionary.cs` и `ProbabilityDictionaryData.cs`

## Теория

**Частотный анализ токенов.** Текст приводится к нижнему регистру, режется по пробелам, переводам строк и ряду символов (`[`, `]`, `-` и т.д.). Для каждого фрагмента выполняется обрезка пунктуации, опционально **стемминг** русского слова (`StemmerRus`). Глобальный список стоп-слов хранится в статическом поле `ProbabilityDictionary.stop` (по умолчанию пустой); применяется ли он — решает per-instance флаг `IsStopDel` (он же параметр `isStopDel` конструктора). Конструктор больше не меняет статическое поле как побочный эффект.

Для каждого **уникального** токена $w$ задаётся «вероятность» как отношение числа его вхождений к $n$, где $n$ — **число фрагментов после первичного `Split`** исходной строки (включая пустые сегменты), а не длина списка после фильтров. Результат сортируется по убыванию этой величины.

Статический метод **`GetWords(text, IsStem)`** использует `TextStandard.OnlyCharsAndDigit`, разбиение по пробелу и тот же список стоп-слов — удобен как единая токенизация для TF‑IDF и хеш-словаря.

## API: `ProbabilityDictionaryData<T>`

| Член | Описание |
|------|----------|
| `T Word { get; set; }` | Токен (строка для `T = string`). |
| `double Probability { get; set; }` | Вес в смысле реализации (доля от $n$). |

## API: `ProbabilityDictionary`

| Член | Теория / поведение | Пример |
|------|-------------------|--------|
| `pDictionary` | Последний результат `Run` (массив пар слово–вес). | После `Run` читать топ слов. |
| `stop`, `StopWords` | Общий (статический) список стоп-слов. Изменяется пользователем, конструктором не мутируется. | `ProbabilityDictionary.stop = new[] { "и", "а" };` |
| `IsStopDel` | Использовать ли `stop` у этого экземпляра. | `pd.IsStopDel = false;` — отключить фильтр локально. |
| `IsDigitDel`, `IsStem` | Удалять ли токены с цифрами/разделителями; стемминг. | Настройка перед `Run`. |
| `ProbabilityDictionary(isStopDel, isDigitDel, isStem)` | Инициализация флагов. | `new ProbabilityDictionary(true, true, true)` |
| `Run(string text)` | Полный конвейер: токены → подсчёт → сортировка. | `var table = pd.Run("текст ...");` |
| `GetWordsRunAll(text)` | Все слова по убыванию веса (через `Run`). | `string[] words = pd.GetWordsRunAll(text);` |
| `GetWordsRun(text, numW)` | Первые `numW` слов из отсортированного списка. | `var top = pd.GetWordsRun(text, 10);` |
| `ToString(int index)` | Текстовый дамп первых `index` записей. | `Console.WriteLine(pd.ToString(5));` |
| `GetWords(string text)` | Заполняет внутренний список токенов (используется внутри `Run`). | Обычно вызывается только косвенно через `Run`. |
| `static GetWords(text, IsStem)` | Токены без создания таблицы вероятностей. | `ProbabilityDictionary.GetWords(text, IsStem: true)` |

Исходники: `src/AI.NLP/ProbabilityDictionary.cs`, `src/AI.NLP/ProbabilityDictionaryData.cs`.
