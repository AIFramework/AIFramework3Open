# Шаблон агентов, очередь 0: запуск, идемпотентность, гейт эффектов, движки

Дата: 20.11.2026. Изменения только добавляют типы и члены. `src/AI.LLM` не изменен ни одним файлом, `Agent` и `PlanningAgent` не тронуты, снимок `AI.LLM.txt` побитно прежний. Ничего не закоммичено.

## Что добавлено

- **Запуск как сущность** в `AI.Agents.Contracts`, новое пространство `AI.Agents.Contracts.Runs`: `RunStatus`, `RunState`, `EffectRecord`, порт хранилища `IRunStore` и синхронный канал к человеку `IHumanChannel`. Только данные и интерфейсы, реализации дает продукт.
- **Ось «повтор безопасен»**: перечисление `ToolIdempotency`, необязательное поле `ToolSpec.Idempotency` и помощник `ToolIdempotencies.Effective` (`null` и `Unknown` → `NotIdempotent`).
- **Атрибут эффекта** `[ToolEffect(action, idempotency)]` в `AI.Agents.Interop` и набор из `[AgentTool]` с ним: `AttributedTools.AsToolSet`.
- **Ключ эффекта** `EffectKeys.Create` и **гейт эффектов** `EffectGate` с журналом.
- **Канал к человеку над набором**: `WithHumanChannel`, `AnsweredToolContext`.
- **Движки `IEngine`**: `ReActEngineAdapter` (ключ `react`) и `PlanningEngineAdapter` (ключ `plan`).
- **Предварительные пакеты**: `AI.Agents.Contracts` и `AI.Agents.Interop` версии `0.1.0-preview.1`, флаг `-IncludePreview` в `build/nuget-push.ps1`; зависимость Interop на `AI.LLM` — точная версия `[4.0.0]`.
- README обеих сборок, абзац в `CONTRIBUTING.md`, тесты и храповики.

## Типы

| Тип | Сборка, пространство | Назначение |
|---|---|---|
| `RunStatus` | Contracts, `Runs` | состояние запуска: `Unknown`, `Queued`, `Running`, `AwaitsUser`, `Done`, `Incomplete`, `Failed`, `Cancelled`, `Lost` |
| `RunState` | Contracts, `Runs` | запуск: `Id`, `Agent`, `Task`, `Status`, `Updated` (обязательные), `EngineState`, `Question`, `Effects` |
| `EffectRecord` | Contracts, `Runs` | запись журнала эффектов: ключ, инструмент, `ToolResult` |
| `IRunStore` | Contracts, `Runs` | порт хранилища: `SaveAsync`, `GetAsync`, `ListAsync(RunStatus)` |
| `IHumanChannel` | Contracts, `Runs` | `AskAsync(Question, CancellationToken) → Answer`, временная реализация `AwaitsUser` |
| `ToolIdempotency` | Contracts, `Tools` | `Unknown = 0`, `Idempotent`, `WithKey`, `NotIdempotent` |
| `ToolIdempotencies` | Contracts, `Tools` | `Effective`: одно место трактовки незаданного значения |
| `ToolSpec.Idempotency` | Contracts, `Tools` | новое необязательное поле (`ToolIdempotency?`) |
| `ToolEffectAttribute` | Interop, `Effects` | вид действия и «повтор безопасен» рядом с `[AgentTool]` |
| `AttributedTools` | Interop, `Effects` | `IToolSet` из объектов с `[AgentTool]`, оси — из `[ToolEffect]` |
| `EffectKeys` | Interop, `Effects` | единственное место создания ключа эффекта |
| `EffectGate`, `EffectGateCodes` | Interop, `Effects` | гейт эффектов над любым `IToolSet`, журнал запуска, коды |
| `HumanChannels`, `AnsweredToolContext`, `HumanChannelCodes` | Interop, `Humans` | вопрос инструмента → `IHumanChannel` → один повторный вызов с ответом |
| `ReActEngineAdapter` | Interop, `Engines` | `IEngine` над `ReActEngine`, ключ `react` |
| `PlanningEngineAdapter` | Interop, `Engines` | `IEngine` над `PlanningAgent`, ключ `plan` |
| `ReActEngineTranslationTable`, `EngineInteropCodes` | Interop, `Engines` | перевод причин остановки и событий, коды отказов |

