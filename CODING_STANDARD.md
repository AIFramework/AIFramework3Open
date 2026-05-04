# Стандарт кода AIFramework 3.0

Настоящий документ определяет единые правила для всех 23 библиотек фреймворка.

---

## 1. Форматирование

| Правило | Значение |
|---------|----------|
| Кодировка | UTF-8 (BOM не нужен) |
| Перенос строки | CRLF (Windows) |
| Отступ | 4 пробела, без табуляции |
| Пространства имён | **file-scoped** (`namespace Foo.Bar;`) |
| Максимальная длина строки | 140 символов (рекомендация) |

## 2. Именование

| Элемент | Стиль | Пример |
|---------|-------|--------|
| Тип (class, struct, enum, interface) | PascalCase | `KMeans`, `LinearRegression` |
| Интерфейс | `I` + PascalCase | `IClassifier`, `IRegressor` |
| Метод, свойство, событие | PascalCase | `Train`, `Predict`, `Height` |
| Параметр, локальная переменная | camelCase | `clusterCount`, `learningRate` |
| Приватное поле | `_camelCase` | `_centroids`, `_weights` |
| Константа | PascalCase | `DefaultSeed`, `MaxIterations` |
| Пространство имён | `AI.*` по домену | `AI.ML.Classification`, `AI.DSP.DSPCore` |

Имена — на английском языке, без опечаток и транслитерации.

## 3. XML-документация

- **Обязательна** для всех `public` и `protected` членов.
- Язык — **русский** (единый стиль проекта).
- Формат: `<summary>`, `<param>`, `<returns>`, `<example>` при необходимости.
- Кодировка файла — **UTF-8**; перед коммитом убедитесь, что кириллица не повреждена.

## 4. Типы данных в публичном API

### Правило

> В публичных сигнатурах (`public`, `protected`) используются **только** типы из `AI.DataStructs`:
> `Vector`, `Matrix`, `NDTensor`, `ComplexVector`, `ComplexMatrix`.
>
> `double[]`, `double[,]`, `float[]` — допустимы только в `internal` и `private` коде.

### Обоснование

Единые типы данных обеспечивают совместимость между библиотеками (результат `AI.DSP` можно напрямую передать в `AI.ML`), сохраняя при этом производительность через внутреннее поле `.Data`.

### Быстрые пути (internal)

Для горячих циклов допустимо обращаться к `vector.ToArray()`, `matrix.Data` или `Span<double>` — но **только внутри реализации**, не в публичных сигнатурах.

### Tensor-граница

| Тип | Область применения |
|-----|--------------------|
| `NDTensor` (ядро `AI`) | Универсальный многомерный тензор для данных и ML |
| `Tensor` (`AI.NeuralNetworks.V2`) | Autograd-тензор; **не экспортируется** за пределы `AI.NeuralNetworks` |

## 5. Архитектура алгоритмов

### Классификация API

| Паттерн | Когда использовать | Пример |
|---------|--------------------|--------|
| **`static class`** | Чистые функции без состояния | `Gauss.Solve(...)`, `FFT.Forward(...)` |
| **Экземплярный класс + интерфейс** | Алгоритмы с обучением / состоянием | `KMeans : IClustering`, `BayesianClassifier : IClassifier` |

### Иерархия интерфейсов

```
IAlgorithm                              ← маркерный интерфейс
├── IEstimator<TInput, TLabel>          ← Fit + Predict (классификация, регрессия)
│   ├── IClassifier                     ← Train/Classify (наследует IEstimator<Vector, int>)
│   ├── IRegressor                      ← Train/Predict (наследует IEstimator<Vector, double>)
│   └── IClustering                     ← Train/Classify (без меток)
└── ITransformer<TInput, TOutput>       ← Fit + Transform (PCA, TF-IDF, нормализация)
```

### Правила для обучаемых алгоритмов

1. Реализовать соответствующий интерфейс (`IClassifier`, `IRegressor`, `IClustering`, `ITransformer`).
2. Обучение — через метод `Train` / `Fit`, а не в конструкторе.
3. Безпараметрический конструктор должен быть доступен (для десериализации).
4. Если алгоритм поддерживает сериализацию — реализовать `ISavable`.

## 6. Анализаторы

- **Roslyn-анализаторы** (`EnableNETAnalyzers`) включены глобально.
- **StyleCop.Analyzers** подключён через `Directory.Build.props`; конфигурация в `src/stylecop.json`.
- `EnforceCodeStyleInBuild = true` — нарушения стиля видны при сборке.
- Уровень: `suggestion` (не блокирует сборку на унаследованном коде).

## 7. Прочие соглашения

- **Один тип = один файл** (допускаются `partial` для крупных классов).
- **using** — в начале файла, отсортированные; `System.*` первыми.
- **`#nullable enable`** — для новых файлов (унаследованный код остаётся с `disable`).
- **`#region`** — допустим для группировки крупных блоков; не для однострочного кода.
- **Тесты** — xUnit; имя: `ClassName_MethodName_ExpectedBehavior`.
