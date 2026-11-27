# AI.Agents.Interop

Адаптеры между старыми типами агентного слоя `AI.LLM` и контрактами [AI.Agents.Contracts](../AI.Agents.Contracts/README.md). Сборка добавляет только новые типы: поведение `AI.LLM` не меняется. В сессии С2 `AI.LLM` получил только добавления (правило `ToolTextResult`, перегрузки с ним) и пометки `[Obsolete]` на прежних входах, которые работают как раньше.

## Почему отдельная сборка

`AI.LLM` — обычный пакет `4.x`, а контракты — предварительный (`0.1.0-preview.1`). Обычный пакет не зависит от предварительного (решение владельца), поэтому `AI.LLM` на контракты не ссылается, а `[AgentTool]` не меняется. Адаптеры, атрибут эффекта, гейт и движки живут здесь: эта сборка ссылается и на `AI.LLM`, и на контракты. Она тоже предварительный пакет (`0.1.0-preview.1`): `.\build.ps1 -Pack` ее собирает, а `build/nuget-push.ps1` выкладывает пакеты с суффиксом предварительной версии только с флагом `-IncludePreview`. Ссылку `AI.LLM` → контракты вернут к 1.0 контрактов или к шагу Ф5 отдельным решением.

**Interop и `AI.LLM` выпускаются вместе.** Пакет опирается на закрытые члены `AI.LLM` (`InternalsVisibleTo`), форма которых от версии к версии не обещана. Поэтому зависимость на `AI.LLM` в пакете — точная версия, та же, с которой собрана сборка (`[4.0.0]`), а не диапазон «не ниже»: с диапазоном новая `AI.LLM` с переименованным закрытым членом сломала бы адаптер во время выполнения, а не при установке. Точную версию пишет цель `PinAiLlmExactVersion` в `AI.Agents.Interop.csproj`, сам `AI.LLM` для этого не меняется; храповик `EngineRatchetTests.Package_PinsAiLlmToExactVersion`. Привязку снимут, когда нужные члены станут публичными.

## Пространства имен

| Пространство | Что внутри |
|---|---|
| `AI.Agents.Interop.ReAct` | инструмент ReAct (`IReActTool`, `IReActToolSource`, `ReActToolOutcome`, `ReActCitation`) ↔ `ITool`, `IToolSet`, `ToolResult`, `Citation` |
| `AI.Agents.Interop.Models` | клиент модели `ILLMClient` (`LLMMessage`, `GenerateSettings`, `ChatCompletionsResponse`, `LlmRequestException`) ↔ `IModelPort`, `ModelAsk`, `LlmAnswer`; сторож потока `ModelPort.Watched`; классификатор `ModelClassifier` |
| `AI.Agents.Interop.Effects` | атрибут эффекта `ToolEffectAttribute`, набор из `[AgentTool]` (`AttributedTools`), ключ эффекта (`EffectKeys`), гейт эффектов (`EffectGate`, `EffectGateCodes`) |
| `AI.Agents.Interop.Humans` | синхронный канал к человеку над набором: `WithHumanChannel`, `AnsweredToolContext`, `HumanChannelCodes` |
| `AI.Agents.Interop.Engines` | движки `IEngine`: `ReActEngineAdapter` (`react`), `PlanningEngineAdapter` (`plan`), `ReActEngineTranslationTable`, `EngineInteropCodes`; эталонный бюджет хода `LimitedRunBudget` |

## Точки входа

| Вызов | Что получается |
|---|---|
| `reactTool.AsTool(action: null, log: null)` | `ITool` над старым инструментом |
| `tool.AsReActTool(log: null)` | `IReActTool` над инструментом контракта |
| `source.AsToolSet(run: null, actions: null, log: null)` | `IToolSet` над источником старых инструментов |
| `source.AsToolSet(specs, run: null, log: null)` | то же, но вид действия и «повтор безопасен» берутся из описаний инструментов (`ToolSpec`) одним входом: гейт получает обе оси, хосту не нужно собирать спецификацию второй раз |
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

