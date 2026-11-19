# AI.Agents.Interop

Адаптеры между старыми типами агентного слоя `AI.LLM` и контрактами [AI.Agents.Contracts](../AI.Agents.Contracts/README.md). Сборка добавляет только новые типы: поведение `AI.LLM` не меняется, его код и снимок API остаются прежними.

## Почему отдельная сборка

`AI.LLM` публикуется в NuGet, а контракты нет: у них предварительный статус и `IsPackable=false`. Пакет не может зависеть от неопубликованной сборки, поэтому адаптеры не входят ни в `AI.LLM`, ни в контракты. Эта сборка ссылается на обе и тоже не упаковывается (`IsPackable=false`). Когда контракты станут стабильными и выйдут пакетом, решение можно пересмотреть.

## Пространства имен

| Пространство | Что внутри |
|---|---|
| `AI.Agents.Interop.ReAct` | инструмент ReAct (`IReActTool`, `IReActToolSource`, `ReActToolOutcome`, `ReActCitation`) ↔ `ITool`, `IToolSet`, `ToolResult`, `Citation` |
| `AI.Agents.Interop.Models` | клиент модели `ILLMClient` (`LLMMessage`, `GenerateSettings`, `ChatCompletionsResponse`, `LlmRequestException`) ↔ `IModelPort`, `ModelAsk`, `LlmAnswer` |

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

## Модель

### Точки входа

| Вызов | Что получается |
|---|---|
| `ModelPort.Fixed(client, capabilities, settings: null, log: null)` | `IModelPort` над `ILLMClient`: одна модель на все роли |
| `client.AsModelPort(capabilities, settings: null, log: null)` | то же расширением |
| `port.AsLlmClient(role, log: null)` | `ILLMClient` над портом: старые пути (`Agent`, `PlanGenerator`, `ScriptWriter`, модуль `llm`) ходят через порт без изменений в себе |

Значения без адаптера переводит `ModelTranslation`: `ToLlmMessages`, `ask.ToSettings(...)`, `response.ToAnswer(lost)`, `ModelTranslation.ToAnswer(exception)`, `ToAsk(messages, settings, role, lost)`, `answer.ToResponse()`, `answer.ToException()`, `usage.ToUsage()`, `usage.ToLlmUsage()`. Поле, которое теряется без исключения, метод кладет ключом из таблицы в список `lost`, и адаптеры пишут о нем предупреждение.

```csharp
using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;

IModelPort port = llm.AsModelPort(new ModelCapabilities
{
    ToolCalling = true, Images = true, Audio = false, StructuredOutput = true, Streaming = true,
});
ILLMClient legacy = port.AsLlmClient(ModelRole.Decide);   // обратно, для старых путей
```

Клиент не сообщает, что умеет модель, поэтому `ModelCapabilities` задает вызывающий. При `Streaming = true` поток идет через перегрузку `SendFullAsync` с `onChunk`, а при `false` порт отдает один итоговый кадр, как реализация по умолчанию. Начальные настройки (`GenerateSettings`) несут то, чего нет в контракте: температуру, рассуждение, `IncludeUsage` для стоимости у OpenRouter. Инструменты, режим инструментов, схему ответа и предел токенов задает запрос.

### Итог и сбой

| Старая форма | Итог порта |
|---|---|
| ответ с текстом или вызовами | `Ok` |
| `finish_reason = length`, `MAX_TOKENS` | `Truncated` |
| ни текста, ни вызовов; `EmptyModelAnswerException`, в том числе внутри `LlmRequestException` | `Empty` (рассуждение сохраняется) |
| `content_filter`, отказ модели (`refusal`) без ответа | `Failed`, `ProviderRefusal` |
| переполнение окна (`IsContextOverflow`) | `Failed`, `ContextOverflow` |
| нет ответа сервера: сеть, обрыв потока, тайм-аут; 408 | `Failed`, `Network` |
| 429 | `Failed`, `RateLimit` |
| 401, 402, 403 | `Failed`, `Access` |
| 400, 404, 409, 413, 422, 451 | `Failed`, `ProviderRefusal` |
| прочие коды и исключения | `Failed`, `Other` |

