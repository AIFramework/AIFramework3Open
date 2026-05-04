# `TextStandard.cs` — статический класс `TextStandard`

## Теория

**Нормализация и очистка текста** для последующей токенизации и сравнения множеств слов.

- **`Normalize`**: если строка содержит подстроку `"base64"`, возврат без изменений; иначе приведение регистра, замена переводов строк и табуляций на пробелы, унификация знаков препинания, замена `ё` → `е`, схлопывание пробелов и `..`.

- **`OnlyCharsAndDigit` / `OnlyChars` / `OnlyRusChars`**: фильтрация символов после `Normalize` — остаются буквы и цифры (и пробелы), только буквы, или только русские буквы в диапазоне `а`–`я` плюс пробелы.

- **`NoDoubleWord`**: удаление подряд идущих дубликатов слов (простой порядковый проход).

- **`GetWords`**: построение множества слов через пользовательские делегаты предобработки строки/слова и фильтра `appendWord`.

- **`SimTextDice` / `SimTextDiceAsymmetric`**: меры сходства множеств: Dice (симметричный коэффициент) и асимметричное отношение пересечения к мощности «основного» множества.

## API: `TextStandard`

| Метод | Назначение | Пример |
|-------|------------|--------|
| `Normalize(input, isLower)` | Общая нормализация. | `TextStandard.Normalize(text);` |
| `OnlyCharsAndDigit(input, isLower)` | Буквы, цифры, пробелы. | `TextStandard.OnlyCharsAndDigit(s);` |
| `OnlyChars(input, isLower)` | Буквы и пробелы. | `TextStandard.OnlyChars(s);` |
| `OnlyRusChars(input)` | Русские буквы и пробелы. | `TextStandard.OnlyRusChars(s);` |
| `NoDoubleWord(input)` | Убрать подряд повторяющиеся слова. | `TextStandard.NoDoubleWord(s);` |
| `GetWords(input, preprocessingString, preprocessingWord, appendWord)` | Множество слов с кастомной логикой. | См. сигнатуру в исходнике |
| `SimTextDice(set1, set2)` | Dice между множествами. | `TextStandard.SimTextDice(a, b);` |
| `SimTextDiceAsymmetric(main, set)` | Асимметричное сходство. | `TextStandard.SimTextDiceAsymmetric(main, other);` |

Исходник: `src/AI.NLP/TextStandard.cs`.
