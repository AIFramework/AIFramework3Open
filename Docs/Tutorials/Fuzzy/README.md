# Туториалы: методы нечёткого вывода

Ниже приведены учебные тексты по четырём классическим схемам агрегирования и дефаззификации, реализованным в сборке **`AI.Fuzzy`**. Стиль: краткая теория, соответствие классам и методам API, без избыточной рекламы возможностей библиотеки.

| Тема | Файл |
|------|------|
| Мамдани (Mamdani) | [Mamdani.md](Mamdani.md) |
| Ларсен (Larsen) | [Larsen.md](Larsen.md) |
| Такаги–Сугено (Takagi–Sugeno) | [Sugeno.md](Sugeno.md) |
| Цукамото (Tsukamoto) | [Tsukamoto.md](Tsukamoto.md) |

Обзор архитектуры модуля, пространств имён (`AI.Fuzzy`, `AI.Fuzzy.Sets`, `AI.Fuzzy.Inference`, …) и зависимостей от **`AI.ML`** / **`AI.KNN`**: [../../Architecture/FuzzyLogic.md](../../Architecture/FuzzyLogic.md).

Консольные примеры вызовов: проект **`Tests/Logic/FuzzyInferenceConsole`**. Демонстрация **`FuzzyClassifier`**: **`Tests/Logic/Fuzzy`**.
