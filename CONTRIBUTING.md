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

### Два варианта сборки: полная и лёгкая

| | Полная | Лёгкая |
|---|---|---|
| Запуск | `.\build.ps1` | `.\build.ps1 -Light` |
| Решение | `AIFramework.Full.sln` | `AIFramework.Light.sln` |
| Проекты | 68: библиотеки, модульные тесты, демо, примеры из решений, GPU, инструменты | 52: все библиотеки `src`, кроме GPU, и модульные тесты |
| Нативные библиотеки пакетов | всех платформ | только платформы, на которой идёт сборка |
| OpenBLAS | да | нет: нейросети считают собственным управляемым умножением |
| GPU (`AI.NeuralNetworks.Gpu`) | да | нет |
| Каталоги сборки | `bin`, `obj` | `bin-light`, `obj-light` |
| Размер в Release | 6,90 ГБ вместе с тестами | 0,42 ГБ вместе с тестами |
| Тестов | 2 473 | 2 411 |

Функциональность библиотек в лёгкой сборке та же: в неё входят все библиотеки `src`, кроме GPU,
и все модульные тесты — 2 411 тестов проходят. Отличий два. Нет GPU. Крупные матричные
операции нейросетей медленнее: `CpuBlas`, не найдя нативный OpenBLAS, переходит на своё
умножение с тем же результатом.

Вес полной сборки создают не наши сборки, а нативные библиотеки пакетов. ONNX Runtime кладёт
в каждый выход библиотеки под iOS, Android, macOS и Linux — 321 МБ на копию, из них под
`win-x64` лишь 9,6 МБ; OpenBLAS добавляет ещё 49 МБ. Лёгкая сборка отсекает их после
разрешения пакетов и до копирования (`Directory.Build.targets`), а восстановление пакетов
у обоих вариантов общее. Поэтому переключаться между ними можно в любой момент, а выходы
не смешиваются.

Ключи `build.ps1`:

- `-Light` — лёгкая сборка вместо полной;
- `-Test` — после сборки прогнать тесты выбранного варианта;
- `-Clean` — удалить каталоги сборки обоих вариантов;
- `-Configuration Debug` — отладочная конфигурация вместо Release.

В Visual Studio достаточно открыть `AIFramework.Light.sln`: свойство `AILightBuild` выставляется
по имени решения. Правила лёгкой сборки лежат в [build/Light.props](build/Light.props); его
подключает и корневой `Directory.Build.props`, и `Tests/Directory.Build.props`, который корневой
перекрывает.

Ограничение лёгкой сборки: нативные библиотеки в ней только для платформы, на которой собирали.
Для запуска на другой нужна полная сборка либо публикация под нужный `RuntimeIdentifier`.
Около пятидесяти консольных и WinForms-примеров в `Tests/` не входят ни в одно решение и собираются
по отдельности.

Каноническое решение для полной сборки всех библиотек и автотестов:

```bash
dotnet restore AIFramework.sln
dotnet build AIFramework.sln -c Release
dotnet test AIFramework.sln -c Release --no-build
```

### Пакеты NuGet

Пакеты всех библиотек собираются в каталог `artifacts/nuget`:

```powershell
.\build.ps1 -Pack
.\build\nuget-push.ps1 -DryRun
```

Вторая команда проверяет зависимости между пакетами и показывает порядок выкладки, ничего не публикуя.

Версия задается в [Directory.Build.props](Directory.Build.props), описания, иконка и README пакетов находятся в [build/NuGet.props](build/NuGet.props) и [build/nuget/](build/nuget/). Для SDK-проектов в каталоге `src/` включено `IsPackable`, `PackageId` совпадает с именем сборки. Исключения: `AI.SignalLabs` (сборка называется `AI.SignalLab`) и `AI.Faiss`, который не публикуется, потому что содержит библиотеки Intel MKL и Faiss без текстов их лицензий.

**Публикация.** Для новой версии поменяйте `Version` в `Directory.Build.props` и запустите рабочий процесс [Publish NuGet](.github/workflows/publish-nuget.yml) вручную с галочкой `publish`. Ключи не нужны: nuget.org выдает временный ключ по политике Trusted Publishing. Опубликованную версию нельзя удалить, только скрыть, поэтому сначала стоит сделать запуск без галочки. Подробности, запасной путь с ключом и частые проблемы описаны в [Docs/NuGet.md](Docs/NuGet.md).

## Git hooks и стиль (AI.ML)

- Рекомендуемая установка проверки перед коммитом: `git config core.hooksPath .githooks` (из корня репозитория). Скрипт [.githooks/pre-commit](.githooks/pre-commit) выполняет `dotnet build` для [src/AI.ML/AI.ML.csproj](src/AI.ML/AI.ML.csproj).
- **StyleCop.Analyzers** для `AI.ML` можно подключить опционально через [src/AI.ML/StyleCop.props](src/AI.ML/StyleCop.props) (`Import` в `AI.ML.csproj`); на существующей базе ожидается большое число предупреждений — имеет смысл включать после настройки `stylecop.json` / глобальных подавлений.

