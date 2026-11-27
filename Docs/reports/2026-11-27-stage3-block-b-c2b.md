# Этап 3, блок Б, сессия С2б: механика вызова модели

Дата: 27.11.2026. Репозиторий AIFramework3Open, ветка main. MASAgents и MASMarketplace не трогались: их код только читался, чтобы общий механизм AIF подходил для перехода в С3. Секреты из настроек не читались и не переносились, в коде и тестах только синтетические ключи (`key-test-…`, `tvly-test-…`) и имена моделей (`model-primary`, `model-backup`).

## Шаг 0: проверка, не сделано ли уже

После коммита `8f453ed` в истории AIF (локально и на `origin/main`) коммитов не было, отчета С2б в `Docs/reports` не было. Отчет С2 (`2026-11-23-stage3-block-b-c2.md`) закрывал коды исхода, гейт, порт и бюджет, но ни один из семи механизмов этой сессии. Следовательно, работа шла по всему списку.

## Коммиты

| Коммит | Что |
|---|---|
| `33478e2` | храповик `CallMechanicsRatchetTests` с порогами по текущему счету, отдельным коммитом до сведения |
| `3017a1d` | сведение семи механизмов, тесты, пороги храповика опущены, снимки API, README Interop |
| этот отчет | `Docs/reports/2026-11-27-stage3-block-b-c2b.md` |

## База падений

До сведения (коммит `8f453ed`, все 12 проектов из `build-and-test.yml`, Release): 4526 тестов, падений нет.

После сведения (те же 12 проектов): 4587 тестов, падений нет. Прибавилось 61: Interop 314 (было 275), AI.LLM 225 (было 203).

Пока шло сведение, падали только ожидаемые тесты, поименно:

- `CallMechanicsRatchetTests.Threshold_FollowsTheCount` для восьми механизмов: храповик требовал опустить пороги, они опущены;
- `StepValidatorTests.LlmStepValidator_IsSuccessAsync_AcceptsStepWhenCheckItselfFails`: тест закреплял старое поведение «приемка сломалась, шаг принят», которое прямо запрещает пункт 6. Он заменен тестом `LlmStepValidator_IsSuccessAsync_ModelFailureIsNotAVerdict`.

## По механизмам

Храповик один: `Tests/unit/AI.Agents.Interop.Tests/CallMechanicsRatchetTests.cs`. У каждого механизма есть признак (текст, без которого копию не написать), порог и «дом», то есть файл общей реализации, который в счет не входит. Порог проверяется в обе стороны: число выросло, значит появилась копия; число упало, значит порог надо опустить. Отдельный тест `Home_HoldsTheMechanism` следит, чтобы признак находил и сам дом, иначе счет проверял бы пустоту.

### 1. Ключи провайдера

Где жил: каждый клиент ставил заголовок `Authorization: Bearer` своим кодом и держал один ключ на всю жизнь объекта. Мест было 7: `ProxyHTTPClient`, `WithoutProxyClient` (двумя способами сразу), `TavilyClient`, `FimClient`, `ReplicateClient`, `OpenRouterEmbedder`, `OpenRouterReranker`. Ротации не было нигде.

Стало: 0 мест вне дома. Дом — `AI.LLM/Infrastructure/Resilience/ApiKeyPool.cs`.

- `CooldownPool<T>` — пул взаимозаменяемых ресурсов с карантином, одно правило ротации для ключей и для прокси: отказавший элемент выводится на срок, запрос берет другой, по истечении срока элемент возвращается сам с чистым счетом. Если в карантине все, `Lease` отдает ближайший к возвращению, а `TryLeaseAvailable` честно отвечает «нет».
- `ApiKeyPool` поверх него: причина отказа по коду (`401/403` неверный ключ, `402` нет средств, `429` лимит), срок по причине (`KeyCooldowns`: минута, полчаса, шесть часов), для лимита срок перебивает `Retry-After`. Снимок здоровья несет только маску (последние четыре знака). Заголовок собирается в одном месте, `ApiKeyPool.Bearer`.
- Ротацию делает `ProviderHttpClient` внутри одного вызова: отказ по ключу берет следующий доступный ключ, каждый ключ пробуется не больше раза; код, который ключ не винит (5xx, 400), уходит вызывающему ответом.

Храповик: `keys`, признак `"Bearer`, порог 0.

### 2. Запасные модели