Простая схема `{"input": string}` и разворачивание аргумента — внутренние методы `NativeToolCallPolicy` (`BuildPlainSchema`, `Unwrap`). Чтобы не копировать их, `AI.LLM` открывает свои внутренние члены этой сборке (`InternalsVisibleTo`), и адаптер вызывает их напрямую: переименование ловит компилятор. Атрибут виден в снимке API `AI.LLM` — это решение владельца. Так же берется `ToolRegistry.ScanMethods` (имя инструмента и метод `[AgentTool]`). Отражения для вызова членов `AI.LLM` в сборке нет, это проверяет храповик `NoReflectionTests`. Исключение в нем ровно одно и записано с причиной: `Effects/AttributedTools.cs` читает пользовательский атрибут `[ToolEffect]` с метода и ищет `[ToolEffect]` без `[AgentTool]`. Это чтение метаданных пользовательского типа, а не вызов членов `AI.LLM`.

## Атрибут эффекта

`[AgentTool]` в `AI.LLM` не меняется, поэтому вид действия и «повтор безопасен» объявляет отдельный атрибут `[ToolEffect(action, idempotency)]` на том же методе. Оба значения обязательны в конструкторе: выбор не пропускается молча. Читает атрибут только `AttributedTools.AsToolSet(instances, textResults, log)`: реестр `ToolRegistry` о нем не знает, исполнение и схема те же, что у `ToolRegistryToolSource`.

```csharp
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using AI.LLM.Agents.Tools;

public sealed class MailTools
{
    [AgentTool("send_mail", "Отправить письмо")]
    [ToolEffect(ToolAction.Irreversible, ToolIdempotency.NotIdempotent)]
    public ToolResult SendMail(string to, string body) => new()
    {
        Status = ToolStatus.Ok, Text = "отправлено", Origin = new Origin { Kind = OriginKind.Trusted },
    };
}

IToolSet tools = AttributedTools.AsToolSet([new MailTools()], ToolTextResult.Refused);
```

| Случай | Что получается |
|---|---|
| `[AgentTool]` и `[ToolEffect]` | `ToolSpec.Action` и `ToolSpec.Idempotency` из атрибута |
| `[AgentTool]` без `[ToolEffect]` | `ToolAction.Unknown` и `ToolIdempotency.Unknown` (действуют как «необратимо» и «повторять нельзя») и одно предупреждение в журнал на инструмент; `Read` и `Idempotent` сами не появляются |
| `[ToolEffect]` без `[AgentTool]` | `ArgumentException` при построении набора с именем типа и метода |

### Итог метода

Строка не говорит, удалось ли действие, поэтому правило для строкового результата выбирается явно (`ToolTextResult` из `AI.LLM`, то же правило у `ToolRegistryToolSource` и `ReActAgentBuilder`). Старая перегрузка `AsToolSet(instances, log)` помечена `[Obsolete]` и работает как `ToolTextResult.Success`.

| Метод вернул | Итог контракта |
|---|---|
| `ToolResult` контракта (или `Task` с ним) | проходит целиком: статус, код из `ToolCodes`, `Handle`, подтверждения, файлы |
| `ToolResult` из `AI.LLM` (`Success` или `Failure`) | `Ok` или `Failed` |
| ничего (`void`, `Task`) | `Ok`; исключение — `Failed` |
| строку, `object`, `ValueTask` и прочее | `ToolTextResult.Success` — `Ok`, как раньше; `ToolTextResult.Refused` — `ArgumentException` при построении набора с именами таких инструментов; `Unspecified` — `ArgumentOutOfRangeException` |

Итог контракта едет через старую форму в `ReActToolOutcome.Payload` и переводом назад (`ReActTranslation`) отдается без потерь. Так `[AgentTool]`-инструмент может вернуть отказ до действия (`transient`), неизвестный исход или отложенный результат (`pending` с `Handle`), и гейт эффектов поймет его так же, как у любого `ITool`.

## Ключ эффекта

