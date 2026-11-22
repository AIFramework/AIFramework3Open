# Tavily: ключ в заголовке Bearer

Дата: 22.11.2026. Основа: разведка `AIRefactoring/разведка/2026-11-22-api.md`, раздел 1.5 (строка Tavily) и развилка 7.

Коммиты AIFramework3Open: «Tavily: ключ заголовком Authorization: Bearer вместо api_key в теле» (`5f6d7b7`) и поправка «Tavily: ApiKey возвращен с Obsolete, публичный API не ломаем» (`4744852`, CI main зеленый: Build and test и Docs lint). Закрепление в MASMarketplace — раздел «Закрепление в MASMarketplace».

## Что изменено

- `src/AI.LLM/Clients/Tavily/TavilyClient.cs`. Ключ ставится один раз в конструкторе: `HttpClient.DefaultRequestHeaders.Authorization = Bearer <ключ>`. Поле `_apiKey` удалено, ключ в объекте больше не хранится. Запросы `/search` и `/extract` ключа не передают. Добавлен внутренний конструктор `TavilyClient(string apiKey, HttpMessageHandler handler)` для подставного обработчика; публичный `TavilyClient(string apiKey, WebProxy proxy = null)` вызывает его и ведет себя как раньше.
- `Models/SearchArgs.cs`, `Models/ExtractArgs.cs`. Свойство `ApiKey` оставлено, но помечено `[Obsolete]` и `[JsonIgnore]`, атрибут `JsonPropertyName("api_key")` снят: свойство игнорируется и в тело не сериализуется. Сначала свойства были удалены, затем возвращены по решению владельца (вторым коммитом).
- `Tests/unit/PublicApi.Tests/Snapshots/AI.LLM.txt`. У обоих свойств в снимке теперь `[System.Obsolete(...)]` и `[JsonIgnore]` вместо `JsonPropertyName("api_key")`. Сигнатуры публичного API не изменились.
- `Tests/unit/AI.LLM.UnitTests/TavilyClientAuthTests.cs`. Новые тесты.

Про `/usage`: в AIFramework3Open вызова `/usage` нет. Он есть только в MASAgents (`ProviderBalanceProbe.cs:94`) и уже идет с Bearer. Теперь весь клиент AIFramework авторизуется тем же способом.

Ключ в исключениях. Сообщение о пустом ключе называет параметр, а не значение. `EnsureSuccessStatusCode` пишет только код ответа, заголовки запроса в исключение не попадают. Клиент ничего не логирует.

## Разбор `content` с фрагментами «[...]»

`SearchItemResult.Content` — обычная строка, клиент ее не режет и не разбирает. Фильтр `ContainsForbiddenContent` смотрит адрес и `raw_content`, а не `content`. `TavilySearchTool` выводит `content` целиком. Ответ с фрагментами через «[...]» разбирается без потерь, правки разбора не понадобились. Это закреплено тестами.

## Тесты

| Тест | Что проверяет |
|---|---|
| `Search_SendsBearerHeader_AndNoKeyInBody` | `/search`: заголовок `Authorization: Bearer <ключ>`, в теле нет ни `api_key`, ни самого ключа |
| `Extract_SendsBearerHeader_AndNoKeyInBody` | то же для `/extract` |
| `Search_Unauthorized_DoesNotLeakKeyInException` | при 401 ключа нет в тексте исключения |
| `Constructor_WithoutKey_DoesNotMentionKeyValue` | пустой ключ: исключение называет параметр |
| `ObsoleteApiKey_IsNotSerializedIntoBody` | заполненный вызывающим устаревший `ApiKey` не попадает в тело `SearchArgs` и `ExtractArgs` |
| `SearchResult_KeepsContentFragments` | разбор `content` с «[...]» и квадратными скобками в тексте не теряет фрагментов |
| `Search_KeepsContentFragments_ThroughClient` | то же через `SearchAsync` с подставным обработчиком |
| `TavilyClientSources_DoNotMentionApiKeyField` | храповик: в `src/AI.LLM/Clients/Tavily/**/*.cs` нет строки `api_key` |

Локально (Release): `AI.LLM.UnitTests` 197 из 197, `PublicApi.Tests` 53 из 53. Новых предупреждений в файлах Tavily нет.

## Закрепление в MASMarketplace

Ветка MASMarketplace `new_version`, коммиты:

- до удаления Elsa: `53971bff` (Neighbors.props: MASAgents 26ca937);
- удаление Elsa: `8f3a38b1`;
- закреп: `3e7cbe81` «Neighbors.props: AIFramework3Open 4744852 …».