Где жил: нигде. В AIF не было ни таблицы ролей, ни цепочки запасных; `ModelPort.Fixed` ставит одну модель на все роли. Мест 0, стало 1.

Стало: `ModelPort.Routed(chains)` в Interop. Роль знает основную модель и запасные (`ModelSlot(model, port)`), переход по цепочке решает одно правило для всех ролей, `ModelFallback.Cures(LlmFailure)`:

- переход: лимит частоты, отказ провайдера, нет доступа, сеть, модель замолчала, ответ не того вида, прочий сбой;
- не переход: переполнение окна (менять надо запрос), отмена не вызывающим, ответ без сбоя, в том числе пустой;
- незнакомый код сбоя — `ArgumentOutOfRangeException`, а не догадка; тест перебирает все значения `LlmFailure`, так что новое значение без решения уронит его.

Итог говорит, какая модель ответила (`LlmAnswer.Model`, если порт модели ее не назвал, берется имя из цепочки), расход считается по всем попыткам. В потоке следующая модель берется, только если прежняя не отдала ни одного кадра. Незнакомая роль — `ArgumentException`, модели по умолчанию нет.

Храповик для механизма с нуля счетом копий не выражается; его держат тесты `RoutedModelPortTests` (правило перехода, перебор всех кодов, поток, незнакомая роль).

### 3. Повторы

Где жил: семь своих циклов попыток с паузой (`ChatLLMApi`, поиск и извлечение `TavilyClient`, `BaseInfinityEmbedder`, `OpenRouterEmbedder`, `InfinityReranker`, `OpenRouterReranker`, `Qwen3VLLMReranker`), четыре своих правила «временный ли статус» (`LlmRequestException`, `ProxyHTTPClient`, эмбеддер и реранкер OpenRouter), три своих разбора `Retry-After`. Правила расходились: Infinity и vLLM повторяли и ответ 400, эмбеддер OpenRouter ждал `Retry-After` любой длины, Tavily повторял неверный ключ и удачный ответ с частью неизвлеченных адресов.

Стало: 0 мест вне дома. Дом — `AI.LLM/Infrastructure/Resilience/RetryPolicy.cs`.

- Повтор только временного сбоя: коды 408, 425, 429, 5xx и сеть без ответа у безопасного вызова.
- Пауза по `Retry-After`, если сервер ее дал, иначе нарастающая (1 с, 2 с, …) с пределом 30 с; `Retry-After` длиннее минуты не ждется, сбой отдается сразу.
- Вызывающий обязан сказать, безопасен ли повтор (`RepeatSafety.Safe` или `Unsafe`; `Unspecified` отвергается). Необратимый вызов не повторяется никогда; сеть без ответа у него классифицируется как «результат неизвестен» (`FailureKind.Unknown`), а такой результат не повторяется.
- Срок `Retry-After` исключение несет само: `LlmRequestException.RetryAfter` или пометка `RetryPolicy.WithRetryAfter` (так клиенты сохранили прежний тип исключения `HttpRequestException`, на который опираются вызывающие и тесты).
- Отказ по ключу и смена маршрута — не повтор, а ротация: попытки правила на них не тратятся.

Числа попыток сохранены: `ChatLLMApi` 3, эмбеддер и реранкер OpenRouter `MaxRetries + 1`, поиск Tavily 2, извлечение 4, Infinity и vLLM 2. `ReplicateClient` повторов не получил намеренно: создание предсказания необратимо.

Храповики: `retry-loops` (цикл с `attempt` и `Task.Delay`), `retry-after` (разбор `.Delta is TimeSpan`), `transient-status` (своя проверка 429), все с порогом 0.

### 4. Выбор модели по роли

Где жил: в слое агентов AIF выбора модели по имени нет. Движки `react` и `plan` уже берут модель через порт по роли (`input.Model.AsLlmClient(ModelRole.Decide)`), имени модели не знают. Мест 0, стало 0.

Новое: таблица «роль → модели» теперь есть в одном месте, `ModelPort.Routed` (пункт 2). Отдельно отмечу то, что выбором по имени не является: десять вызовов `ILLMClient.Send*` в агентном слое AI.LLM и в AI.Script.Llm. Это долг перехода на `IModelPort`, его держит прежний храповик `ModelCallDebtTests`; модель там выбирает тот, кто создал клиент, а в движках это порт по роли. AI.LLM на `AI.Agents.*` не ссылается, поэтому эти места переводятся только через `port.AsLlmClient(role)` снаружи.