`EffectKeys.Create(runId, toolName, arguments)` — единственное место во фреймворке, где создается `ToolCall.EffectKey`. Строка `длина runId : runId : длина имени : имя : канонический JSON аргументов` хешируется SHA-256, ключ — версия формулы `ek1:` (`EffectKeys.Version`) и 64 строчных шестнадцатеричных знака. Хеш нужен, чтобы аргументы не утекли внешнему сервису в заголовок `Idempotency-Key` и длина ключа была постоянной.

Версия формулы меняется (`ek2:`), если прежние входы начинают давать другие ключи: ключи разных версий не совпадают, и журнал старого запуска не примет чужой ключ за свой. Эталоны формулы (`EffectKeyTests.Golden_Ek1`: фиксированный вход — конкретная строка ключа) меняются только решением владельца и только вместе с версией.

Ключ — точная идентичность эффекта, поэтому канонизация строгая:

| Что | В ключе |
|---|---|
| порядок полей объекта (на любой глубине) | не важен: ключи отсортированы порядковым сравнением |
| пробелы и переводы строк между токенами JSON | не важны |
| регистр и пробелы внутри строкового значения | различаются |
| запись числа (`100`, `100.0`, `1e2`, `1E+2`; `0` и `-0`; `0.1` и `0.10`) | не важна: число сравнивается по значению |
| разные значения чисел (в том числе `9007199254740993` и `9007199254740992`) | различаются всегда |
| порядок элементов массива | различается |
| экранирование знака в строке (`\u00e9` и `é`) | не важно: значение строки то же |
| имя инструмента | порядковое сравнение, с учетом регистра |

**Числа.** Число приводится по своей десятичной записи, без `double` и `decimal`: значащие цифры без ведущих и хвостовых нулей и порядок, вид `[-]цифры e порядок`, ноль — `0` (`100` → `1e2`, `0.10` → `1e-1`, `-0.0` → `0`). Значение так определяется точно при любом числе цифр, поэтому разные значения никогда не дают один ключ: ни соседние целые, неразличимые в `double`, ни числа длиннее точности `decimal`. Если порядок не помещается в `long` (больше 18 цифр), число остается своей записью без приведения: лучше лишний вызов, чем пропущенное действие.

**Почему не `ReActActionKey`.** `ReActActionKey` из `AI.LLM` — эвристика, по которой движок ReAct узнает, что модель повторяет действие: он не различает регистр и схлопывает пробелы внутри строк. Для повтора модели это верно, для эффекта нет: два письма, которые отличаются только регистром текста, получили бы один ключ, и второе не ушло бы, а вернуло итог первого. Это разные понятия, и канонизаций в `src` ровно две: `ReActActionKey` (не тронут) и `EffectKeys`. Храповик `EngineRatchetTests` проверяет, что других нет, что SHA-256 считается только в `EffectKeys`, что `ToolCall.EffectKey` назначается только через `EffectKeys.Create` и что `ReActActionKey.Create` вызывает только движок ReAct.

## Гейт эффектов

`new EffectGate(set, log)` оборачивает любой `IToolSet`. Перед каждым вызовом он назначает ключ эффекта (ключ хоста заменяется, с предупреждением) и ведет журнал эффектов запуска `Journal` (`EffectRecord`: ключ, инструмент, итог), который хост кладет в `RunState.Effects`. Продолжение запуска создает гейт с прежним журналом: `new EffectGate(set, runState.Effects ?? [], log)`; два раза один ключ или пустая запись — `ArgumentException`. О каждом изменении журнала (начало вызова, итог, снятие записи) гейт сообщает событием `JournalChanged` со снимком всего журнала, чтобы хост сохранял его сразу; исключение обработчика пишется ошибкой и вызов не прерывает.