Запуск лежит в новом пространстве `Runs`, а не в `Engines`: запуск принадлежит хосту, переживает несколько прогонов движка (пауза, продолжение) и может пережить смену движка, а `Engines` описывает один прогон.

## Атрибут эффекта и почему он в Interop

По решению владельца стабильный пакет `AI.LLM` не зависит от предварительных контрактов, поэтому `[AgentTool]` не меняется, а `AI.LLM.csproj` не ссылается на `AI.Agents.*` (храповик `RunContractTests.AiLlm_DoesNotReferenceAgentPackages`, тест читает csproj). Вид действия и «повтор безопасен» объявляет отдельный атрибут `[ToolEffect]` в `AI.Agents.Interop` на том же методе. Оба значения — обязательные параметры конструктора.

`AttributedTools.AsToolSet` строит реестр `ToolRegistry` из тех же объектов (исполнение и схема прежние) и читает `[ToolEffect]` с метода, который вернул закрытый `ToolRegistry.ScanMethods` (через уже открытый `InternalsVisibleTo`): правило имени инструмента одно. Без атрибута — `Unknown` по обеим осям и одно предупреждение на инструмент; `Read` и `Idempotent` сами не появляются. Атрибут без `[AgentTool]` — `ArgumentException` с именем типа и метода.

Чтение пользовательского атрибута — отражение. Храповик `NoReflectionTests` разрешает его ровно в одном файле (`Effects/AttributedTools.cs`) явным исключением с причиной и снимает там только маркеры чтения метаданных (`System.Reflection`, `BindingFlags`). Вызов членов `AI.LLM` через отражение запрещен по-прежнему; отдельный тест проверяет, что исключение одно и указывает на существующий файл.

## Ключ эффекта

`EffectKeys.Create(runId, toolName, arguments)` — единственное место создания. Ключ — SHA-256 от строки `длина runId : runId : длина имени : имя : канонический JSON аргументов`, 64 строчных шестнадцатеричных знака. Хеш нужен, чтобы аргументы не уходили внешнему сервису в `Idempotency-Key` и длина была постоянной.

Канонизация строгая и своя (решение владельца по остановке 2): ключи объектов отсортированы порядковым сравнением на любой глубине, пробелов вне строк нет, строки и числа не меняются (регистр и пробелы внутри строк сохраняются, текст числа тот же), порядок массива сохраняется, имя инструмента сравнивается с учетом регистра. `ReActActionKey` — эвристика повтора действия модели, а ключ эффекта — точная идентичность эффекта; это разные понятия, поэтому ключ эффекта на `ReActActionKey` больше не опирается. `ReActActionKey` не тронут.

Ключ получает инструмент через существующее `ToolCall.EffectKey`; новых полей у `ToolContext` нет. Заполняет его гейт перед каждым вызовом, ключ хоста заменяется с предупреждением. Описание поля в контракте обновлено: «назначает фреймворк».

Храповики (`EngineRatchetTests`):

- `SHA256` в `src/AI.Agents.*` есть только в `EffectKeys.cs`;
- `ToolCall.EffectKey` в `src/AI.Agents.*` назначается только значением `EffectKeys.Create(...)` (в гейте — через переменную с этим значением);
- `ReActActionKey.Create(` вызывает только движок ReAct (`ReActEngine.Loop.cs`, `ReActEngine.Tools.cs`);
- канонизаций аргументов JSON (обход свойств `EnumerateObject` и сортировка имен) в `src` ровно две: `ReActActionKey.cs` и `EffectKeys.cs`.

Тесты `EffectKeyTests`: другой порядок полей (в том числе вложенных) и пробелы между токенами — один ключ; другой регистр или пробелы внутри строкового значения, другой регистр имени, другой порядок массива, `1` и `1.0` — разные ключи; экранированная и обычная запись знака — один ключ. Тест, фиксировавший схлопывание регистра и пробелов, заменен.

## Правила гейта

