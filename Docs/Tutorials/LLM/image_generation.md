# Генерация изображений и SSRF Guard

## Обзор

**`APIImageGenerator`** — высокоуровневый клиент для получения изображений от
мультимодальных LLM (DALL-E, Gemini, Stable Diffusion через API и др.).
Принимает текстовый промпт, отправляет его через `ChatLLMApi` и возвращает
изображение в виде `byte[]`.

Встроенный **`SsrfGuardOptions`** защищает от SSRF-атак (Server-Side Request
Forgery): когда модель возвращает URL изображения вместо data-URI,
сервер проверяет адрес по белому списку *прежде* чем выполнить HTTP-запрос.

## Быстрый старт

```csharp
using AI.LLM.Clients.ImageGeneration;
using AI.LLM.Clients.OpenRouter;

// LLM-клиент с поддержкой изображений (например, gpt-image-1 через OpenRouter)
var llmApi = new OpenRouterModelApi("sk-or-...", "openai/gpt-image-1");

// Создать генератор с защитой по умолчанию
var generator = new APIImageGenerator(llmApi);

var answer = await generator.GenerateAsync("Нарисуй закат над океаном");

if (answer.StatusOK)
    File.WriteAllBytes("sunset.png", answer.ImageData);
else
    Console.WriteLine($"Ошибка: {answer.Text}");
```

## SsrfGuardOptions — защита от SSRF

### Что такое SSRF

**Server-Side Request Forgery** — атака, при которой злоумышленник через
вредоносный промпт заставляет LLM вернуть произвольный URL в качестве
«изображения». Сервер затем сам выполняет запрос к этому адресу — из своей
сети, от своего имени. Цели атаки:

- `http://169.254.169.254/` — cloud metadata (AWS/Azure/GCP, хранит ключи доступа)
- `http://192.168.x.x/admin` — административные панели внутренней сети
- `http://localhost:8080/` — сервисы, недоступные снаружи

`SsrfGuardOptions` перехватывает URL до HTTP-запроса и проверяет его по
настроенной политике.

### Предустановленные конфигурации

```csharp
// Блокирует приватные IP, хост не ограничен (по умолчанию)
var generator = new APIImageGenerator(llmApi);
var generator = new APIImageGenerator(llmApi, SsrfGuardOptions.Default);

// Строгий allowlist — только хосты OpenAI DALL-E
var generator = new APIImageGenerator(llmApi, SsrfGuardOptions.OpenAiOnly);

// Произвольный список хостов
var generator = new APIImageGenerator(llmApi,
    SsrfGuardOptions.WithHosts("cdn.mycompany.com", "images.example.com"));

// Отключить проверку (только для изолированных локальных сред)
var generator = new APIImageGenerator(llmApi, SsrfGuardOptions.Disabled);
```

### Полная конфигурация

```csharp
var guard = new SsrfGuardOptions
{
    // Включить/выключить всю защиту
    Enabled = true,

    // Список допустимых хостов. Пустой список = хост не ограничен,
    // но BlockPrivateRanges всё равно применяется.
    AllowedHosts = [
        "oaidalleapiprodscus.blob.core.windows.net",
        "cdn.openai.com",
    ],

    // Блокировать loopback (127.x), RFC-1918 (10.x, 192.168.x, 172.16-31.x)
    // и link-local (169.254.x). По умолчанию: true.
    BlockPrivateRanges = true,

    // Допустимые схемы URI
    AllowedSchemes = ["https"],   // только HTTPS
};

var generator = new APIImageGenerator(llmApi, guard);
```

### Что блокируется

| Адрес / диапазон | Причина блокировки |
|---|---|
| `127.0.0.1`, `localhost` | Loopback — сервисы на той же машине |
| `10.x.x.x` | RFC-1918 — приватная сеть |
| `172.16–31.x.x` | RFC-1918 — приватная сеть |
| `192.168.x.x` | RFC-1918 — локальная сеть |
| `169.254.x.x` | Link-local — cloud metadata endpoint |
| `file://`, `ftp://` и др. | Неразрешённые схемы |
| Любой хост вне `AllowedHosts` | Нарушение allowlist (если список задан) |

При нарушении политики выбрасывается `SecurityException` с подробным сообщением,
генератор возвращает `ImageGenerationAnswer { StatusOK = false }`.

### Логика работы

```
GenerateAsync(prompt)
    └─ ChatLLMApi.SendWithContextAsync(...)   // запрос к LLM
          └─ ответ содержит URL или base64
                ├─ base64 data URI → Convert.FromBase64String()  (без запросов)
                └─ обычный URL
                      ├─ SsrfGuardOptions.Validate(url)          ← проверка здесь
                      │     ├─ схема OK?
                      │     ├─ хост в AllowedHosts?
                      │     └─ адрес не приватный?
                      └─ HttpClient.GetByteArrayAsync(url)        (только если OK)
```

## Параметры ответа

```csharp
public class ImageGenerationAnswer
{
    bool    StatusOK;   // true — успех
    byte[]  ImageData;  // байты изображения (PNG/JPEG/WebP)
    string  Text;       // сопроводительный текст или описание ошибки
    decimal? Cost;      // стоимость запроса в USD (если API вернул)
}
```

## Рекомендации по безопасности

1. **Используйте `AllowedHosts`** для ограничения хостов изображений конкретным
   провайдером (например, `SsrfGuardOptions.OpenAiOnly`).
2. **Не отключайте** `BlockPrivateRanges` в production — это минимальная защита.
3. **Разрешайте только HTTPS** через `AllowedSchemes = ["https"]`, чтобы исключить
   перехват изображений по незашифрованному каналу.
4. **`SsrfGuardOptions.Disabled`** допустим только в полностью изолированных
   локальных средах разработки без выхода в сеть.