| Случай | Что делает гейт |
|---|---|
| вид `Read` или `Compute`, неизвестное набору имя | вызов идет к набору без журнала |
| вид `Reversible`, `Irreversible`, `Unknown` | запись в журнал до вызова итогом `Unknown`, после вызова — настоящий итог |
| ключ уже исполнен с итогом `Ok` | инструмент не вызывается, возвращается сохраненный итог |
| ключ начинался и кончился иначе (сбой, обрыв, исключение, поток без итога, вызов еще идет) | `Idempotent` и `WithKey` — вызов повторяется с тем же ключом; `NotIdempotent` и незаданное — `Failed` с кодом `repeat_unsafe`, инструмент не вызывается |
| итог `Denied`, `NeedsInput`, `AwaitsUser` | действие не исполнялось: запись снимается, вызов можно повторить (например, с ответом человека) |
| итог `Failed` с кодом отказа до действия (`ToolCodes.NotPerformed`: `transient`, `not_configured`, `needs_reconnect`, `needs_input`, `too_long`, `bad_markup`, `no_rights`, `not_found`, `unknown_tool`) | сервер ничего не сделал: запись снимается (или возвращается прежняя), вызов можно повторить |
| итог `Ok` с кодом `pending` и `Handle` | действие начато: запись остается, повтор возвращает тот же `Handle`, второй раз действие не запускается |

Поток без итога дает `Unknown` с кодом `no_result`, исключение — `exception` (исключение пробрасывается дальше), отмена и остановка чтения до итога — `interrupted`. Повтор с другим ключом через гейт невозможен: ключ детерминирован.

Между прогонами одного запуска защита держится на прежнем журнале: ключ с итогом `Ok` не исполняется второй раз, начатый с неизвестным исходом (`Unknown` потерянного прогона) повторяется только при `Idempotent` и `WithKey`. Движки `react` и `plan` пока не принимают `EngineInput.State`, поэтому гейт с прежним журналом хост собирает сам и передает набором: движок не оборачивает уже огражденный набор второй раз.

## Канал к человеку

`set.WithHumanChannel(channel, log)` — временная синхронная реализация `AwaitsUser` через `IHumanChannel`. Если инструмент вернул `AwaitsUser` или `NeedsInput` с вопросом, набор спрашивает человека и один раз повторяет вызов, передав ответ наследником контекста `AnsweredToolContext` (новых полей у `ToolContext` нет). Ответ на другой вопрос — `Failed` с кодом `answer_mismatch`; вопрос о согласии без явного «да» — `Denied` с кодом `declined`; повторный вопрос отдается итогом как есть. Если контекст вызова уже наследник с данными хоста, заменить его без потери нельзя: вопрос отдается итогом, в журнал пишется предупреждение.

Все происходит внутри вызова набора, поэтому старый движок над ним своего поведения не меняет: он видит только итог. Канал ставят снаружи гейта (`gate.WithHumanChannel(channel)`), тогда повтор с ответом тоже идет через гейт. Его заменит продолжение запуска с места (Ф6).

Опасность, видная только по состоянию мира (кнопка в браузере оказалась оплатой), объявляется так же: инструмент сам возвращает `AwaitsUser` с вопросом `Confirm`, видом действия, целью и суммой в тексте, а `ITool.Classify` не меняется.

## Движки

| Ключ | Тип | Что внутри |
|---|---|---|
| `react` | `ReActEngineAdapter` | `ReActEngine`, собранный `ReActAgentBuilder` на каждый прогон |
| `plan` | `PlanningEngineAdapter` | `PlanningAgent`, собранный `PlanningAgentBuilder`; шаг плана исполняет тот же цикл ReAct |

Общее у обоих: модель — `EngineInput.Model` под `ILLMClient` (`AsLlmClient`, роль `decide`), инструменты — `EngineInput.Tools` через гейт эффектов и `AsToolSource`. Набор, уже обернутый гейтом (в том числе под каналом к человеку), второй раз не оборачивается: так хост читает `Journal`. Инструкции — системный промпт, история и картинки из `EngineContext` идут в запрос (`plan` их не принимает и пишет предупреждение). `Deliverable` и `Remarks` дописываются к тексту задачи: «Что сдать: …» и список «Замечания к прошлой попытке:». Инструмент получает в `ToolContext` идентификатор запуска; путь шага в нем — ярус 1 у `react` и ярус и шаг плана у `plan`, потому что старый движок метку по шагам не обновляет.

