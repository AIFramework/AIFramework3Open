# Tavily: ключ в заголовке Bearer

Дата: 22.11.2026. Основа: разведка `AIRefactoring/разведка/2026-11-22-api.md`, раздел 1.5 (строка Tavily) и развилка 7.

Коммит: «Tavily: ключ заголовком Authorization: Bearer вместо api_key в теле» (`5f6d7b7`) и поправка «Tavily: ApiKey возвращен с Obsolete, публичный API не ломаем» (с ней обновлен этот отчет). MASAgents, MASMarketplace и `Neighbors.props` не тронуты, закрепления версии нет.

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

## Развилки

1. **`SearchArgs.ApiKey` и `ExtractArgs.ApiKey` в публичном API.** Решение владельца: публичный API не ломаем. Удаление отложено до мажорной версии, свойство помечено `[Obsolete]` и `[JsonIgnore]`: оно сохранено ради совместимости, но игнорируется и в тело запроса не попадает. `JsonPropertyName("api_key")` снят, поэтому храповик остается прежним. Снимок API показывает свойства с обоими атрибутами.
2. **Закрепление новой версии в `Neighbors.props` MASMarketplace.** Не сделано по условию, отдельный шаг после срочной сессии.
3. **Возврат к телу.** Если Tavily (Nebius) когда-нибудь перестанет принимать Bearer, что противоречит спецификации, менять придется одну строку в конструкторе.
