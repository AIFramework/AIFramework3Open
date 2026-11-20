# Контракт памяти: `AI.Agents.Contracts.Memory`

Дата: 21.11.2026. Изменения только добавляют типы. `src/AI.LLM` не изменен, реализаций и адаптеров нет, снимок `AI.Agents.Contracts.txt` получил 156 строк добавлений и ни одного удаления. Ничего не закоммичено.

Основа: разведка `memory-recon.md` (раздел 11.2, черновик контракта; раздел 13, развилки), `kg-fractal-memory-recon.md` (раздел 4, объект памяти) и решения владельца от 21.11.

## Что добавлено

- Новое пространство `AI.Agents.Contracts.Memory`: порт `IMemoryPort` и 16 типов, по файлу на тип в `src/AI.Agents.Contracts/Memory/`.
- `Memory/README.md`: что это, правила формы, таблица двух реализаций, две границы.
- Строка пространства в таблице `src/AI.Agents.Contracts/README.md`.
- Образцы JSON всех новых записей в `ContractSamples.cs` и тесты `MemoryContractTests.cs`.
- Обновленный снимок API.

## Типы

| Тип | Назначение |
|---|---|
| `IMemoryPort` | `WriteAsync(MemoryWrite) → MemoryWriteResult`, `ViewAsync(MemoryAsk) → MemoryView` |
| `MemoryScope` | `Owner` (обязателен), `Project`, `Chat`; ставит хост |
| `MemoryEpisode` | `Id`, `Actor`, `At` (обязательны), `Text`, `Read` (список `Citation`: что ход прочитал) |
| `MemoryActor` | `Unknown`, `User`, `Assistant`, `Agent`, `Host` |
| `MemoryWrite` | `Scope`, `Episode` (обязательны), `Changes` (`null`: извлечь из текста), `Withdraw` |
| `MemoryChange` | `Op` (обязателен), `Fact`, `Target`, `Reason` |
| `MemoryOp` | `Unknown`, `Add`, `Revise`, `Confirm`, `Retire` |
| `MemoryObject` | `Id`, `Name` (обязательны), `Kind` (один вид или `null`) |
| `MemoryFact` | `Subject`, `Property`, `Value`, `Source` (обязательны), `Id`, `Target`, `ValidFrom`, `ValidTo`, `Recorded`, `Episodes`, `Premises`, `Quote` |
| `ValueSource` | `Unknown`, `Said`, `Document`, `Kind`, `Derived` |
| `MemoryWriteResult` | `Status`, `Outcomes` (обязательны), `Problems` |
| `MemoryOutcome` | `Fact`, `Verdict` (обязательны), `Reason`, `Existing` |
| `MemoryVerdict` | `Unknown`, `Written`, `Revised`, `Confirmed`, `Proposed`, `Archived`, `Rejected` |
| `MemoryAsk` | `Scope`, `Task`, `Budget` (все обязательны) |
| `MemoryView` | `Status`, `Profile`, `Focus`, `Others`, `Trace` (обязательны), `Problems` |
| `MemoryPick` | `Fact`, `Channel` (обязательны), `Score`; известные каналы в `MemoryPick.Channels` |
| `MemoryStatus` | `Unknown`, `Ok`, `Partial`, `Failed` |

Стиль соседних контрактов соблюден: только BCL, члены `required init`, ноль каждого перечисления равен `Unknown` со `StrictEnumConverter`, необязательное допускает `null` с описанным смыслом. Занятые имена (`IAgentMemory`, `Origin`, `MemoryEntry`) не использованы, новых совпадений в `OwnershipTests` нет.

## Отличия от черновика 11.2

Черновик взят за основу, но в нескольких местах форма изменена. Причины ниже.