Храповик: `model-by-name`, признак `ModelName`, `new ChatLLMApi(`, `…ModelApi(` в `src/AI.Agents.Interop`, `src/AI.Script.Llm`, `src/AI.LLM/Agents`, порог 0.

### 5. HTTP-клиент провайдера и прокси

Где жил: 11 файлов создавали свой `HttpClient` или обработчик (`ProxyHTTPClient`, `WithoutProxyClient`, `TavilyClient`, `FimClient`, `ReplicateClient`, `BaseLLMServerAPI`, эмбеддеры и реранкеры Infinity, OpenRouter, vLLM). Прокси ставили на обработчик два места (`ProxyHTTPClient` со своим черным списком, `TavilyClient` с одним прокси без смены).

Стало: 0 мест вне дома. Дом — `AI.LLM/Infrastructure/Http/ProviderHttpClient.cs`, единственный клиент отправки: пул ключей, пул прокси на том же `CooldownPool`, тайм-ауты (соединение, заголовки ответа, весь запрос), постоянные заголовки, готовый клиент или обработчик для тестов и своего транспорта. Смена прокси при сетевом сбое следует тому же правилу, что ключи; ответ сервера прокси больше не винит. Шаг «POST JSON, неудача — исключение с кодом и сроком повтора» тоже один: `PostJsonAsync` и `EnsureSuccessAsync`.

`ProxyHTTPClient` и `WithoutProxyClient` стали адаптерами над ним с прежним публичным API и прежним поведением, кроме одного пункта (см. «Изменения поведения»).

Храповики: `http-clients` и `proxy-handlers` (`UseProxy = `), порог 0.

### 6. Сбой хоста модели

Где жил: три места выдавали сбой модели за другое. Политики ReAct (`NativeToolCallPolicy`, `StructuredJsonPolicy`) превращали падение клиента в «неверный формат»: модель получала подсказки формата, ход кончался `PolicyFailure` без причины. `LlmStepValidator` при сбое модели принимал шаг. Движки отдавали сбой модели кодом `engine_failure` с текстом, то есть отказ ключа выглядел поломкой движка.

Стало:

- в AI.LLM сбой хоста модели — всегда `LlmRequestException`; новая фабрика `LlmRequestException.FromClient(ex)` для падения клиента, которое не ответ сервера и не сеть (причина во внутреннем исключении, повтор не предлагается). Пустой ответ модели по-прежнему ведет к подсказке и новому шагу;
- `ReActResult.Failure` несет исключение сбоя, `ReActResult.ModelFailed` отличает сбой модели от поломки цикла;
- в контрактах новый код `StopCode.ModelFailure` (`model_failure`); итог движка собирает одно место, `ReActRun.ModelFailed`: статус `Failed`, код `model_failure`, в `Unfinished` код `LlmFailure` из `ModelTranslation.FailureOf` и причина. Так кончаются и `react`, и `plan` (включая сбой приемки шага).

Храповик: `model-failure-as-success` (ветка `catch (Exception …)` с `Malformed(null)` или `return true;`) в `src/AI.LLM/Agents` и `src/AI.Agents.Interop`, было 3, порог 0.

### 7. Безопасность повтора доходит до гейта

Где жил: публичный `source.AsToolSet(run, actions, log)` принимал только виды действий, а повторяемость задавал лишь закрытый путь `AttributedTools` вторым словарем. Мест с раздельными словарями 2 (`AttributedTools`, `ReActSourceAsToolSet`).

Стало: перегрузка `source.AsToolSet(specs, run, log)` берет из описаний инструментов (`ToolSpec`) и вид действия, и повторяемость одной записью; набор отдает их в `Available`, и гейт решает повтор по ним. Не объявленная повторяемость и инструмент без описания дают `Unknown`, а не «можно повторять». Внутри обе оси едут одной закрытой записью `ToolEffect`; старый вход остался и работает как раньше.

Храповик: `split-effects` (`Dictionary<string, ToolIdempotency>`), было 2, порог 0.

## Изменения публичного API

Удалений нет. Снимки: `AI.Agents.Contracts.txt` +1 строка, `AI.Agents.Interop.txt` +13, `AI.LLM.txt` +175 (одна строка сменилась: имя автомата состояний компилятора у `ChatLLMApi.SendWithContextStreamAsync`).