`EffectGate` оборачивает любой `IToolSet`. Вид — `ToolActions.Effective(ToolSpec.Action)`, повтор — `ToolIdempotencies.Effective(ToolSpec.Idempotency)`.

| Случай | Решение |
|---|---|
| `Read`, `Compute`, неизвестное имя | к набору без журнала (ключ назначается) |
| `Reversible`, `Irreversible`, `Unknown` | запись до вызова итогом `Unknown`, после — настоящий итог |
| ключ исполнен с `Ok` | инструмент не вызывается, возвращается сохраненный итог |
| ключ начинался и не кончился успехом (сбой, обрыв, исключение, поток без итога, вызов идет) | `Idempotent`, `WithKey` — повтор с тем же ключом; `NotIdempotent`, `Unknown`, `null` — `Failed` с кодом `repeat_unsafe` без вызова |
| итог `Denied`, `NeedsInput`, `AwaitsUser` | действие не исполнялось: запись снимается, повтор возможен |

Коды исхода «неизвестно»: `no_result` (поток без итога), `exception` (исключение пробрасывается дальше), `interrupted` (отмена или остановка чтения). Журнал `EffectGate.Journal` хост кладет в `RunState.Effects`. Повтор с другим ключом через гейт невозможен: ключ детерминирован, поэтому `WithKey` повторяется только с тем же ключом.

Вместо `Failed` можно было бы задавать вопрос человеку. Выбран `Failed` с машинной причиной: вопрос через синхронный канал потребовал бы обхода гейта по ответу, а это второе правило повтора.

## Канал к человеку

`IHumanChannel` в контрактах — временная синхронная реализация `AwaitsUser`; его заменит продолжение запуска с места (итог `AwaitsUser`, `RunState` через `IRunStore`, новый прогон с `EngineTask.Answer`). В Interop набор `WithHumanChannel` превращает итог инструмента `AwaitsUser` или `NeedsInput` с вопросом в вопрос через канал и один повторный вызов с ответом в наследнике контекста `AnsweredToolContext`. Это выражается без смены поведения старого движка: все происходит внутри вызова набора, движок видит только итог. Ответ на чужой вопрос — `Failed` (`answer_mismatch`), согласие без «да» — `Denied` (`declined`). Если контекст вызова уже наследник с данными хоста, заменить его без потери нельзя: вопрос отдается итогом с предупреждением.

Правило решения 2 записано в README контрактов (правило 12): опасность, видная только по состоянию мира, объявляется инструментом через `AwaitsUser` и `Question` с видом действия, целью и суммой; `ITool.Classify` не меняется. Тест `HumanChannelTests` проверяет это на «кнопке оплаты».

## Движки

`react`: `ReActEngine` собирается `ReActAgentBuilder` на каждый прогон; модель — `IModelPort` под `ILLMClient` (Ф4, роль `decide`), инструменты — гейт и `IToolSet → IReActToolSource` (Ф3). `ReActEvent → EngineEvent`, `ReActResult → EngineResult` по `ReActEngineTranslationTable`: `IterationLimit`, `TimeLimit`, `NoProgress`, `NoTools` → `Incomplete`; `FinalAnswer`, `TerminalTool` → `Done`; `PolicyFailure`, `EngineFailure` → `Failed`; `Cancelled` → `Cancelled`.

Эталон `EngineAdapterTests.React_Reference_SameAnswerAndSameModelMessages`: синтетический сценарий (модель зовет инструмент, затем отвечает) через старый вход и через адаптер дает тот же ответ, те же сообщения модели в обоих обращениях (роль, текст, вызовы, ответ на вызов) и тот же аргумент инструменту.

`plan`: `PlanningAgent` собирается публичным `PlanningAgentBuilder` без правок. Шаг плана исполняет цикл ReAct тем же образцом, что `react`, через публичный `WithStepExecutor` (см. «Пункты, где пришлось остановиться», п. 1). Гейт один на прогон: повтор шага и перепланирование не исполняют совершенный эффект второй раз (тест `Plan_RunsStepsThroughGate_AndFinishesDone`).

