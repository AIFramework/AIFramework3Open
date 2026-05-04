# `StringExtention.cs` — статический класс `StringExtention`

## Теория

**Вспомогательные методы расширения** для `string` и `string[]`: склейка массива строк с разделителем, разбиение по строке-разделителю, массовое удаление подстрок, замены через регулярные выражения, **«разность» строк** `Diff`: удаление первого вхождения подстроки `text2` из `text1` посимвольно по специальному проходу (используется в `WordEndingsRU` для оценки окончания).

Замечание по реализации **`Remove`**: в исходном коде в цикле вызывается `text.Replace` вместо накопления в `ret` — фактическое поведение может отличаться от ожидаемого «удалить все вхождения всех подстрок»; ориентируйтесь на фактический код при отладке.

## API: `StringExtention`

| Метод | Назначение | Пример |
|-------|------------|--------|
| `Concatinate(strings, sep)` | Склейка массива строк. | `arr.Concatinate("\n");` |
| `Split(text, strSpliter)` | `Split` по строке-разделителю. | `s.Split("\t");` |
| `Remove(text, delStrs)` | Удаление подстрок (см. реализацию). | `text.Remove(new[] { "a", "b" });` |
| `ReReplace(text, pattern, new_string)` | `Regex.Replace`. | `text.ReReplace(@"\d+", "0");` |
| `ReTransform(text, pattern, transformer)` | Замена с функцией от совпадения. | `text.ReTransform("...", m => m.Value.ToUpper());` |
| `Diff(text1, text2)` | «Вычитание» первого вхождения `text2` из `text1`. | `words[i].Diff(stemmed);` |

Исходник: `src/AI.NLP/StringExtention.cs`.