Параметры (`EngineInput.Options`): `maxIterations` (целое ≥ 1, предел шагов цикла; без него у `react` — `ReActConfig` по умолчанию, у `plan` — 30 на попытку шага, как у старого исполнителя) и `decisions` (`native` или `json`; без него — по `ModelCapabilities.ToolCalling` роли `decide`). Другой ключ или значение — итог `Failed` с кодом `invalid_options`.

### Перевод `react`

Причина остановки → статус и код (`ReActEngineTranslationTable.StopReasons`):

| `ReActStopReason` | `EngineStatus` | `StopCode` |
|---|---|---|
| `FinalAnswer` | `Done` | `final_answer` |
| `TerminalTool` | `Done` | `terminal_tool` |
| `IterationLimit` | `Incomplete` | `step_limit` |
| `TimeLimit` | `Incomplete` | `time_limit` |
| `NoProgress` | `Incomplete` | `no_progress` |
| `NoTools` | `Incomplete` | `no_tools` |
| `PolicyFailure` | `Failed` | `policy_failure` |
| `Cancelled` | `Cancelled` | `cancelled` |
| `EngineFailure` | `Failed` | `engine_failure` (текст сбоя — в журнал) |

События (`ReActEngineTranslationTable.Events`): фаза шага → `Step`, рассуждение → `Step` того же шага с `Thought`, начало инструмента → `ToolStarted` (без ключа: его назначит гейт), прогресс → `ToolProgress`, наблюдение → `ToolFinished` с итогом и вызовом с ключом из набора, фрагменты → `Text` и `Reasoning`, расход → `Spent`, итог → `Finished`. Служебная пометка движка (`Note`) и фаза синтеза не переводятся. Храповик требует запись для каждого события и причины.

Эталон: один сценарий через старый вход (`ReActAgentBuilder` + `ILLMClient` + `IReActTool`) и через адаптер (`IModelPort`, `IToolSet`, гейт) дает тот же ответ, те же сообщения модели и тот же аргумент инструменту (`EngineAdapterTests.React_Reference_SameAnswerAndSameModelMessages`).

### Перевод `plan`

`PlanningAgent` не меняется. Его исполнитель шагов (`Agent`) вызывает инструменты через `ToolRegistry` напрямую, мимо `IToolSet`, поэтому провести его вызовы через гейт без потерь нельзя. Адаптер передает свой исполнитель шагов (публичный `WithStepExecutor`): шаг исполняет цикл ReAct тем же образцом, что `react`, над одним гейтом на весь прогон, и повтор шага или перепланирование не исполняют уже совершенный эффект второй раз. Планировщик видит инструменты по `IToolSet.Available` через реестр, обработчик которого не вызывается (а если вызван — отвечает отказом, а не исполнением мимо гейта).

Настройки шага сверены с исполнителем шагов старого `PlanningAgent` (`Agent`, который собирает `PlanningAgentBuilder`):

| Настройка старого исполнителя | В адаптере |
|---|---|
| предел 30 итераций на попытку шага (`WithMaxIterations(30)`) | тот же: 30, если `maxIterations` не задан |
| системный промпт — промпт `PlanningAgentBuilder.WithSystemPrompt` | тот же текст: `EngineContext.Instructions` идут и в планировщик, и в шаг |
| повторы шага, приемка, перепланирование | те же: это код `PlanningAgent`, адаптер его не заменяет |

Расхождения — то, что без правок `AI.LLM` и контрактов не выражается:

- **Температура 0,15** (`WithTemperature(0.15)`). В запросе порта модели (`ModelAsk`) температуры нет, она задается начальными настройками порта (`ModelPort.Fixed`). Шаг идет с температурой порта.
- **Предел токенов ответа.** Старый исполнитель его не задает; цикл ReAct без явных настроек политики задает свой, и порт получает его в `ModelAsk.MaxTokens`.
- **Обертка системного промпта.** Старый `Agent` отправляет промпт как есть, цикл ReAct оборачивает его своим шаблоном (описание инструментов, правила ответа).
- **Запрос шага.** С `WithStepExecutor` `PlanningAgent` сам строит другой запрос: «Общая задача», итоги принятых шагов, «Сделай этот шаг», критерий готовности и отклоненный ответ прошлой попытки. Свой запрос старого исполнителя (`BuildStepQuery`) закрытый и недоступен.
- **Память шагов.** Старый исполнитель получает `StepMemory`; адаптер — итоги принятых шагов в тексте запроса (их кладет `PlanningAgent`).
- **Страж и наблюдатель** (`WithGuard`, `WithObserver`). У контракта их нет, адаптер их не задает.