| Сборка | Добавлено |
|---|---|
| AI.Agents.Contracts | `StopCode.ModelFailure` |
| AI.Agents.Interop | `ModelPort.Routed`, `ModelSlot`, `ModelFallback`, перегрузка `ReActToolExtensions.AsToolSet(source, specs, run, log)` |
| AI.LLM | пространство `AI.LLM.Infrastructure.Resilience`: `CooldownPool<T>`, `PoolLease<T>`, `PoolEntryHealth`, `ApiKeyPool`, `KeyFailure`, `KeyCooldowns`, `KeyHealth`, `RetryPolicy`, `RepeatSafety`, `FailureKind`; `ProviderHttpClient`, `ProviderHttpClientOptions`, `ProxyList`; `ChatLLMApi.Retry`; `LlmRequestException.FromClient`; `ReActResult.Failure`, `ReActResult.ModelFailed`; конструкторы `OpenRouterEmbedder(ProviderHttpClient, …)`, `OpenRouterReranker(ProviderHttpClient, …)`, `TavilyClient(ApiKeyPool, proxy)`; свойства `Retry` у эмбеддеров и реранкеров, `SearchRetry` и `ExtractRetry` у Tavily |

Помечены `[Obsolete]`, работают через адаптер:

- `ProxyHTTPClient` целиком: адаптер над `ProviderHttpClient` (8 сетевых сбоев подряд, карантин 24 часа, все прокси в карантине — `InvalidOperationException`, неудачный ответ — `HttpRequestException` с кодом); статические `LoadProxiesFromJson` и `GetWebProxy` ведут на `ProxyList`;
- `WithoutProxyClient` целиком: адаптер с прямым маршрутом.

`CS0618` в репозитории не ошибка, сборка и примеры документации собираются. Соседи увидят пометки только при обновлении закрепа.

Новые конструкторы с `ProviderHttpClient` или `ApiKeyPool` рядом с прежними со строкой ключа делают неоднозначным только вызов с литералом `null` первым аргументом (`new TavilyClient(null)`); прежде он все равно падал на проверке ключа.

## Изменения поведения

Каждое закреплено тестом, который падает на старом коде. Шесть таких тестов, переписанных на старый API, прогнаны во временной копии репозитория на коммите `33478e2`: все шесть упали, как и ожидалось.

| Было | Стало | Тест |
|---|---|---|
| код ответа, пришедший исключением от клиента отправки, терялся; запрос шел «нет связи» и повторялся три раза, даже при 401 | код сохраняется, 401 не повторяется | `RetryRuleTests.ChatLLMApi_StatusFromSenderException_KeptAndNotRetried` |
| эмбеддер OpenRouter ждал `Retry-After` любой длины | дольше минуты не ждет, сбой сразу | `OpenRouterEmbedder_LongRetryAfter_FailsAtOnceInsteadOfWaiting` |
| Tavily повторял неверный ключ | не повторяет | `Tavily_RejectedKey_NotRetried` |
| Tavily повторял извлечение четыре раза, если хоть один адрес не извлекся | удачный ответ с `FailedResults` возвращается сразу: это итог по существу, а не сбой сервера | `Tavily_PartialExtract_ReturnedWithoutRetry` |
| Infinity и vLLM повторяли и ответ 400 | только временный сбой | правило `RetryPolicy`, `RunAsync_FatalNotRetried` |
| `ProxyHTTPClient` винил прокси за ответ 429 и 5xx и уводил запрос на другой прокси | маршрут меняет только сбой сети | `ProviderHttpClient_ServerError_DoesNotBlameKey` (то же правило для ключа), устройство `ProviderHttpClient` |
| падение клиента модели в политике ReAct считалось неверным форматом | сбой модели с причиной, `ReActResult.ModelFailed` | `ModelHostFailureTests.ReActEngine_ClientCrash_IsModelFailureWithCause` |
| сбой модели в приемке шага принимал шаг | `LlmRequestException`, итог плана `model_failure` | `StepValidatorTests.LlmStepValidator_IsSuccessAsync_ModelFailureIsNotAVerdict` |
| сбой модели в движке давал `engine_failure` | `model_failure` и код `LlmFailure` в `Unfinished` | `ToolEffectInputTests.ReactEngine_ModelFailure_HasOneCodeAndKeepsCause` |

