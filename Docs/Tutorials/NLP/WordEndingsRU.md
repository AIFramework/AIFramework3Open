# `Stemmers/WordEndingsRU.cs` — `WordEndingsRU`

## Теория

**Оценка «окончания» слова** как разности между исходным словом и его стемом: для каждого слова после фильтрации `TextStandard.OnlyRusChars` и разбиения по пробелам вычисляется `words[i].Diff(StemmerRus.TransformingWord(words[i]))` (см. [StringExtention.md](StringExtention.md)). При ошибке в `Diff` подставляется пустая строка.

Полезно для лингвистических экспериментов и отладки стеммера, а не как строгий морфологический анализ.

## API: `WordEndingsRU`

| Метод | Описание | Пример |
|-------|----------|--------|
| `static Endings(string text)` | Массив «окончаний» по словам строки. | `WordEndingsRU.Endings("мышь бежит");` |

Исходник: `src/AI.NLP/Stemmers/WordEndingsRU.cs`.