Тесты: `EngineAdapterTests.Plan_StepIterations_MatchOldExecutor` (30 обращений к модели на попытку шага, `maxIterations` заменяет предел) и `Plan_StepIterations_AreThoseOfOldBuilder` (старый `PlanningAgentBuilder` все еще задает 30 и 0,15; изменение в `AI.LLM` роняет тест, и таблицу пересматривают).

Итог: план выполнен — `Done`, `final_answer`, текст — ответ последнего принятого шага; план не утвержден — `Failed`, `plan_rejected`; иначе — `Incomplete`, `no_progress`, в `Unfinished` — описания непринятых шагов. События: начало шага плана → `Step` с ярусом плана и идентификатором шага в `Branch`, события цикла внутри шага идут с тем же путем; расход планировщика → `Spent`.

### Границы до шагов Ф5 и Ф6

- **Продолжение с места.** `EngineInput.State` или ответ человека (`EngineTask.Answer`) на входе — итог `Failed` с кодом `continuation_not_supported`, модель не вызывается. Старые движки состояния не принимают.
- **Срок.** `IRunBudget.Deadline` задается до старта: у `react` — пределом времени движка (`TimeLimit` → `Incomplete`, `time_limit`), у `plan` — отменой по сроку (`Incomplete`, `time_limit`). Срок, истекший до старта, — `Incomplete` без вызова модели.
- **Шаги.** Бюджет спрашивается в начале каждого шага цикла (`TryStep`); отказ останавливает прогон до вызова модели: `Incomplete` с кодом бюджета.
- **Токены и деньги.** Расход каждого вызова модели сообщается бюджету (`Spend`) и проверяется его `TryStep` в начале следующего шага. Внутри шага (несколько инструментов, синтез ответа после цикла) и у планировщика между шагами проверки нет: перерасход возможен на один шаг. Проверка на каждом вызове модели — порт `WithBudget` у `ReActEngine` (Ф5).
- **Дочерние агенты.** `EngineInput.Steps` не используется: старые движки других агентов не зовут.

## Модель

### Точки входа

| Вызов | Что получается |
|---|---|
| `ModelPort.Fixed(client, capabilities, settings: null, log: null)` | `IModelPort` над `ILLMClient`: одна модель на все роли |
| `client.AsModelPort(capabilities, settings: null, log: null)` | то же расширением |
| `ModelPort.Routed(chains, log: null)` | порт по ролям: роль → основная модель и запасные (`ModelSlot(model, port)`). Переход по цепочке — одно правило `ModelFallback`; итог называет ответившую модель (`LlmAnswer.Model`), расход — по всем попыткам. Незнакомая роль — `ArgumentException`, а не модель по умолчанию. Это единственное место, где роль сопоставляется с именем модели |
| `port.AsLlmClient(role, log: null)` | `ILLMClient` над портом: старые пути (`Agent`, `PlanGenerator`, `ScriptWriter`, модуль `llm`) ходят через порт без изменений в себе |
| `port.Watched(watch, time: null, log: null)` | сторож потока (`StreamWatch`): нет первого фрагмента за `FirstChunk` или тишина дольше `Idle` посреди ответа — итог `Failed`, `Stalled`, текст причины в `FailureText`, внутренний поток отменяется. Порт без настоящего потока идет без сторожа |
| `new ModelClassifier(port, role, classes, prompt)` | классификатор поверх порта (замена `ClassifierWithLLM`, см. ниже) |

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