| Черновик | Сделано | Почему |
|---|---|---|
| `MemoryScope.Thread` | `MemoryScope.Chat` | в решении владельца область названа «владелец, проект, чат» |
| `MemoryChange.Fact` обязателен | `Fact` необязателен, добавлен `Reason` | для `Confirm` и `Retire` нужен только `Target`; у `Retire` есть причина. Неполное изменение реализация отклоняет исходом `Rejected`, а не исключением |
| `MemoryOutcome.Merged` (только для `Confirmed`) | `MemoryOutcome.Existing` | тот же смысл нужен и для `Revised`: какой факт закрыт новым значением |
| у `MemoryWriteResult` нет списка проблем | добавлен `Problems` | при `Failed` без исходов иначе негде сказать причину; у `MemoryView` такой список уже был |
| `MemoryView.Core`, `Elsewhere` | `Profile`, `Others` | слова решения владельца: «профиль» и «строка имен остальных объектов» |
| `MemoryPick.Why` (строка) | `MemoryPick.Channel` и константы `Channels` | открытый набор строк с известными значениями, как `LocationSegment.Kinds`: у двух реализаций разные каналы, поэтому перечисление было бы тесным |
| параметр `ct` | `cancellationToken` | как в `IRunStore` и других портах контрактов |

## Проверки, которые закрепляют решения

`MemoryContractTests`:

- `Port_HasOnlyWriteAndView`: у порта ровно два метода с ожидаемыми параметрами и итогами; статус лежит в итоге.
- `Enums_HaveAgreedValues`: наборы `MemoryOp`, `ValueSource`, `MemoryVerdict`, `MemoryStatus` совпадают с решением владельца. Новое значение требует нового решения.
- `Memory_DoesNotUseTakenNames`: `IAgentMemory`, `Origin`, `MemoryEntry`, `IUserMemory` в пространстве не объявлены.
- `Scope_LivesOnlyInRequests`: `MemoryScope` встречается только в `MemoryWrite` и `MemoryAsk`, то есть в том, что собирает хост.
- `Object_HasIdNameAndSingleKind`: у объекта ровно `Id`, `Name` и один `Kind`.
- `Write_ReadsFromHostJson`: запись в виде JSON хоста читается без настроек, перечисления именами, даты в формате ISO.
- `Read_MissingRequired_Throws`: исход без вердикта, срез без статуса и объект без `Id` не читаются.

Общие храповики контрактов подхватили новые типы сами: круг через JSON с заполнением всех свойств первым образцом, строгие перечисления, форма `required init`, единственность имен.

## Результаты проверки

- `AI.Agents.Contracts` компилируется без предупреждений.
- `AI.Agents.Contracts.Tests`: 183 из 183.
- `PublicApi.Tests` (фильтр `AI.Agents.Contracts`): снимок обновлен, только добавления (+156 строк).
- Полный прогон `AIFramework.sln` (Release): сборка без ошибок, 49 предупреждений в других сборках, ни одного в `AI.Agents.Contracts`. Все 8 тестовых проектов зеленые, 4186 тестов: `AI.Agents.Contracts.Tests` 183, `AI.LLM.UnitTests` 189, `AI.Agents.Interop.Tests` 177, `AI.Solvers.Chem.UnitTests` 444, `PublicApi.Tests` 53, `AIFramework.UnitTests` 1458, `AI.Script.UnitTests` 1355, `AI.Economics.UnitTests` 327.

## Чего не делали

Реализаций, адаптеров к KnowledgeGraph, правок `AI.LLM`, экранов и ответов с интервалами нет по условию задачи. Уверенность факта и распределения значений в контракт не вошли: решения по ним нет, и при необходимости они добавятся необязательными полями.

## Что дальше

1. Реализация №1 в MASAgents: `src/Memory/GraphMemory/MemoryPort.cs` поверх KnowledgeGraph (раздел 11.3 разведки).
2. Общие контрактные тесты порта (одинаковые сценарии записи и среза для обеих реализаций), когда появится первая реализация.
3. Адаптер FAIMemory к порту.