## Тесты

- **xUnit:** модульные тесты лежат в [Tests/unit/](Tests/unit/), тесты нейросетей — в `Tests/NNW.V2.*`. Все они, кроме GPU, прогоняются в CI на каждый push и pull request — см. [CI](#ci); локально запускайте их через `dotnet test`.
- **Интеграция AI.ML 4.x:** [Tests/AI.ML.Integration/AI.ML.Integration.csproj](Tests/AI.ML.Integration/AI.ML.Integration.csproj) — дымовые проверки доменов после рефакторинга.
- **Общий код тестов:** [Tests/shared/AIFramework.TestHelpers/](Tests/shared/AIFramework.TestHelpers/) — разбор вывода калькулятора (`ProcessorOutputReader`); подключайте этот проект вместо копирования логики в консольных тестах.
- **Консольные сценарии** в каталоге `Tests/` остаются дополнительными проверками и демо; при добавлении регрессий по возможности дублируйте критичные проверки в xUnit.

## CI

Выкладку пакетов выполняет отдельный рабочий процесс [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml), он запускается только вручную (см. [Пакеты NuGet](#пакеты-nuget)).

Проверку документации выполняет [`.github/workflows/docs-lint.yml`](.github/workflows/docs-lint.yml) («Docs lint»):

- **Что проверяет:** соответствие теоретической документации канону [Docs/Tutorials/STRUCTURE.md](Docs/Tutorials/STRUCTURE.md) через линтер [Tools/DocsLint](Tools/DocsLint/) (`dotnet run --project Tools/DocsLint -- --strict`).
- **Когда запускается:** push в `main` и pull request, если затронуты `Docs/Tutorials/**`, `Demo/WebUI/AiFrameworkDemo/AiFrameworkDemo/Modules/**`, `Tools/DocsLint/**` или сам workflow; плюс `workflow_dispatch` вручную.
- **Среда:** `ubuntu-latest`, .NET SDK `9.0.x`. Линтер не зависит от решения — `src/` и демо не собираются.
- **Ошибки** роняют проверку всегда; **предупреждения** — только новые, не занесённые в `Docs/Tutorials/.docslint-baseline`.

Локальный прогон перед PR (и автоисправление синонимов разделов):

```bash
dotnet run --project Tools/DocsLint -- --strict
dotnet run --project Tools/DocsLint -- --fix
```

Сборку и автотесты выполняет [`.github/workflows/build-and-test.yml`](.github/workflows/build-and-test.yml) («Build and test»):

- **Когда запускается:** push в любую ветку, pull request и вручную (`workflow_dispatch`). Новый push в ту же ветку отменяет незаконченный прогон.
- **Среда:** `windows-latest`, .NET SDK `9.0.x` и `10.0.x` (как локально: `global.json` нет, сборку ведёт SDK 10, тесты идут на среде выполнения .NET 9). Windows выбран потому, что локальный результат получен там: `AI.Charts.WinForms` и примеры нацелены на `net9.0-windows`, `libopenblas.dll` копируется рядом со сборкой только на Windows, нативные библиотеки `AI.Faiss` есть только под `win-x64`.
- **Что делает:** `dotnet build AIFramework.Full.sln -c Release`, отдельно собирает `Tests/AI.ML.Integration` (он не входит ни в одно решение) и запускает каждый xUnit-проект через `dotnet test -c Release --no-build`. Упавший тест роняет проверку; результаты в формате `.trx` прикладываются к прогону.
- **Список тестовых проектов** ведётся в самом workflow. Новый проект с `Microsoft.NET.Test.Sdk`, не внесённый ни в список запуска, ни в список исключений, роняет проверку.

Повторить прогон CI локально:

```powershell
dotnet build AIFramework.Full.sln -c Release
dotnet build Tests/AI.ML.Integration/AI.ML.Integration.csproj -c Release
dotnet test Tests/unit/AIFramework.UnitTests/AIFramework.UnitTests.csproj -c Release --no-build
# ...и так для каждого проекта из списка в build-and-test.yml
```

**GPU-тесты в CI не запускаются.** `Tests/NNW.V2.Gpu.Tests` сверяет вычисления на CPU и CUDA, а на раннерах GitHub нет GPU: каждый тест пропустился бы и создал видимость проверки. Проект собирается вместе с решением, а запускается вручную на машине с видеокартой NVIDIA и CUDA перед изменениями в `AI.NeuralNetworks` и `AI.NeuralNetworks.Gpu`:

```powershell
dotnet test Tests/NNW.V2.Gpu.Tests/NNW.V2.Gpu.Tests.csproj -c Release
```

В итоге должно быть «пропущено 0»: без CUDA тесты не падают, а пропускаются, и такой прогон ничего не проверил.

## Изменение публичного API

Публичный API каждой сборки `src` записан в снимок: [Tests/unit/PublicApi.Tests/Snapshots/](Tests/unit/PublicApi.Tests/Snapshots/), по файлу на сборку (`AI.LLM.txt`, `AI.SignalLab.txt` и т. д.). Тест `PublicApi.Tests` строит API собранных сборок и сравнивает его со снимком; любое расхождение роняет тест с построчной разницей: «-» — есть только в снимке, «+» — только в коде. Тест входит в решения `AIFramework.sln`, `AIFramework.Full.sln`, `AIFramework.Light.sln` и в CI.

Снимок меняется при любом изменении публичной поверхности: новый или удалённый публичный тип или член, другая сигнатура, значение по умолчанию у параметра, базовый тип, атрибут (`[Obsolete]`, `InternalsVisibleTo` и т. п.).

**Изменение публичного API требует решения владельца.** В pull request опишите, что меняется и зачем, и приложите обновлённый снимок — его разница и есть перечень изменений API для ревью.

Обновить снимки (только явно, только локально):

```powershell
$env:AI_PUBLIC_API_UPDATE = '1'
dotnet test Tests/unit/PublicApi.Tests -c Release
Remove-Item Env:AI_PUBLIC_API_UPDATE
```

```bash
AI_PUBLIC_API_UPDATE=1 dotnet test Tests/unit/PublicApi.Tests -c Release
```

Без переменной тест снимки не трогает. В CI (`CI=true`) обновление запрещено: тест падает, даже если переменная выставлена. После обновления просмотрите `git diff Tests/unit/PublicApi.Tests/Snapshots` и закоммитьте снимок вместе с кодом.

**Удаление — через `[Obsolete]`.** Публичный тип или член не удаляют сразу: сначала его помечают `[Obsolete("Используйте <замена>.")]` (при необходимости вместе с `[EditorBrowsable(EditorBrowsableState.Never)]`) и оставляют рабочим хотя бы на одну опубликованную версию (`Version` в [Directory.Build.props](Directory.Build.props)). Удаление после этого — тоже решение владельца и тоже обновление снимка.

**AI.Agents.Contracts: предварительный статус.** Общие контракты агентного слоя ([src/AI.Agents.Contracts](src/AI.Agents.Contracts/README.md)) еще не объявлены стабильными. Версия пакета — с суффиксом предварительной версии, `0.1.0-preview.1` (как и у адаптеров `AI.Agents.Interop`): `build.ps1 -Pack` его собирает, а `build/nuget-push.ps1` выкладывает пакеты с таким суффиксом только с флагом `-IncludePreview`. Предварительность определяется суффиксом, а не нулевым старшим номером: пакет `0.1.0` без суффикса (например, `AI.Script`) обычный и выкладывается как раньше. Обычные пакеты от предварительных не зависят, `AI.LLM` на контракты не ссылается. Публичный API контрактов меняется по решению владельца без мажорной версии и без шага `[Obsolete]`, пока стабильность не объявлена. Снимок `AI.Agents.Contracts.txt` проверяется наравне с остальными, исключений для него нет: любое изменение приходит с обновленным снимком.

Храповик: тест падает, если в `src` появилась сборка без снимка или снимок лежит для сборки, которой больше нет. Новая библиотека приходит со снимком, удалённая — без него. Исключения задаются только списком `SrcAssemblies.Excluded` с причиной в коде; сейчас там одна запись — `AI.NeuralNetworks.Gpu` в лёгкой сборке, которая GPU-библиотеку не собирает (в полной сборке и в CI её снимок проверяется).

Текст строит [PublicApiGenerator](https://github.com/PublicApiGenerator/PublicApiGenerator) (MIT) по метаданным сборки. Его версия закреплена в `PublicApi.Tests.csproj`: после обновления генератора снимки пересоздаются отдельным коммитом без изменений кода.

> **Пути в `.sln`.** Windows нечувствителен к регистру, поэтому опечатка в пути к проекту здесь не проявляется, а на Linux-раннере роняет `dotnet restore`. Записывайте пути ровно в том регистре, в каком каталоги лежат в репозитории (`Tests\unit\...`, а не `tests\unit\...`), и не оставляйте в решении записи об удалённых проектах.

## Качество кода

- Форматирование и базовые соглашения: [.editorconfig](.editorconfig) (корневой файл, `charset = utf-8`).
- В [Directory.Build.props](Directory.Build.props) включены анализаторы (`EnableNETAnalyzers`); унаследованный код оставлен с `Nullable` отключённым — новые файлы можно помечать `#nullable enable` по мере готовности.
- Соблюдайте стиль существующих модулей; избегайте несвязанных с задачей рефакторингов в одном PR.

## Лицензия

Вклад принимается на условиях **Apache 2.0**, в соответствии с корневой лицензией проекта.
