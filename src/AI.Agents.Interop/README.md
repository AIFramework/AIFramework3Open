# AI.Agents.Interop

Адаптеры между старыми типами агентного слоя `AI.LLM` и контрактами [AI.Agents.Contracts](../AI.Agents.Contracts/README.md). Сборка добавляет только новые типы: поведение `AI.LLM` не меняется, его код и снимок API остаются прежними.

## Почему отдельная сборка

`AI.LLM` публикуется в NuGet, а контракты нет: у них предварительный статус и `IsPackable=false`. Пакет не может зависеть от неопубликованной сборки, поэтому адаптеры не входят ни в `AI.LLM`, ни в контракты. Эта сборка ссылается на обе и тоже не упаковывается (`IsPackable=false`). Когда контракты станут стабильными и выйдут пакетом, решение можно пересмотреть.

## Пространства имен

| Пространство | Что внутри |
|---|---|
| `AI.Agents.Interop.ReAct` | инструмент ReAct (`IReActTool`, `IReActToolSource`, `ReActToolOutcome`, `ReActCitation`) ↔ `ITool`, `IToolSet`, `ToolResult`, `Citation` |
| `AI.Agents.Interop.Models` | будет на шаге Ф4: клиент модели `AI.LLM` ↔ `IModelPort` |

## Точки входа

| Вызов | Что получается |
|---|---|
| `reactTool.AsTool(action: null, log: null)` | `ITool` над старым инструментом |
| `tool.AsReActTool(log: null)` | `IReActTool` над инструментом контракта |
| `source.AsToolSet(run: null, actions: null, log: null)` | `IToolSet` над источником старых инструментов |
| `set.AsToolSource(log: null)` | `IReActToolSource` над набором контракта |

Значения без адаптера переводит `ReActTranslation`: `outcome.ToResult()`, `result.ToOutcome()`, `citation.ToCitation()`, `citation.ToReActCitation()`, `image.ToFile()`, `file.ToImage()`. Других мест, где одна форма собирается из другой, нет. Тест `OneTranslationPlaceTests` ищет такие места в `src` и `Tools`.

Журнал — Serilog, как в `AI.LLM` (`ToolRegistry`). `log = null` означает `Log.Logger` на момент записи.

```csharp
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;

ITool search = webSearch.AsTool(action: ToolAction.Read);   // вид известен вызывающему
IToolSet tools = registrySource.AsToolSet();                // виды неизвестны: Unknown и предупреждения
IReActToolSource legacy = tools.AsToolSource();             // обратно, для ReActEngine
```

## Вид действия

Старый инструмент вида действия не знает. Без явного параметра `AsTool` дает `ToolAction.Unknown`, а `Unknown` действует как `Irreversible` (`ToolActions.Effective`). О неизвестном виде пишется одно предупреждение на инструмент: у `AsTool` на экземпляр, у `AsToolSet` на имя, потому что источник вправе каждый раз отдавать новые экземпляры. `Read` сам не появляется никогда, только из явного параметра.

В обратную сторону гейта в старом движке нет. Адаптер перед каждым вызовом спрашивает `Classify(ToolCall)` и пишет вид в журнал. На поведение движка это не влияет.

## Как переводится итог

Полный список с причинами лежит в `ReActTranslationTable`: `Translated` (что и куда) и `NotTranslated` (почему нет). Тест-храповик перебирает рефлексией свойства `ReActToolOutcome`, `ToolResult`, `ReActCitation`, `Citation`, `ToolSpec`, `IReActTool`, `ToolFile`, `AgentImage` и кадров потока. Новое свойство без записи в таблице роняет тест.

Основные решения:

- **Финал.** `EndsTurn` ↔ `EndsRun`. В контракте один текст (`Text`), а у старого итога два: `Observation` для следа и `TerminalAnswer`, откуда движок брал ответ запуска. У финала в `Text` идет `TerminalAnswer`. Если `Observation` от него отличается, он не переносится, и в журнал пишется предупреждение. Финал без `TerminalAnswer` дает `Text = Observation` и тоже предупреждение: отличить «ответа нет» контракт не позволяет. Обратно `EndsRun` дает `EndsTurn = true` и `TerminalAnswer = Observation = Text`.
- **Статусы, которых старый движок не знает.** `Denied`, `NeedsInput`, `AwaitsUser` и `Unknown` дают `Ok = false` и код из `ReActInteropCodes`: `denied`, `needs_input`, `awaits_user`, `unknown_status`. Причина идет в `Observation`. Пустой текст заменяется причиной по умолчанию, свой код инструмента и текст вопроса дописываются туда же. Структура `Question` в старой форме не выражается.
- **Цитата без адреса.** `Citation` без `Url` в `ReActCitation` не переводится: адрес не выдумывается. Цитата не попадает в `Citations`, а число пропущенных пишется предупреждением в журнал. `ReActTranslation.ToReActCitation()` в этом случае возвращает `null`, и вызывающий видит пропуск сам. Так же поступают с файлами, которые не являются картинкой для модели с байтами.
- **`Payload` ↔ `Data`.** Перевод идет через System.Text.Json. Если значение не сериализуется, бросается `InvalidOperationException` с именем инструмента и типом значения, тихого `null` нет. Обратно приходит `JsonElement`, тип CLR не сохраняется.
- **Происхождение.** Старый инструмент происхождения не знает, поэтому вперед всегда идет `OriginKind.Unknown`. Текст «инструмент не найден» составляет сам набор, и у него `Trusted`.

## Контекст вызова

`ToolContext` несет идентификатор запуска и путь шага. Запроса и истории в нем нет, поэтому `ReActRunContext` собирается так:

| Поле `ReActRunContext` | Откуда |
|---|---|
| `Query`, `History` | из контекста `run`, переданного в `AsToolSet`; без него пустые (`AsTool` контекста не принимает) |
| `StepNumber` | `ToolContext.Path.Tier`: оба считают шаги с 1 |
| `Tag` | сам `ToolContext`: инструмент читает его через `invocation.Run.TagAs<ToolContext>()` |

Метка базового контекста должна быть пустой, иначе `AsToolSet` бросает `ArgumentException`: подменять чужую метку молча нельзя.

Обратно (`AsReActTool`) контекст вызова берется из `Run.Tag`, если там `ToolContext`. Иначе `RunId` пустой, потому что старый движок идентификатор запуска инструменту не передает, а `Path.Tier = StepNumber`.

Аргументы старого движка — строка. Пустая строка дает `{}`, JSON идет как есть. Простая строка кладется в единственное свойство схемы, а если свойств не одно, передается строкой JSON. Вперед инструменту без своей схемы аргумент `{"input": "…"}` разворачивается в строку так же, как это делает `NativeToolCallPolicy`.

## Переиспользование кода AI.LLM

Простая схема `{"input": string}` и разворачивание аргумента — закрытые методы `NativeToolCallPolicy` (`BuildPlainSchema`, `Unwrap`). Чтобы не копировать их, `NativePolicyBridge` вызывает их через отражение. `InternalsVisibleTo` здесь не помог бы: методы `private`, а любая правка `AI.LLM` изменила бы его снимок API. Если метод переименуют, адаптер при первом обращении упадет с понятной ошибкой, а тест `NativePolicyBridgeTests` поймает это раньше.

## Имена

Контракты и `AI.LLM` оба объявляют `ToolResult` и `ToolCall` (долг в `OwnershipTests.NameDebt` контрактов). Поэтому здесь пространство `AI.LLM.Agents.Multimodal` целиком не подключается: `AgentImage` берется псевдонимом `using`.
