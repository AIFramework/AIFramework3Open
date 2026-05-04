# Нормализация текста (AI.NLP)

## Задачи предобработки текста

Перед анализом текст необходимо привести к стандартной форме: убрать лишние символы, унифицировать регистр, удалить дубликаты.

## Методы TextStandard

| Метод | Что делает |
|-------|-----------|
| `Normalize(text, isLower)` | Убирает лишние пробелы, нормализует пунктуацию, опционально нижний регистр |
| `OnlyCharsAndDigit(text)` | Оставляет только буквы и цифры |
| `OnlyChars(text)` | Оставляет только буквы |
| `OnlyRusChars(text)` | Оставляет только кириллицу |
| `NoDoubleWord(text)` | Удаляет подряд идущие повторяющиеся слова |

## Коэффициент Дайса

Мера сходства двух множеств слов $A$ и $B$:

$$
\text{Dice}(A, B) = \frac{2 |A \cap B|}{|A| + |B|}
$$

Значение 0 — нет пересечения, 1 — множества идентичны.

**Асимметричный Дайс** показывает, какая доля слов из $A$ присутствует в $B$:

$$
\text{Dice}_{\text{asym}}(A, B) = \frac{|A \cap B|}{|A|}
$$

Используется для проверки: «содержит ли документ $B$ тематику документа $A$?»

## API

```csharp
using AI.NLP;

// Нормализация
string norm = TextStandard.Normalize("  Привет,  Мир!!  ", isLower: true);
// → "привет мир"

string rusOnly = TextStandard.OnlyRusChars("Hello, Привет! 123");
// → "Привет"

string noDup = TextStandard.NoDoubleWord("кот кот прыгнул прыгнул");
// → "кот прыгнул"

// Словарная метрика Дайса
var set1 = TextStandard.GetWords(
    text1,
    preprocessingString: s => s.ToLower(),
    preprocessingWord:   s => s,
    appendWord:          s => s.Length > 2  // фильтр коротких слов
);
var set2 = TextStandard.GetWords(text2, s => s.ToLower(), s => s, s => s.Length > 2);

double dice     = TextStandard.SimTextDice(set1, set2);
double diceAsym = TextStandard.SimTextDiceAsymmetric(set1, set2);
```