Клиент не сообщает, что умеет модель, поэтому `ModelCapabilities` задает вызывающий, в том числе размер окна `ContextTokens`, если он известен (`Describe` отдает его как есть). При `Streaming = true` поток идет через перегрузку `SendFullAsync` с `onChunk`, а при `false` порт отдает один итоговый кадр, как реализация по умолчанию. Начальные настройки (`GenerateSettings`) несут то, чего нет в контракте: `TopP`, бюджет рассуждения, `IncludeUsage` для стоимости у OpenRouter. Инструменты, режим инструментов, схему ответа, предел токенов, температуру (`ModelAsk.Temperature`) и уровень размышления (`ModelAsk.Reasoning`) задает запрос; `null` оставляет значение начальных настроек. Уровень размышления ложится в `ReasoningSettings.Effort`, если начальные настройки говорят с провайдером объектом рассуждения (OpenRouter), иначе в `ReasoningEffort`.

Ответ по схеме (`ModelAsk.Schema`) проверяется правилом контракта `LlmAnswers.RequireJson`: `Ok` значит разбираемый JSON, ответ не JSON — `Failed`, `InvalidOutput` (текст ответа в `FailureText`, расход сохранен), пустой ответ — `Empty`, а не сбой.

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

Отмена токеном вызывающего остается исключением, как требует контракт. Текст сбоя для человека едет рядом с кодом в `LlmAnswer.FailureText`: сообщение исключения клиента, текст отказа модели или причина `content_filter`. `RetryAfter` в `LlmAnswer` не помещается, адаптер пишет его в журнал. Расход берется из `Usage`: нули без стоимости означают, что провайдер ничего не сообщил, и дают `null`. Последний кадр потока — итог с расходом.

Обратно итог без ответа становится тем исключением, по которому старые вызывающие его различают: `Empty` — `LlmRequestException.NoResponse(EmptyModelAnswerException)` (`IsEmptyAnswer`, как у `ChatLLMApi`), `Failed` — `LlmRequestException` с условным кодом HTTP (переполнение — 400 с признаком переполнения, частота — 429, доступ — 403, отказ — 400, сеть — без ответа, прочее — 500). Код подобран так, что перевод обратно дает тот же `LlmFailure`, а `FailureText` дописывается к сообщению исключения. Исключения четыре: `Cancelled` и `Stalled` возвращаются как `Network` (ответа нет), незаданный код и `InvalidOutput` — как `Other`, потому что в старой форме их не различить. `TokenizeAsync` клиент над портом не поддерживает: в `IModelPort` такой операции нет.

### Запасные модели и сбой хоста

`ModelFallback.Cures(failure)` решает переход по цепочке: лимит частоты, отказ провайдера, нет доступа, сеть, модель замолчала, ответ не того вида и прочий сбой — следующая модель; переполнение окна и отмена не вызывающим — нет (менять надо запрос). Незнакомый код — `ArgumentOutOfRangeException`. В потоке следующая модель берется, только если прежняя не отдала ни одного кадра: начатый показ чужим ответом не переписывается.

Сбой хоста модели в движке (`react`, `plan`, приемка шага) — итог `Failed` с одним кодом `StopCode.ModelFailure` (`model_failure`); в `Unfinished` — код `LlmFailure` и причина. Собирает его одно место, `ReActRun.ModelFailed`. Ключи, повторы и прокси — ниже, в `AI.LLM` (`ProviderHttpClient`, `ApiKeyPool`, `RetryPolicy`): порт над клиентом получает их готовыми.

### Что отвергается до отправки

Запрос, который старая форма не выражает, бросает исключение при вызове `AskAsync` или `StreamAsync`, до обращения к клиенту:

- картинка или звук не в сообщении пользователя: конструктор `LLMMessage` молча выбрасывает их из остальных ролей, поэтому переносить их в сообщение пользователя должен цикл (`NotSupportedException`);
- звук по адресу: в старой форме звук только байтами (`NotSupportedException`);
- `ToolMode.Unknown`, `Required` или `Named` без инструментов, `ToolName` не из списка (`ArgumentException`).

### Происхождение

В старой форме происхождения нет. Внешний текст (`OriginKind.External`) уходит без обрамления, как на старом пути. Формат обрамления выберет сборщик контекста на шаге Ф5. Обратно каждая часть получает `OriginKind.Unknown`: доверенным такой текст контракт не считает.

### Что не переводится