Отмена токеном вызывающего остается исключением, как требует контракт. Текст сбоя для человека и `RetryAfter` в `LlmAnswer` не помещаются, поэтому адаптер пишет их в журнал. Расход берется из `Usage`: нули без стоимости означают, что провайдер ничего не сообщил, и дают `null`. Последний кадр потока — итог с расходом.

Обратно итог без ответа становится тем исключением, по которому старые вызывающие его различают: `Empty` — `LlmRequestException.NoResponse(EmptyModelAnswerException)` (`IsEmptyAnswer`, как у `ChatLLMApi`), `Failed` — `LlmRequestException` с условным кодом HTTP (переполнение — 400 с признаком переполнения, частота — 429, доступ — 403, отказ — 400, сеть — без ответа, прочее — 500). Код подобран так, что перевод обратно дает тот же `LlmFailure`. Исключения две: `Cancelled` возвращается как `Network`, незаданный код как `Other`, потому что в старой форме их не различить. `TokenizeAsync` клиент над портом не поддерживает: в `IModelPort` такой операции нет.

### Что отвергается до отправки

Запрос, который старая форма не выражает, бросает исключение при вызове `AskAsync` или `StreamAsync`, до обращения к клиенту:

- картинка или звук не в сообщении пользователя: конструктор `LLMMessage` молча выбрасывает их из остальных ролей, поэтому переносить их в сообщение пользователя должен цикл (`NotSupportedException`);
- звук по адресу: в старой форме звук только байтами (`NotSupportedException`);
- `ToolMode.Unknown`, `Required` или `Named` без инструментов, `ToolName` не из списка (`ArgumentException`).

### Происхождение

В старой форме происхождения нет. Внешний текст (`OriginKind.External`) уходит без обрамления, как на старом пути. Формат обрамления выберет сборщик контекста на шаге Ф5. Обратно каждая часть получает `OriginKind.Unknown`: доверенным такой текст контракт не считает.

### Что не переводится

Полный список лежит в `ModelTranslationTable`, его проверяет такой же храповик, как у ReAct. Главное:

- обратно не передаются настройки, которых нет в `ModelAsk`: температура, `TopP`, `TopK`, рассуждение, модальности, `IncludeUsage`, `json_object` вместо схемы. О каждой пишется одно предупреждение на клиент;
- в ответе теряются картинки, сгенерированные моделью, текст отказа, второй и следующие варианты, токены рассуждения и кэша, идентификатор и провайдер ответа;
- роль модели при `Fixed` не используется: модель на все роли одна.

### Стоимость числом

`CostExtractor` (AI.LLM) разбирает число CLR в `Usage.Cost` строкой текущей культуры. При запятой в дроби (ru-RU) 0,5 читается как 5. JSON провайдера этой ошибки не знает. Адаптер берет число CLR как есть, а обратно кладет стоимость числом JSON. Сам `CostExtractor` и `AgentUsage` не меняются.

### Долг: вызовы мимо адаптеров

Файл `Tests/unit/AI.Agents.Interop.Tests/Models/ModelCallDebt.txt` перечисляет вызовы `ILLMClient.Send*` в `src/AI.LLM/Agents`, `src/AI.LLM/Services/Reranking/Llm` и `src/AI.Script.Llm`. Сейчас их десять: `Agent`, `NativeToolCallPolicy`, `StructuredJsonPolicy`, `DelegateReActSynthesizer`, `PlanGenerator`, `LlmStepValidator`, `SummarizationMemory`, `LlmReranker`, `LlmModule`, `ScriptWriter`. Храповик `ModelCallDebtTests` не дает долгу расти, а переведенный вызов требует вычеркнуть.

## Имена

Контракты и `AI.LLM` оба объявляют `ToolResult` и `ToolCall` (долг в `OwnershipTests.NameDebt` контрактов). Поэтому здесь пространство `AI.LLM.Agents.Multimodal` целиком не подключается: `AgentImage` берется псевдонимом `using`. В адаптерах модели оба `ToolCall` берутся псевдонимами `ContractToolCall` и `LlmToolCall`. В таблице перевода старый записан ключом `LLM.ToolCall`.
