# `BoWModel.cs` — класс `BoWModel`

## Теория

**Мешок слов (Bag of Words).** Задаётся фиксированный **упорядоченный словарь** (массив строк), обычно полученный из частотного анализа корпуса. Для нового текста считается вектор длины `Len`: в позиции $j$ — число вхождений токена (после `ProbabilityDictionary.Run`), совпадающего с `model[j]` (с учётом `Trim('\r')` при сравнении).

Опционально **`IsNormalise`**: деление на максимум компонент (с малым смещением) и вычитание среднего по вектору (`Statistic` из `AI.Statistics`).

**Генерация словаря** (`ModelGen`): один прогон `ProbabilityDictionary` по тексту, слова по убыванию «вероятности» записываются построчно в файл.

## API: `BoWModel`

| Член | Описание | Пример |
|------|----------|--------|
| `vector` | Последний вычисленный вектор BoW. | После `GetVector` |
| `isStop`, `isDig` | Флаги для внутреннего `ProbabilityDictionary`. | Перед `GetVector` |
| `Len` | Размер словаря. | `bow.Len` |
| `IsNormalise` | Включить нормализацию вектора. | `bow.IsNormalise = true;` |
| `BoWModel(string pathModel)` | Загрузка словаря из файла (текст режется по ` .,!\t\n`). | `var bow = new BoWModel("vocab.txt");` |
| `GetVector(string text)` | Вектор подсчётов по словарю. | `Vector v = bow.GetVector("новый текст");` |
| `static ModelGen(text, path, isStop)` | Записать словарь в файл. | `BoWModel.ModelGen(corpus, "vocab.txt");` |

Исходник: `src/AI.NLP/BoWModel.cs`.