Полный список лежит в `ModelTranslationTable`, его проверяет такой же храповик, как у ReAct. Главное:

- обратно не передаются настройки, которых нет в `ModelAsk`: `TopP`, `TopK`, бюджет и исключение рассуждения, незнакомый уровень рассуждения, модальности, `IncludeUsage`, `json_object` вместо схемы. О каждой пишется одно предупреждение на клиент. Температура и уровень рассуждения (`none`, `minimal`, `low`, `medium`, `high`) с сессии С2 переводятся;
- в ответе теряются картинки, сгенерированные моделью, отказ рядом с ответом, второй и следующие варианты, токены рассуждения и кэша, идентификатор и провайдер ответа;
- роль модели при `Fixed` не используется: модель на все роли одна.

### Стоимость числом

`CostExtractor` (AI.LLM) разбирает число CLR в `Usage.Cost` строкой текущей культуры. При запятой в дроби (ru-RU) 0,5 читается как 5. JSON провайдера этой ошибки не знает. Адаптер берет число CLR как есть, а обратно кладет стоимость числом JSON. Сам `CostExtractor` и `AgentUsage` не меняются.

### Долг: вызовы мимо адаптеров

Файл `Tests/unit/AI.Agents.Interop.Tests/Models/ModelCallDebt.txt` перечисляет вызовы `ILLMClient.Send*` в `src/AI.LLM/Agents`, `src/AI.LLM/Services/Reranking/Llm` и `src/AI.Script.Llm`. Сейчас их десять: `Agent`, `NativeToolCallPolicy`, `StructuredJsonPolicy`, `DelegateReActSynthesizer`, `PlanGenerator`, `LlmStepValidator`, `SummarizationMemory`, `LlmReranker`, `LlmModule`, `ScriptWriter`. Храповик `ModelCallDebtTests` не дает долгу расти, а переведенный вызов требует вычеркнуть.

### Классификатор

`ModelClassifier` относит текст к одному из классов-слов через `IModelPort`: запрос с температурой 0 (`Temperature`, `MaxTokens`, `Reasoning` настраиваются), класс — первое слово ответа из списка, без учета регистра и знаков по краям. Итог `Classification` несет статус, класс и весь `LlmAnswer` с расходом и текстом сбоя:

| Статус | Когда |
|---|---|
| `Ok` | в ответе есть слово из списка |
| `NoClass` | модель ответила, но слова из списка нет |
| `Empty` | модель не написала ответа |
| `Failed` | вызов не состоялся: код и текст в `Answer` |

`ClassifierWithLLM` из `AI.LLM` помечен `[Obsolete]` и работает как прежде: он ходит мимо порта (без повторов, цены и плательщика хоста) и сбой сообщает исключением. Переход со старой настройки — `ModelClassifier.FromChatApi(chatApi, classes, prompt)`. Вероятностей классов у замены нет: порт не отдает вероятности токенов.

## Бюджет хода

`LimitedRunBudget` — эталонный `IRunBudget` по пределам `RunLimits` (шаги, токены, деньги, вызовы модели, длительность). Его делят все участники хода. Первый исчерпанный предел запоминается и отдается кодом из `TryStep` (`step_limit`, `token_limit`, `cost_limit`, `call_limit`, `time_limit`). Исчерпание отменяет `Exhausted`: долгая работа внутри шага прекращается, не дожидаясь границы шага. Вызов модели без стоимости при пределе денег считается в `UnpricedCalls`, чтобы неполный учет не выглядел точным. Пределы хоста сводятся с потолками агента через `RunLimits.Narrow(agentLimits)`, побеждает меньший.

## Имена

Контракты и `AI.LLM` оба объявляют `ToolResult` и `ToolCall` (долг в `OwnershipTests.NameDebt` контрактов). Поэтому здесь пространство `AI.LLM.Agents.Multimodal` целиком не подключается: `AgentImage` берется псевдонимом `using`. В адаптерах модели оба `ToolCall` берутся псевдонимами `ContractToolCall` и `LlmToolCall`. В таблице перевода старый записан ключом `LLM.ToolCall`.