Закреп сделан штатно: `Marketplace/pin-neighbors.ps1 -Only AIFramework3Open`. В `Neighbors.props` изменилась одна строка `AiFrameworkCommit` (848c293 → 4744852), MASAgents, FAIRouter и KnowledgeGraph остались на прежних хешах. Коммит `3e7cbe81` с ровно этой правкой создала параллельная сессия «Удалить Elsa из MASMarketplace целиком», пока шел прогон. Второй коммит с той же правкой я не делал. На 17:18 22.11 коммит `3e7cbe81` не отправлен на сервер (`new_version` опережает `origin/new_version` на один коммит), чужой коммит я не пушил.

Присваиваний устаревшего `ApiKey` в MASAgents и MASMarketplace нет: оба места (`MASAgents/src/Tools/WebSearch/TavilyWebProvider.cs`, `Marketplace.Agents.AiFramework/AiFrameworkToolFactory.cs`) создают `new TavilyClient(ключ)` и аргументы Tavily не собирают. В сборке `MASAgents.slnx` и `Marketplace.slnx` (Release) ошибок нет, предупреждений CS0618 нет ни одного.

Соседи на диске при прогоне: AIFramework3Open `4744852` (закреплен), FAIRouter `c8586f0` (закреплен), MASAgents `0f7c47b` (закреплен `26ca937`, разница — только два коммита документов и отчета об удалении Elsa), KnowledgeGraph `32bc069` (закреплен `6eb6fb7`; разница — исправление взаимоблокировки OwnerStore, пропуски тестов и отчет). Незакоммиченных правок у соседей не было.

### Прогон против базы падений

База — раздел 5 отчета MASAgents `docs/reports/2026-11-22-elsa-removal.md` (36 падений). Те же проекты, Release.

| Проект | База: всего / упало | Сейчас: всего / упало |
|---|---|---|
| MASAgents `ArchitectureTests` | 36 / 0 | 36 / 0 |
| MASAgents `Executers.Tests` | 1252 / 2 | 1252 / 2 |
| `Shared.Tests` | 795 / 11 | 795 / 11 |
| `Mas.Core.Tests` | 1852 / 5 | 1852 / 5 |
| `Mas.Web.Tests` | 1182 / 18 | 1182 / 18 |
| `Marketplace.Tests` | 315 / 0, 1 пропуск | 315 / 0, 1 пропуск |
| `Marketplace.Web.Tests` | 122 / 0 | 122 / 0 |
| `Identity.Web.Tests` | 217 / 0 | 217 / 0 |
| `FlowEditor.Tests` | 29 / 0 | 29 / 0 |
| `PM.Tests` | 14 / 0 | 14 / 0 |
| `Academy.Core.Tests` | 38 / 0 | 38 / 0 |
| `MASMp.HLBackend.Tests` | 136 / 0 | 136 / 0 |

Упавшие поименно равны базе:

- Executers: `ExecutorSpecInventoryTests.Каждый_агент_каталога_описан_целиком`, `RunCheckpointTests.Question_on_the_third_step_resumes_from_the_fourth`.
- Shared: семь случаев `DocsReferenceTests.Doc_references_only_existing_paths_and_names`, `FileLengthRatchetTests.Source_files_do_not_grow_past_the_baseline`, `LlmCoreGuardTests.Долг_обходов_ядра_вычеркивается_по_мере_исправления`, `PromptTranslationTests.Every_prompt_has_both_translations_or_a_reason`, `PromptTranslationTests.Translations_are_clean`.
- Mas.Core: `AgentCatalogTests.Descriptions_are_complete`, `LayeringTests.Прикладной_слой_не_знает_об_инфраструктуре`, `ReactStepSynthesisTests.Chat_answer_carries_the_depth_contract_and_the_provider_ceiling`, `StepAndPlanReviewTests.A_long_enough_answer_is_accepted_without_the_model`, `StepReviewerTests.Long_text_is_never_reviewed`.
- Mas.Web: 13 `DevApiHostTests` и 5 `DevShareTests`.

Новых падений нет. Фронты (`npm test`) не гонялись: правка касается только C#-клиента Tavily.

## Развилки

1. **`SearchArgs.ApiKey` и `ExtractArgs.ApiKey` в публичном API.** Решение владельца: публичный API не ломаем. Удаление отложено до мажорной версии, свойство помечено `[Obsolete]` и `[JsonIgnore]`: оно сохранено ради совместимости, но игнорируется и в тело запроса не попадает. `JsonPropertyName("api_key")` снят, поэтому храповик остается прежним. Снимок API показывает свойства с обоими атрибутами.
2. **Отправка закрепа.** Коммит `3e7cbe81` в MASMarketplace создан параллельной сессией и пока не отправлен. Отправит его та сессия или нужно отправить отсюда — решение владельца.
3. **Возврат к телу.** Если Tavily (Nebius) когда-нибудь перестанет принимать Bearer, что противоречит спецификации, менять придется одну строку в конструкторе.
