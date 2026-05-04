# MCP-сервер AIFramework

## Обзор

AIFramework поддерживает **Model Context Protocol (MCP)** — открытый протокол для предоставления контекста и инструментов языковым моделям. Любой метод, помеченный `[AgentTool]`, автоматически становится доступен MCP-клиентам: **Cursor**, **Claude Desktop**, **Continue**, и любому другому MCP-совместимому приложению.

## Быстрый старт

### 1. Создание проекта

```bash
dotnet new web -n MyMcpServer
cd MyMcpServer
dotnet add reference ../path/to/AI.LLM/AI.LLM.csproj
```

### 2. Определение инструментов

```csharp
using AI.LLM.Agents.Tools;

public class StatisticsTools
{
    [AgentTool("compute_statistics", "Вычисляет описательную статистику для числового ряда")]
    public string ComputeStats(
        [ToolParameter("Числа через запятую")] string numbers)
    {
        var values = numbers.Split(',').Select(double.Parse).ToArray();
        var vector = new AI.DataStructs.Algebraic.Vector(values);
        var stat = new AI.Statistics.Statistic(vector);
        return $"μ={stat.Expected:F4}, σ={stat.STD:F4}, min={stat.MinValue:F4}, max={stat.MaxValue:F4}";
    }
}

public class DSPTools
{
    [AgentTool("compute_fft", "Вычисляет БПФ для временного ряда")]
    public string ComputeFFT(
        [ToolParameter("Значения через запятую")] string values,
        [ToolParameter("Частота дискретизации (Гц)")] double sampleRate = 1000)
    {
        // ... реализация ...
        return "результат";
    }
}
```

### 3. Запуск MCP-сервера

```csharp
using AI.LLM.Agents.MCP;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddAIFrameworkTools(new StatisticsTools(), new DSPTools());

var app = builder.Build();
app.MapMcp();
app.Run("http://localhost:5000");
```

## Подключение клиентов

### Cursor

В настройках Cursor -> MCP -> добавить сервер:

| Параметр              | Значение                         |
|----------------------|----------------------------------|
| **Type**             | `streamablehttp` (или `sse`)     |
| **URL**              | `http://localhost:5000/mcp`      |
| **Authentication**   | API Key / Bearer / None          |

### Claude Desktop

В файле `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "aiframework": {
      "url": "http://localhost:5000/mcp",
      "transport": "streamable-http"
    }
  }
}
```

## Авторизация

### API Key

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddAIFrameworkTools(new StatisticsTools());

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        var apiKey = context.Request.Headers["Authorization"].FirstOrDefault();
        if (apiKey != "Bearer my-secret-key")
        {
            context.Response.StatusCode = 401;
            return;
        }
    }
    await next();
});

app.MapMcp();
app.Run();
```

### Bearer Token

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = "https://your-auth-server.com";
    });

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp().RequireAuthorization();
app.Run();
```

## Транспорт

AIFramework MCP-сервер поддерживает два транспорта:

| Транспорт           | Описание                                        |
|--------------------|-------------------------------------------------|
| **Streamable HTTP** | Рекомендуемый. Стандартный HTTP с потоковой передачей |
| **SSE**            | Server-Sent Events. Поддерживается для обратной совместимости |

```csharp
// Streamable HTTP (по умолчанию)
builder.Services.AddMcpServer()
    .WithHttpTransport();

// SSE
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    });
```

## Архитектура

```
+---------------------------+
|    MCP-клиент             |
|  (Cursor / Claude / ...)  |
+---------┬-----------------+
          | HTTP/SSE + JSON-RPC
          v
+---------------------------+
|   ASP.NET Core Host       |
|   + ModelContextProtocol  |
|     .AspNetCore           |
+---------┬-----------------+
          | McpToolBridge
          v
+---------------------------+
|     ToolRegistry          |
|   [AgentTool] методы      |
+---------┬-----------------+
          |
    +-----┴----------+
    v                v
+--------+   +------------+
| AI.ML  |   | AI.DSP     |
| AI     |   | AI.NLP     |
| ...    |   | ...        |
+--------+   +------------+
```

## Пример: все библиотеки как MCP-инструменты

```csharp
using AI.LLM.Agents.MCP;

var builder = WebApplication.CreateBuilder(args);

// Каждый класс содержит [AgentTool] методы
var tools = new object[]
{
    new StatisticsTools(),      // AI: статистика
    new MLTools(),              // AI.ML: кластеризация, классификация
    new DSPTools(),             // AI.DSP: FFT, фильтры
    new NLPTools(),             // AI.NLP: NER, токенизация
    new NeuralNetworkTools(),   // AI.NeuralNetworks: обучение, предсказание
    new TavilySearchTool(new TavilyClient("tvly-...")),  // Поиск в интернете
};

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddAIFrameworkTools(tools);

var app = builder.Build();
app.MapMcp();
app.Run("http://localhost:5000");
```

После запуска все инструменты будут доступны в Cursor, Claude Desktop и любом MCP-клиенте.

## См. также

- [agents.md](agents.md) — агентный фреймворк
- [tools.md](tools.md) — справочник по инструментам