## Проверка

- `AIFramework.Full.sln -c Release` и `Tests/AI.ML.Integration`: 0 ошибок, новых предупреждений в правленых файлах нет.
- Все 12 тестовых проектов из `build-and-test.yml` локально в Release: 4587 тестов, 0 падений.
- `Tools/DocsLint --strict`: ошибок 0, новых предупреждений 0; примеры документации (184) собираются.
- Файлы до 500 строк: самый длинный из правленых `ReActRun.cs`, 381 строка.
- CI GitHub на коммите `3017a1d`: Build and test (прогон 45931321841) и Docs lint (45931321819) зеленые.

## Что нужно сделать в MASAgents после закрепа (С3)

1. **Ключи.** `OpenRouterKeyPool` и `TavilyKeyPool` перевести на `ApiKeyPool` (или на `CooldownPool<string>`, если нужна своя стратегия выбора). Сроки карантина MAS (`KeyCooldownOptions` из `OpenRouter:KeyCooldown`) ложатся в `KeyCooldowns` без потерь; снимок для админ-панели — `ApiKeyPool.Snapshot()` с маской. Серверные опросы баланса по каждому ключу (`AllKeys`) в AIF намеренно не вынесены: значения ключей пул наружу не отдает, их держит хост.
2. **HTTP-клиент.** `OpenRouterWebApi` (свой `IWebAPIClient` со своим циклом повторов и своим правилом 429/5xx) заменить на `ProviderHttpClient` с `Keys` и `Proxies` и на `ChatLLMApi.Retry` для повторов. `OutboundProxyPool` сложнее нашего пула (пробы мертвого маршрута по лестнице, подозрение без снятия, привязка скрапера к хосту): для моделей его можно заменить пулом `ProviderHttpClient`, а скрапер оставить на своем пуле до отдельного решения (развилка 1).
3. **Повторы.** Свои массивы пауз (`RetryDelays`) убрать в пользу `RetryPolicy`; инструменты с побочным действием передавать как `RepeatSafety.Unsafe`.
4. **Роли и запасные модели.** `LlmRoute` и выбор модели в `ModelCatalog` свести к `ModelPort.Routed`: роль хода → основная модель и запасные. Итог `LlmModelPort` должен заполнять `LlmAnswer.Model` фактической моделью, иначе подставится имя из цепочки.
5. **Гейт.** Набор хода собирать через `source.AsToolSet(specs)` из уже имеющихся описаний инструментов, а не второй спецификацией: тогда гейт с прежним журналом (`new EffectGate(set, journal.Effects)`) видит повторяемость.
6. **Сбой модели.** Итог хода с `StopCode.ModelFailure` показывать человеку как отказ модели с причиной из `Unfinished`, а не как «ошибка движка». Если у MAS есть своя проверка приемки шага поверх `LlmStepValidator`, учесть, что сбой модели теперь не принимает шаг.
7. **Закреп.** Все это доступно после обновления `Neighbors.props` на коммит С2б.

## Развилки для решения владельца

1. **Пул маршрутов MAS.** `OutboundProxyPool` умеет пробу мертвого маршрута и привязку к хосту, наш `CooldownPool` возвращает маршрут по сроку. Варианты: (а) модели на `ProviderHttpClient`, скрапер на пуле MAS; (б) перенести пробу и привязку в AIF как расширение `CooldownPool`. Оценка (б): около дня. Рекомендация: (а) сейчас, (б) при следующем касании скрапера.
2. **Частичное извлечение Tavily.** Раньше клиент сам повторял извлечение при неизвлеченных адресах. Теперь их видит вызывающий в `FailedResults`. Если повтор по адресам нужен, его место у вызывающего, по отказавшим адресам, а не по всему запросу. Подтвердить.
3. **Роли движков.** Движки спрашивают порт одной ролью `Decide`, в том числе для приемки шага. С `ModelPort.Routed` приемку можно отдать роли `Judge` (дешевая модель-судья). Это меняет, какая модель отвечает, поэтому не сделано без решения.

## Не делалось

Правки MASAgents, MASMarketplace и закреп `Neighbors.props`. Перевод десяти вызовов `ILLMClient.Send*` агентного слоя на порт (долг `ModelCallDebtTests`, отдельный шаг). Реранк и эмбеддинги через порт.
