# Участие в разработке AIFramework 3.0 Open

## Состав модулей

- **[src/AI.ML/](src/AI.ML/)** — машинное обучение (нейросети, классификаторы, кластеризация, HMM и т.д.), сборка `AI.ML.dll`. Ранее входило в `AICrossPlatform`.
- **[src/AI.KNN/](src/AI.KNN/)** — классификатор k-ближайших соседей (`KNNCl`), сборка `AI.KNN.dll`. Зависит от `AI.ML` (интерфейс `IClassifier`, наборы данных). Вынесен из `AI.ML` для явной зависимости и прозрачного подключения.
- **[src/AI.Fuzzy/](src/AI.Fuzzy/)** — нечёткая логика и связка с ML в одной сборке `AI.Fuzzy.dll`: пространства имён **`AI.Fuzzy`** (FLV, классические термы, вывод по аналогии), **`AI.Fuzzy.Sets`** (обобщённые нечёткие множества для связки с `AI.Logic`), **`AI.Fuzzy.Inference`**, **`AI.Fuzzy.Control`**, **`AI.Fuzzy.Fuzzification`**, **`AI.ML.Classification`** (`FuzzyClassifier`). Зависит от **`AI`**, **`AI.ML`** и **`AI.KNN`**. Обзор: [Docs/Architecture/FuzzyLogic.md](Docs/Architecture/FuzzyLogic.md); туториалы: [Docs/Tutorials/Fuzzy/README.md](Docs/Tutorials/Fuzzy/README.md).
- **[src/AI.NLP/](src/AI.NLP/)** — текст: стеммер, словари вероятностей, BoW, TF‑IDF, токенизация; сборка `AI.NLP.dll`. Зависит только от `AI`. Обзор: [Docs/Architecture/NLP.md](Docs/Architecture/NLP.md); туториалы: [Docs/Tutorials/NLP/README.md](Docs/Tutorials/NLP/README.md).
- **[src/AI.ControlSystems/](src/AI.ControlSystems/)** — САУ: модели объекта и обратной задачи (`ComplexObjectControl`), классические PID (`AI.ControlSystems.Pid`), сборка `AI.ControlSystems.dll`. Зависит от `AI` и `AI.ML`. Обзор: [Docs/Architecture/ControlSystems.md](Docs/Architecture/ControlSystems.md).
- **[src/AI.ComputerVision/](src/AI.ComputerVision/)** — компьютерное зрение (фильтры, преобразования, ONNX-экстрактор), сборка `AI.ComputerVision.dll` (.NET Framework 4.7.2). Зависит от `AI`, `AI.DSP`, `AI.DataPrepaire`, `AI.ONNX`.
- **[src/AI.Charts/](src/AI.Charts/)** — WinForms-графики (`AI.Charts.*`), сборка `AI.Charts.dll`. Зависит от `AI`, `AI.DSP`, `AI.ComputerVision`.

## Сборка

Каноническое решение для полной сборки всех библиотек и автотестов:

```bash
dotnet restore AIFramework.sln
dotnet build AIFramework.sln -c Release
dotnet test AIFramework.sln -c Release --no-build
```

Локальная упаковка NuGet (пример — ядро `AI`):

```bash
dotnet pack src/AI/AI.csproj -c Release -o ./artifacts
```

Версия и метаданные пакетов задаются в [Directory.Build.props](Directory.Build.props). Для SDK-проектов в каталоге `src/` включено `IsPackable` и `PackageId` по имени сборки.

## Git hooks и стиль (AI.ML)

- Рекомендуемая установка проверки перед коммитом: `git config core.hooksPath .githooks` (из корня репозитория). Скрипт [.githooks/pre-commit](.githooks/pre-commit) выполняет `dotnet build` для [src/AI.ML/AI.ML.csproj](src/AI.ML/AI.ML.csproj).
- **StyleCop.Analyzers** для `AI.ML` можно подключить опционально через [src/AI.ML/StyleCop.props](src/AI.ML/StyleCop.props) (`Import` в `AI.ML.csproj`); на существующей базе ожидается большое число предупреждений — имеет смысл включать после настройки `stylecop.json` / глобальных подавлений.

## Тесты

- **xUnit:** проект [tests/unit/AIFramework.UnitTests/AIFramework.UnitTests.csproj](tests/unit/AIFramework.UnitTests/AIFramework.UnitTests.csproj) подключён к `AIFramework.sln` и выполняется в CI.
- **Интеграция AI.ML 4.x:** [Tests/AI.ML.Integration/AI.ML.Integration.csproj](Tests/AI.ML.Integration/AI.ML.Integration.csproj) — дымовые проверки доменов после рефакторинга.
- **Общий код тестов:** [tests/shared/AIFramework.TestHelpers/](tests/shared/AIFramework.TestHelpers/) — разбор вывода калькулятора (`ProcessorOutputReader`); подключайте этот проект вместо копирования логики в консольных тестах.
- **Консольные сценарии** в каталоге `Tests/` остаются дополнительными проверками и демо; при добавлении регрессий по возможности дублируйте критичные проверки в xUnit.

## CI

Workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) выполняет сборку и тесты на **Windows** (`windows-latest`), так как часть проектов зависит от WinForms (например `AI.Charts`).

## Качество кода

- Форматирование и базовые соглашения: [.editorconfig](.editorconfig) (корневой файл, `charset = utf-8`).
- В [Directory.Build.props](Directory.Build.props) включены анализаторы (`EnableNETAnalyzers`); унаследованный код оставлен с `Nullable` отключённым — новые файлы можно помечать `#nullable enable` по мере готовности.
- Соблюдайте стиль существующих модулей; избегайте несвязанных с задачей рефакторингов в одном PR.

## Лицензия

Вклад принимается на условиях **Apache 2.0**, в соответствии с корневой лицензией проекта.