Настройки шага сверены со старым исполнителем (решение владельца по остановке 1). Совпадают: предел 30 итераций на попытку шага (если `maxIterations` не задан), системный промпт (`EngineContext.Instructions`, у старого — промпт `PlanningAgentBuilder.WithSystemPrompt`), повторы, приемка и перепланирование (код `PlanningAgent`). Расхождения, которые без правок `AI.LLM` и контрактов не выражаются:

- температура 0,15: в `ModelAsk` температуры нет, шаг идет с температурой из начальных настроек порта;
- предел токенов ответа: у старого исполнителя не задан, цикл ReAct передает свой в `ModelAsk.MaxTokens`;
- обертка системного промпта: старый `Agent` шлет промпт как есть, ReAct оборачивает своим шаблоном;
- запрос шага: с `WithStepExecutor` `PlanningAgent` строит свой запрос (общая задача, итоги принятых шагов, шаг, критерий готовности, отклоненный ответ); закрытый `BuildStepQuery` старого исполнителя недоступен;
- память шагов: вместо `StepMemory` — итоги принятых шагов в тексте запроса;
- страж и наблюдатель (`WithGuard`, `WithObserver`): в контракте их нет.

Тесты: `Plan_StepIterations_MatchOldExecutor` (30 обращений к модели на попытку шага; `maxIterations = 3` дает 3) и `Plan_StepIterations_AreThoseOfOldBuilder` (старый `PlanningAgentBuilder` все еще задает `WithMaxIterations(30)` и `WithTemperature(0.15)`, адаптер — 30, README записывает расхождение по температуре).

## Честные границы до Ф5 и Ф6

- `EngineInput.State` или `EngineTask.Answer` на входе — `Failed` с кодом `continuation_not_supported`, модель не вызывается.
- Срок `IRunBudget.Deadline` задается до старта: предел времени движка (`react`) или отмена по сроку (`plan`); истекший до старта — `Incomplete`, `time_limit`.
- Шаги: `TryStep` в начале каждого шага цикла, отказ — `Incomplete` с кодом бюджета до вызова модели.
- Токены и деньги: каждый расход сообщается `Spend` и проверяется `TryStep` следующего шага. Внутри шага, в синтезе после цикла и у планировщика между шагами проверки нет, перерасход возможен на один шаг. Это записано в README Interop; проверка на каждом вызове модели — порт `WithBudget` у `ReActEngine` (Ф5).
- Защита от повтора действует внутри одного запуска (один экземпляр гейта). Между запусками — когда движок начнет принимать `State` (Ф6). Записано в README контрактов и Interop.
- Неизвестный параметр движка — `Failed` с кодом `invalid_options`.

## Что сознательно не сделано

- Подвид необратимого, Egress, схема результата, секретные параметры, `Preview`, `Scope`, валюта: решений нет, членов нет. Места описаны в README контрактов, раздел «Чего в форме пока нет».
- Ссылка `AI.LLM` → контракты и поля в `[AgentTool]`: по решению 5 — к 1.0 контрактов или к Ф5.
- Реализации `IRunStore` и `IHumanChannel`: их дает продукт.
- Засев гейта журналом прошлого прогона: до Ф6 граница — один запуск.
- Выкладка в NuGet: пакеты собираются только в локальную папку.

## Решения владельца по остановкам (20.11)

| Пункт | Решение | Что сделано |
|---|---|---|
| 1. `plan` | где выражается — те же настройки шага, что у старого `PlanningAgent`; остальное — списком расхождений | предел 30 итераций на попытку шага; промпт тот же; расхождения — в README Interop и в разделе «Движки» выше; два теста |
| 2. Ключ эффекта | строгая канонизация, отдельная от `ReActActionKey` | своя каноническая форма в `EffectKeys`; храповики переписаны; тест на схлопывание заменен тестами на различие |
| 3. Правило выкладки | предварительность — суффикс версии, а не ноль | `AI.Agents.*` — `0.1.0-preview.1`; `nuget-push.ps1` пропускает без флага только версии с суффиксом; `AI.Script 0.1.0` — обычный пакет |
| 4. Зависимость Interop от `AI.LLM` | точная версия, пока используются внутренние члены | цель `PinAiLlmExactVersion` в `AI.Agents.Interop.csproj`; в nuspec `AI.LLM [4.0.0]`; храповик на цель; в README: «выпускаются вместе» |

