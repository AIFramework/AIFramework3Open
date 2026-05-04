# `ProbDictHash.cs` — `ProbabilityDictionaryHash`

## Теория

**Нормированные частоты в хеше.** Токены получаются через **`ProbabilityDictionary.GetWords(text, IsStem)`** (отдельный путь токенизации, без таблицы «вероятностей» из `ProbabilityDictionary.Run`). Для каждого уникального токена считается число вхождений; значение в словаре — **отношение этого числа к общему числу токенов** в тексте (сумма по ключам в смысле подсчёта равна 1 при непустом списке токенов). Пустой текст даёт пустой `Dictionary`.

Используется внутри **`TFIDF`** как представление «TF» по документу.

## API: `ProbabilityDictionaryHash`

| Член | Описание | Пример |
|------|----------|--------|
| `pDictionary` | Результат последнего `Run`: термин → нормированная частота. | `var map = hash.Run(text);` |
| `IsStem` | Передаётся в `GetWords` как признак стемминга. | `hash.IsStem = false;` |
| `ProbabilityDictionaryHash(bool isStem = true)` | Конструктор. | `new ProbabilityDictionaryHash()` |
| `Run(string text)` | Построить словарь частот для одного текста. | `hash.Run("один два два");` |

Исходник: `src/AI.NLP/ProbDictHash.cs`.
