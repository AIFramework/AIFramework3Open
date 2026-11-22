# Tavily: ключ в заголовке Bearer

Дата: 22.11.2026. Основа: разведка `AIRefactoring/разведка/2026-11-22-api.md`, раздел 1.5 (строка Tavily) и развилка 7.

Коммит: «Tavily: ключ заголовком Authorization: Bearer вместо api_key в теле» (этот отчет входит в него же). MASAgents, MASMarketplace и `Neighbors.props` не тронуты, закрепления версии нет.

## Что изменено

- `src/AI.LLM/Clients/Tavily/TavilyClient.cs`. Ключ ставится один раз в конструкторе: `HttpClient.DefaultRequestHeaders.Authorization = Bearer <ключ>`. Поле `_apiKey` удалено, ключ в объекте больше не хранится. Запросы `/search` и `/extract` ключа не передают. Добавлен внутренний конструктор `TavilyClient(string apiKey, HttpMessageHandler handler)` для подставного обработчика; публичный `TavilyClient(string apiKey, WebProxy proxy = null)` вызывает его и ведет себя как раньше.
- `Models/SearchArgs.cs`, `Models/ExtractArgs.cs`. Удалены свойства `ApiKey` (`api_key`).
- `Tests/unit/PublicApi.Tests/Snapshots/AI.LLM.txt`. Из снимка убраны эти два свойства (4 строки удалений, добавлений нет).
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
| `SearchResult_KeepsContentFragments` | разбор `content` с «[...]» и квадратными скобками в тексте не теряет фрагментов |
| `Search_KeepsContentFragments_ThroughClient` | то же через `SearchAsync` с подставным обработчиком |
| `TavilyClientSources_DoNotMentionApiKeyField` | храповик: в `src/AI.LLM/Clients/Tavily/**/*.cs` нет строки `api_key` |

Локально (Release): `AI.LLM.UnitTests` 196 из 196, `PublicApi.Tests` 53 из 53. Новых предупреждений в файлах Tavily нет.

## Развилки

1. **Удаление `SearchArgs.ApiKey` и `ExtractArgs.ApiKey` из публичного API.** Это ломающее изменение для того, кто сам собирал эти DTO. По MASAgents, MASMarketplace и AIFramework3Open их никто, кроме `TavilyClient`, не использует. Оставлять свойство с `[Obsolete]` нельзя без строки `api_key` (храповик), а без нее оно не имеет смысла. Решено удалить; при выпуске пакета отметить в заметках.
2. **Закрепление новой версии в `Neighbors.props` MASMarketplace.** Не сделано по условию, отдельный шаг после срочной сессии.
3. **Возврат к телу.** Если Tavily (Nebius) когда-нибудь перестанет принимать Bearer, что противоречит спецификации, менять придется одну строку в конструкторе.