## Пункты, где пришлось остановиться

1. **`PlanningAgent` и гейт.** Исполнитель шагов `PlanningAgent` (`Agent`) вызывает инструменты через `ToolRegistry` напрямую. Провести такой вызов через `IToolSet` без потерь при неизменном `AI.LLM` нельзя: `ToolRegistry.Register` раскладывает аргументы по именам параметров делегата и не отдает JSON целиком. Варианты:
   - (а) **сделано**: шаг плана исполняет цикл ReAct через публичный `WithStepExecutor`. Планирование, повторы, приемка и перепланирование — прежние. По решению владельца настройки шага повторяют старый исполнитель там, где выражаются (предел 30 итераций, промпт); температура 0,15 и остальное — списком расхождений в разделе «Движки»;
   - (б) реестр-обертка над `IToolSet` для старого `Agent`: требует в `AI.LLM` регистрации инструмента с обработчиком сырого JSON (изменение `AI.LLM`, к Ф5);
   - (в) `plan` без гейта: эффекты при повторе шага исполнялись бы дважды.
2. **Свойства канонизации в ключе эффекта.** `ReActActionKey` не различает регистр имени и строковых значений и схлопывает пробелы внутри строк. Для ключа эффекта это значит: два вызова, которые отличаются только регистром или пробелами (идентификатор с учетом регистра, текст письма), получают один ключ, и второй не исполняется, а возвращает итог первого. Варианты были: (а) оставить; (б) изменить `ReActActionKey` (меняет поведение `AI.LLM`, сейчас нельзя); (в) своя канонизация в Interop. **Решено: (в)**, строгая каноническая форма; см. «Ключ эффекта».
3. **`AI.Script` тоже `0.x`, а от него зависят стабильные модули.** Кроме двух новых пакетов версию `0.1.0` имеет `AI.Script`, а десять модулей `AI.Script.*` — `4.0.0` и зависят от него. Без флага `nuget-push.ps1` пропускает `AI.Script` и, не ослабляя проверку зависимостей, падает на первом модуле: «Пакет AI.Script.Charts зависит от предварительного AI.Script 0.1.0, который без -IncludePreview не выкладывается». Значит, шаг `nuget-push.ps1 -DryRun` в `publish-nuget.yml` в нынешнем виде упадет. С флагом `-IncludePreview` проверка проходит (51 пакет). Рабочий процесс не менялся. Варианты:
   - (а) ограничить правило «0.x только с флагом» пакетами `AI.Agents.*`, а `AI.Script` выкладывать как раньше;
   - (б) пропускать без флага и стабильные пакеты, зависящие от пропущенного предварительного (`AI.Script.*`), с сообщением: проверка не ослабляется, но модули тоже не уходят;
   - (в) передавать `-IncludePreview` в рабочем процессе: тогда вместе со всеми уйдут и `AI.Agents.*`, а их выкладка по решению 3 — позже;
   - (г) поднять `AI.Script` до `4.x` и считать его стабильным.

   **Решено иначе:** предварительность помечается суффиксом версии. `AI.Agents.*` — `0.1.0-preview.1`, `nuget-push.ps1` без флага пропускает только версии с суффиксом, `AI.Script 0.1.0` выкладывается как раньше; проверка зависимостей `AI.*` не ослаблена.
4. **Закрытые члены `AI.LLM` в пакете Interop.** `AI.Agents.Interop` вызывает `internal` члены `AI.LLM` (`NativeToolCallPolicy`, `ToolRegistry.ScanMethods`, `ReActActionKey`), а зависимость в nuspec — `AI.LLM >= 4.0.0`. Новая версия `AI.LLM` с переименованным закрытым членом сломает адаптер во время выполнения. Варианты: (а) точная версия `[4.0.0]` в зависимости; (б) выпускать Interop вместе с каждой версией `AI.LLM`; (в) открыть нужные члены публично к Ф5. **Решено: (а) и (б)** до (в): точная версия, Interop и `AI.LLM` выпускаются вместе. `ReActActionKey` из списка закрытых членов, на которые опирается Interop, ушел вместе с ключом эффекта.

