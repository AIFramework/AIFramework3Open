# Инструменты агента (Agent Tools)

## Обзор

Система инструментов AIFramework позволяет пометить **любой метод** в любом классе атрибутом `[AgentTool]`, и он автоматически станет доступен:

1. **Агентам** — через `ToolRegistry` и ReAct-цикл
2. **MCP-клиентам** — через `McpToolBridge` (Cursor, Claude Desktop и др.)

## Атрибут `[AgentTool]`

```csharp
using AI.LLM.Agents.Tools;

public class MyTools
{
    [AgentTool("tool_name", "Описание для LLM")]
    public string MyMethod(
        [ToolParameter("Описание параметра")] string param1,
        [ToolParameter("Описание с default")] int param2 = 10)
    {
        return "результат";
    }
}
```

### Параметры атрибута

| Параметр    | Тип    | Описание                                      |
|-------------|--------|-----------------------------------------------|
| `name`      | string | Имя инструмента (snake_case). Null → имя метода |
| `description` | string | Описание для LLM                             |

### `[ToolParameter]`

| Свойство    | Тип    | По умолчанию | Описание                          |
|-------------|--------|-------------|-----------------------------------|
| `Description` | string | —          | Описание параметра для LLM       |
| `Required`  | bool   | true        | Обязательный. false для default-параметров |

## ToolRegistry

### Создание

```csharp
// Из набора экземпляров
var registry = ToolRegistry.FromObjects(new MyTools(), new DspTools(), new StatsTools());

// Через регистрацию
var registry = new ToolRegistry();
registry.Register(new MyTools());
registry.Register(new DspTools());
```

### Получение определений для LLM

```csharp
List<ToolDefinition> definitions = registry.GetDefinitions();
// Передать в GenerateSettings.Tools
```

### Выполнение

```csharp
// Один инструмент
ToolExecutionResult result = await registry.ExecuteAsync(toolCall);

// Параллельное выполнение
List<ToolExecutionResult> results = await registry.ExecuteParallelAsync(toolCalls);

// Преобразование в сообщения для LLM
List<LLMMessage> messages = ToolRegistry.ToToolMessages(results);
```

## Поддерживаемые типы параметров

| C# тип       | JSON Schema тип |
|--------------|----------------|
| `string`     | `"string"`      |
| `int`, `long`, `short`, `byte` | `"integer"` |
| `double`, `float`, `decimal` | `"number"` |
| `bool`       | `"boolean"`     |
| `T[]`, `List<T>` | `"array"` |
| Другие       | `"string"` (fallback) |

Параметр типа `CancellationToken` автоматически исключается из JSON Schema и получает `CancellationToken.None`.

## Синхронные и асинхронные методы

Поддерживаются оба варианта:

```csharp
// Синхронный
[AgentTool("sync_tool", "Синхронный инструмент")]
public string DoWork([ToolParameter("Данные")] string data)
{
    return Process(data);
}

// Асинхронный
[AgentTool("async_tool", "Асинхронный инструмент")]
public async Task<string> DoWorkAsync(
    [ToolParameter("Данные")] string data,
    CancellationToken ct = default)
{
    return await ProcessAsync(data, ct);
}
```

## Примеры: алгоритмы фреймворка как инструменты

### Статистика (AI)

```csharp
using AI.LLM.Agents.Tools;
using AI.Statistics;
using AI.DataStructs.Algebraic;

public class StatisticsTools
{
    [AgentTool("compute_statistics", "Вычисляет описательную статистику для числового ряда")]
    public string ComputeStats(
        [ToolParameter("Числа через запятую")] string numbers)
    {
        var values = numbers.Split(',').Select(double.Parse).ToArray();
        var vector = new Vector(values);
        var stat = new Statistic(vector);
        return $"μ={stat.Expected:F4}, σ={stat.STD:F4}, min={stat.MinValue:F4}, max={stat.MaxValue:F4}";
    }
}
```

### Машинное обучение (AI.ML)

```csharp
using AI.LLM.Agents.Tools;
using AI.ML.Clustering;

public class MLTools
{
    [AgentTool("cluster_kmeans", "Кластеризует данные методом K-Means")]
    public string Cluster(
        [ToolParameter("JSON-массив точек [[x,y],...]")] string dataJson,
        [ToolParameter("Число кластеров")] int k = 3)
    {
        var data = JsonSerializer.Deserialize<double[][]>(dataJson);
        // ... KMeans.Train(data, k) ...
        return JsonSerializer.Serialize(result);
    }
}
```

### Обработка сигналов (AI.DSP)

```csharp
using AI.LLM.Agents.Tools;
using AI.DSP.DSPCore;

public class DSPTools
{
    [AgentTool("compute_fft", "Вычисляет БПФ (FFT) для временного ряда")]
    public string ComputeFFT(
        [ToolParameter("Значения через запятую")] string values,
        [ToolParameter("Частота дискретизации (Гц)")] double sampleRate = 1000)
    {
        var signal = values.Split(',').Select(double.Parse).ToArray();
        // ... FFT ...
        return "спектр";
    }
}
```

### Поиск в интернете (Tavily)

```csharp
using AI.LLM.Agents.Tools.Builtin;
using AI.LLM.Clients.Tavily;

var searchTool = new TavilySearchTool(new TavilyClient("tvly-..."));
// searchTool уже содержит [AgentTool("tavily_search", ...)]
```

## ToolExecutionResult

Результат выполнения инструмента:

| Свойство     | Тип       | Описание                    |
|-------------|----------|-----------------------------|
| `ToolCallId` | string   | ID вызова                   |
| `ToolName`  | string   | Имя инструмента              |
| `Content`   | string   | Результат (текст)            |
| `IsSuccess` | bool     | Успешно ли выполнен          |
| `Elapsed`   | TimeSpan | Время выполнения             |

## JSON Schema (автоматическая генерация)

При регистрации инструмента `ToolRegistry` автоматически строит JSON Schema из параметров метода. Пример для `compute_statistics`:

```json
{
  "type": "function",
  "function": {
    "name": "compute_statistics",
    "description": "Вычисляет описательную статистику для числового ряда",
    "parameters": {
      "type": "object",
      "properties": {
        "numbers": {
          "type": "string",
          "description": "Числа через запятую"
        }
      },
      "required": ["numbers"]
    }
  }
}
```

## См. также

- [agents.md](agents.md) — агентный фреймворк
- [mcp.md](mcp.md) — MCP-сервер
