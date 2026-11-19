# AI.Agents.Contracts

Общие контракты агентного слоя: инструмент, движок, порт модели и подтверждение. Сборка содержит только типы и чистые помощники без поведения хоста. Реализации (циклы, клиенты моделей, наборы инструментов) живут в других сборках и ссылаются сюда.

Статус предварительный. Пакет NuGet не выпускается (`IsPackable=false`), публичный API меняется по решению владельца без мажорной версии, пока стабильность не объявлена. Снимок API проверяется наравне с остальными сборками: `Tests/unit/PublicApi.Tests/Snapshots/AI.Agents.Contracts.txt`.

## Пространства имен

| Пространство | Что внутри |
|---|---|
| `AI.Agents.Contracts.Tools` | `ITool`, `IToolSet`, `ToolSpec`, `About`, `ToolAction` и помощник `ToolActions.Effective`, `ToolCall`, `ToolResult`, `ToolStatus`, `ToolUpdate` (`Progress`, `Final`), `ToolContext`, `ToolFile`, `Origin`, `OriginKind` |
| `AI.Agents.Contracts.Engines` | `IEngine`, `EngineInput`, `EngineTask`, `EngineContext`, `EngineEvent` (девять видов), `EngineResult`, `EngineStatus`, `StopCode`, `StepPath`, `IRunBudget`, `IStepRunner`, `Question`, `QuestionKind`, `Answer`, `InputField`, `AgentRef` |
| `AI.Agents.Contracts.Models` | `IModelPort`, `ModelAsk`, `ModelRole`, `ModelCapabilities`, `ModelMessage`, `MessageRole`, `MessagePart` (`Text`, `Image`), `ToolMode`, `LlmAnswer`, `LlmStatus`, `LlmFailure`, `ModelUsage` |
| `AI.Agents.Contracts.Citations` | `Citation`, `CitationKind` |

## Правила

1. **Только BCL.** В проекте нет ни одного `PackageReference`, `ProjectReference` и `FrameworkReference`; `System.Text.Json` входит в общий фреймворк. Платформа `net9.0`, `Nullable` включен. Тест читает csproj и ссылки собранной сборки.
2. **Члены `required init`, никаких позиционных записей.** Обязательное поле отмечено `required`: без него запись не компилируется и не читается из JSON. Новые поля добавляются необязательными, поэтому двоичная совместимость не ломается. Открытых конструкторов с несколькими параметрами нет.
3. **Значений по умолчанию, которые молча подменяют выбор, нет.** Необязательный член допускает `null`, и смысл `null` описан в XML-документации. Исключение составляют признаки `bool`, где `false` означает «нет».
4. **Ноль каждого перечисления равен `Unknown`.** Перечисления пишутся в JSON именем значения: конвертер объявлен на самом перечислении, вызывающему ничего настраивать не нужно. Число и неизвестное имя при чтении дают `JsonException` со списком допустимых имен.
5. **`ToolAction.Unknown` означает `Irreversible`.** Трактовку дает одно место, `ToolActions.Effective`. Гейт спрашивает вид до вызова через `ITool.Classify(ToolCall)`: по умолчанию это `Spec.Action`, а инструмент, у которого вид зависит от аргументов, отвечает точнее. `ToolResult.Raised` нужен только журналу.
6. **Имя инструмента проверяется при создании `ToolSpec`.** Шаблон `^[a-zA-Z0-9_-]{1,64}$`, сравнение с учетом регистра (`ToolSpec.NameComparer`). Схема параметров обязана быть объектом JSON.
7. **Итог движка один.** `IEngine.RunAsync` возвращает только поток событий, последним всегда идет `EngineEvent.Finished` с `EngineResult`. Сбой, исчерпание бюджета и вопрос человеку приходят итогом со статусом, а не исключением. `Incomplete` отличается и от `Failed`, и от `Done`.
8. **Одно понятие хранится в одном месте.** Срок живет только в `IRunBudget`, у `EngineInput` поля срока нет. Код причины остановки (`StopCode`) общий для бюджета и итога движка. Плательщика в контракте движка нет, он приходит в реализацию `IModelPort`.
9. **Каждый тип объявлен один раз.** Имена верхнеуровневых типов контрактов не повторяются в других сборках `src` и `Tools`. Исключения записаны долгом в `OwnershipTests.NameDebt` (`ToolResult` и `ToolCall` из `AI.LLM`, `LlmAnswer` из Атласа). Список только сокращается, а в адаптерах для этих имен используются псевдонимы `using`.
10. **Каждый публичный неабстрактный тип проходит круг через JSON.** Образец лежит в `Tests/unit/AI.Agents.Contracts.Tests/ContractSamples.cs`, и первый образец заполняет все свойства. Новый тип без образца роняет тест. Исключение составляет `EngineInput`: он несет порты хоста, а его данные (`EngineTask`, `EngineContext`, состояние) проходят круг отдельно.

## Как добавить член или тип

- Новый член: `init` с `null` по умолчанию и описанием смысла `null`, либо `required`, если без него контракт теряет смысл. Обновить первый образец типа в `ContractSamples.cs`.
- Новый тип: запись с членами `required init`, XML-документация на русском, образец в `ContractSamples.All`. Для наследника полиморфной записи нужна строка `[JsonDerivedType]` на базовом типе.
- Новое перечисление: `Unknown = 0` и атрибут `[JsonConverter(typeof(StrictEnumConverter<T>))]`.
- После изменения обновить снимок API только этой сборки:

```powershell
$env:AI_PUBLIC_API_UPDATE = '1'
dotnet test Tests/unit/PublicApi.Tests -c Release --filter "DisplayName~AI.Agents.Contracts"
Remove-Item Env:AI_PUBLIC_API_UPDATE
```

## Что дальше

Контракты пока никто не использует: существующие сборки на них не ссылаются, и их поведение не изменилось. Следующие шаги идут отдельными изменениями:

1. `[AgentTool]` с видом действия и `ToolRegistry.AsToolSet()` в `AI.LLM`.
2. Адаптеры в `AI.LLM`: `IReActTool` и `ITool`, `IReActToolSource` и `IToolSet` в обе стороны; `ILLMClient` и `IModelPort` в обе стороны. Если адаптеру понадобится потоковый ответ модели, `IModelPort` получит метод с реализацией по умолчанию, это не ломает реализующих.
3. Порты в `ReActEngine`: `WithModel(IModelPort)`, `WithTools(IToolSet)`, `WithBudget(IRunBudget)`. Умолчания совпадают с нынешним `ReActConfig`.
4. Продолжение с места: состояние на выходе и входе, адаптер `IEngine` с ключом `react`, обработка `AwaitsUser`, `NeedsInput` и `Denied`.
5. Готовые реализации для внешнего пользователя: набор инструментов из списка, бюджет по числу шагов, запуск без дочерних агентов. Готовый набор не исполняет `Irreversible` без подтверждения.

Запись агента (`AgentDefinition`) появится отдельным шагом. Интерфейса агента не будет: агент описывается данными, а не классом.