## Результаты

**Сборка.** `dotnet build AIFramework.Full.sln -c Release --no-incremental`: 0 ошибок, 174 предупреждения (как в Ф3.1); в `AI.Agents.*` и их тестах — ни одного.

**Тесты.** Полный прогон по списку проектов из `.github/workflows/build-and-test.yml` (с отдельной сборкой `AI.ML.Integration` и проверкой неучтенных проектов): 0 упавших.

| Проект | Пройдено |
|---|---|
| AI.ML.Integration | 2 |
| NNW.V2.Tests | 114 |
| NNW.V2.Onnx.Tests | 4 |
| AI.Agents.Contracts.Tests | 151 (было 134) |
| AI.Agents.Interop.Tests | 177 (было 120) |
| AI.Economics.UnitTests | 327 |
| AI.LLM.UnitTests | 189 |
| AI.Script.UnitTests | 1355 |
| AI.Solvers.Chem.UnitTests | 444 |
| AIFramework.UnitTests | 1458 |
| Atlas.UnitTests | 97 |
| PublicApi.Tests | 53 |

Новые тесты: `RunContractTests` (ось повтора, круг JSON и обязательные поля `RunState`, `IRunStore`, `IHumanChannel`, храповик слоев `AI.LLM`), `EffectKeyTests`, `EffectGateTests`, `AttributedToolsTests`, `HumanChannelTests`, `EngineAdapterTests` (эталон, перевод итога, отказы, бюджет, адаптер плана), `EngineRatchetTests`. Храповикам даны записи: образцы `RunState` и `EffectRecord` и поле `Idempotency` в `ContractSamples`; `ToolSpec.Idempotency` в таблицах `ReActTranslationTable` и `ModelTranslationTable`; исключение для `AttributedTools.cs` в `NoReflectionTests`.

**Документация.** `dotnet run --project Tools/DocsLint -- --strict`: ошибок 0, новых предупреждений 0.

**Снимки API.** Изменились только `AI.Agents.Contracts.txt` (+59 строк) и `AI.Agents.Interop.txt` (+80 строк), удаленных и измененных строк нет. Остальные 50 снимков, включая `AI.LLM.txt`, побитно прежние (сверка SHA-256 до и после). Имена автоматов состояний (`<RunAsync>d__N`) в снимок не попадают: итераторы вынесены в закрытые методы.

**Пакеты.** `.\build.ps1 -Pack` собирает 51 пакет, среди них `AI.Agents.Contracts.0.1.0-preview.1.nupkg` и `AI.Agents.Interop.0.1.0-preview.1.nupkg`. Зависимости Interop в nuspec: `AI.Agents.Contracts 0.1.0-preview.1` и `AI.LLM [4.0.0]` (точная версия). Nuspec `AI.LLM 4.0.0` не содержит зависимостей на `AI.Agents.*`.

**Выкладка, пробный прогон.** `nuget-push.ps1 -DryRun` без флага проходит: 49 обычных пакетов, в том числе `AI.Script 0.1.0` и десять модулей `AI.Script.*`; два предварительных пропущены с сообщением «Предварительные пакеты пропущены (выкладка только с флагом -IncludePreview): AI.Agents.Contracts 0.1.0-preview.1, AI.Agents.Interop 0.1.0-preview.1». С флагом `-IncludePreview` проходят все 51, `AI.Agents.Interop` идет после `AI.LLM` и `AI.Agents.Contracts`.

**Правки по решениям владельца (20.11) проверены** тем же порядком: полная пересборка `--no-incremental` (0 ошибок, 174 предупреждения, в `AI.Agents.*` — ни одного), прогон CI (12 проектов, 0 упавших), DocsLint `--strict` (0 ошибок, 0 новых предупреждений), снимки (изменены только `AI.Agents.Contracts.txt` и `AI.Agents.Interop.txt` относительно последнего коммита; публичный API от правок по решениям не изменился, `AI.LLM.txt` побитно прежний).

**`src/AI.LLM`.** Ни одного измененного файла (`git status src/AI.LLM` пуст).
