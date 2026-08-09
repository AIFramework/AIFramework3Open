![Stars](https://img.shields.io/github/stars/AIFramework/AIFramework3Open?style=flat-square)
![Forks](https://img.shields.io/github/forks/AIFramework/AIFramework3Open?style=flat-square)
![Watchers](https://img.shields.io/github/watchers/AIFramework/AIFramework3Open?style=flat-square)
![License](https://img.shields.io/badge/license-Apache%202.0-blue?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-9.0-blueviolet?style=flat-square)
[![Docs lint](https://img.shields.io/github/actions/workflow/status/AIFramework/AIFramework3Open/docs-lint.yml?branch=main&label=docs%20lint&style=flat-square)](https://github.com/AIFramework/AIFramework3Open/actions/workflows/docs-lint.yml)

<img src="https://github.com/AIFramework/AIFramework3Open/blob/main/Docs/img/ai3_0_logo.png?raw=true" width="450" alt="AIFramework 3.0 Open" />

## О проекте

**AIFramework 3.0 Open** — универсальный open-source SDK на C# / .NET 9 для создания AI-агентов и интеллектуальных систем. Включает **27 библиотек**, покрывающих весь стек: от LLM-интеграции и автономных агентов до нейросетей, DSP, NLP, компьютерного зрения и 220+ алгоритмов — с единым Blazor-демонстратором.

### Содержание

- [Ключевые возможности](#ключевые-возможности)
- [Быстрый старт](#быстрый-старт)
- [AI-агенты](#ai-агенты) · [MCP-сервер](#mcp-сервер) · [Semantic Kernel](#semantic-kernel)
- [Примеры кода](#примеры-кода)
- [Интерактивный демонстратор](#интерактивный-демонстратор)
- [Архитектура сборок](#архитектура-сборок) · [Модули](#модули-27-библиотек)
- [Структура репозитория](#структура-репозитория) · [Решения (.sln)](#решения-sln) · [Тесты](#тесты)
- [Стандарт кода](#стандарт-кода) · [Документация](#документация) · [Лицензия](#лицензия-и-атрибуция)

### Ключевые возможности

- **AI-агенты** — автономный ReAct-цикл (`AgentBuilder`), native function calling + prompt fallback для моделей без FC, память (скользящее окно, векторная, суммаризация), гарды безопасности, детальный биллинг (LLM + инструменты). Мультимодальный цикл **Observe-Reason-Act**.
- **LLM-интеграция** — OpenAI, OpenRouter, DeepSeek, Google AI Studio, Perplexity; потоковая генерация, мультимодальность, reasoning-модели, биллинг, proxy-ротация.
- **MCP-сервер** — атрибут `[AgentTool]` превращает любой метод в инструмент для Cursor, Claude Desktop и других MCP-клиентов через `McpToolBridge`.
- **Semantic Kernel** — `IChatCompletionService`-обёртка поверх `ILLMClient`, сохраняющая биллинг; инструменты как `KernelPlugin`.
- **Машинное обучение** — нейросети (MLP, RNN, CNN), классификаторы (kNN, SVM, Байес), кластеризация (K-Means, FOREL, SOM), регрессия, PCA, генетические алгоритмы.
- **Глубокое обучение (V2 Tensor Engine)** — autograd в стиле PyTorch, слои `Linear`/`Conv1d`/`Conv2d`/`Rnn`/`Lstm`/`Gru`/`Attention`, нормализации, `DataLoader`, GPU через ILGPU/CUDA.
- **Алгоритмы на графах** — BFS/DFS, Dijkstra, A\*, MST, максимальный поток, паросочетания, MAPF (CBS/ECBS/PBS, PIBT, LaCAM), VRP/TSP.
- **Компьютерное зрение** — 2D FFT (CPU/cuFFT GPU), Sobel, HOG, эквализация, цветовая обработка.
- **DSP и радиотехника** — фильтры (IIR/FIR), FFT, спектральный анализ (Уэлч), DSP-конвейеры; генераторы, модуляция/демодуляция и АРУ в `AI.SignalLabs`.
- **NLP** — стеммер, лемматизация, BoW, TF-IDF, BPE/Sentence-токенизация, NER, суммаризация.
- **Символьная математика** — парсер выражений, CAS-упрощение, решатели и численные методы (`AI.Solvers.Math`).
- **Геометрия** — преобразования (аффинные, гомография, кватернионы), RANSAC, Безье/Эрмит, SVD/LU/Холецкий.
- **Системы управления** — PID, LQR, LQG, KF/EKF, MPC, скользящий режим, MRAC, RLS.
- **Высокая производительность** — OpenBLAS, ILGPU/CUDA, cuFFT, `Parallel.For`.
- **Интерактивный демонстратор** — Blazor Server с LaTeX (KaTeX), Plotly-графиками и визуализацией всех модулей.

---

## Быстрый старт

### Требования

- **.NET SDK 9.0** или новее (все проекты нацелены на `net9.0`).
- **Windows** — для полной сборки решения: `AI.Charts.WinForms` нацелен на `net9.0-windows` и требует WinForms. На Linux/macOS собирайте `AIFramework-Core.sln` или отдельные проекты.
- **Опционально:** CUDA-совместимая видеокарта для `AI.NeuralNetworks.Gpu` и cuFFT-путей `AI.ComputerVision`.

### Сборка и тесты

```bash
git clone https://github.com/AIFramework/AIFramework3Open.git
cd AIFramework3Open
dotnet restore AIFramework.sln
dotnet build  AIFramework.sln -c Release
dotnet test   AIFramework.sln -c Release --no-build
```

**Запуск интерактивного демонстратора:**

```bash
cd Demo/WebUI/AiFrameworkDemo/AiFrameworkDemo
dotnet run -c Release
# Откройте https://localhost:7280 (или http://localhost:5170) в браузере
```

### Подключение к своему проекту

Публичных пакетов на nuget.org пока нет — подключайте проекты напрямую или собирайте пакеты локально:

```bash
# вариант 1: ссылка на проект
dotnet add MyApp.csproj reference path/to/AIFramework3Open/src/AI.LLM/AI.LLM.csproj

# вариант 2: локальный NuGet-пакет
dotnet pack src/AI/AI.csproj -c Release -o ./artifacts
```

Версия и метаданные пакетов задаются в [Directory.Build.props](Directory.Build.props); для всех проектов из `src/` включён `IsPackable`, `PackageId` совпадает с именем сборки.

---

## AI-агенты

Создание автономного агента с инструментами за 10 строк:

```csharp
using AI.LLM.Agents;
using AI.LLM.Agents.Tools;
using AI.LLM.Clients.OpenRouter;
using AI.LLM.Services.LLM;

var llm = new LLMBase(new OpenRouterModelApi("sk-...", "anthropic/claude-sonnet-4"));

var agent = AgentBuilder.Create()
    .WithLLM(llm)
    .WithSystemPrompt("Ты аналитик данных. Используй инструменты для вычислений.")
    .WithTools(new MyTools())
    .WithMaxIterations(5)
    .Build();

var result = await agent.RunAsync("Вычисли среднее для чисел: 2, 5, 8, 11, 14");
Console.WriteLine(result.Answer);
Console.WriteLine(result.Usage);   // токены, стоимость, время инструментов
```

Инструменты объявляются атрибутами на любом классе:

```csharp
public class MyTools
{
    [AgentTool("compute_statistics", "Описательная статистика числового ряда")]
    public string Stats([ToolParameter("Числа через запятую")] string numbers)
    {
        var values = numbers.Split(',').Select(double.Parse).ToArray();
        var v = new AI.DataStructs.Algebraic.Vector(values);
        var stat = new AI.Statistics.Statistic(v);
        return $"n={values.Length}, μ={stat.Expected:F4}, σ={stat.STD:F4}";
    }
}
```

Дополнительно `AgentBuilder` умеет: `WithMemory(...)` — память диалога, `WithGuard(...)` — проверки безопасности,
`WithObserver(...)` — мультимодальные наблюдения (скриншот/камера), `WithPromptFallback()` — работа с моделями без
native function calling, `WithTemperature/WithMaxTokens` — параметры генерации.

### Архитектура агента

```mermaid
flowchart LR
  User["AgentQuery<br/>(текст + изображения)"] --> Agent
  Agent --> LLM["ILLMClient"]
  Agent --> Tools["ToolRegistry"]
  Agent --> Memory["IAgentMemory"]
  Agent --> Guards["IAgentGuard"]
  Agent <--> Observer["IObservationProvider<br/>(скриншот / камера)"]
  LLM --> Providers["OpenRouter / OpenAI / DeepSeek / Google AI"]
  Tools --> Attribute["[AgentTool] методы"]
  Tools --> MCP["McpToolBridge"]
  Tools --> SK["KernelPlugin (SK)"]
```

Подробности — в [Docs/Tutorials/LLM/](Docs/Tutorials/LLM/).

---

## MCP-сервер

Любой алгоритм фреймворка доступен внешним клиентам (Cursor, Claude Desktop) как MCP-инструмент:

```csharp
var builder = WebApplication.CreateBuilder();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .AddAIFrameworkTools(new MyTools(), new SignalProcessingTools());

var app = builder.Build();
app.MapMcp();
app.Run();
```

---

## Semantic Kernel

`LLMClientChatCompletionService` — мост между SK и `ILLMClient`, сохраняющий биллинг и reasoning-настройки:

```csharp
using AI.LLM.Integration.SemanticKernel.Extensions;

var kernel = Kernel.CreateBuilder()
    .AddSharpGPTChatCompletion(llm, "anthropic/claude-sonnet-4")
    .Build();

kernel.Plugins.Add(ToolRegistry.FromObjects(new MyTools()).ToKernelPlugin());
```

---

## Примеры кода

<details>
<summary><strong>Autograd (PyTorch-like API, V2 Tensor Engine)</strong></summary>

```csharp
using AI.ML.NeuralNetworks.V2;

var a = Tensor.From(new float[] { 1, 2, 3, 4 }, new Shape(2, 2)).SetRequiresGrad();
var b = Tensor.From(new float[] { 2, 0, 0, 2 }, new Shape(2, 2)).SetRequiresGrad();

var c = a.MatMul(b) + 5.0f;
var loss = c.Sigmoid().Sum();   // Backward() без аргумента требует скаляр

loss.Backward();                // заполняет a.Grad и b.Grad
```
</details>

<details>
<summary><strong>K-Means кластеризация</strong></summary>

```csharp
using AI.ML.Clustering;
using AI.DataStructs.Algebraic;

var data = new Vector[] { /* ... */ };
var km = new KMeans(clusterCount: 3);
km.Train(data, seed: 42);
int[] labels = km.Classify(data);
```
</details>

<details>
<summary><strong>Линейная регрессия</strong></summary>

```csharp
using AI.ML.Regression;
using AI.DataStructs.Algebraic;

var reg = new LinearRegression();
reg.Fit(xValues, yValues);
Vector predicted = reg.Predict(newX);
Console.WriteLine($"y = {reg.Lrm.Slope:F3}·x + {reg.Lrm.Intercept:F3}");
```
</details>

<details>
<summary><strong>2D FFT фильтрация изображения</strong></summary>

```csharp
using AI.ComputerVision.FrequencyDomain;
using AI.DataStructs.Algebraic;

Matrix image = /* загрузка изображения как Matrix */;
var (re, im, _, _) = FFT2D.Forward(image);        // прямое преобразование с дополнением
FFT2D.LowPassFilter(re, im, cutoffRadius: 30);    // фильтр применяется на месте
Matrix filtered = FFT2D.Inverse(re, im, image.Height, image.Width);
```
</details>

<details>
<summary><strong>PID-регулятор</strong></summary>

```csharp
using AI.ControlSystems.Pid;

var pid = new PidController(kp: 1.0, ki: 0.5, kd: 0.1)
{
    OutputMin = -10, OutputMax = 10,      // насыщение
    UseAntiWindupTracking = true          // анти-виндап
};

const double dt = 0.01;
for (int i = 0; i < 1000; i++)
{
    double control = pid.Compute(setpoint, measurement, dt);
    measurement = plant.Step(control);
}
```
</details>

<details>
<summary><strong>Алгоритмы на графах</strong></summary>

Полный набор туториалов с выкладками и оценками сложности — в
[Docs/Tutorials/Algorithms/](Docs/Tutorials/Algorithms/): Dijkstra и A\*, Беллман–Форд, Флойд–Уоршелл,
MST, максимальный поток и минимальный разрез, поток минимальной стоимости, топологическая сортировка и SCC,
k кратчайших путей (Йен), MAPF и VRP/TSP.
</details>

---

## Интерактивный демонстратор

Unified Blazor Server демонстратор объединяет все модули в одном интерфейсе с LaTeX-формулами, интерактивными графиками и runtime-переключением CPU/GPU:

| Библиотека | Демонстрируемые возможности |
|------------|---------------------------|
| **AI.LLM** | AI-агенты (ReAct), инструменты, Semantic Kernel, MCP, память |
| **AI (ядро)** | Статистика, распределения, корреляция, KL-дивергенция, Монте-Карло |
| **AI.Algorithms** | Графы (BFS/DFS, Dijkstra, A\*, MST), потоки, паросочетания, MAPF, VRP/TSP |
| **AI.ML** | Классификация, кластеризация, регрессия, PCA, GA |
| **AI.NeuralNetworks** | MLP, GRU, автоэнкодер, нейрорегрессия |
| **AI.ComputerVision** | 2D FFT, Sobel, HOG, эквализация |
| **AI.ControlSystems** | PID, LQR, KF/EKF, MPC, скользящий режим |
| **AI.DSP** | Фильтры, FFT, спектр Уэлча |
| **AI.SignalLabs** | Генераторы сигналов, модуляция/демодуляция, АРУ |
| **AI.NLP** | Стемминг, TF-IDF, токенизация, суммаризация |
| **AI.Geometry** | Преобразования, кривые, подгонка RANSAC |
| **AI.ClassicMath** | Интегрирование, интерполяция, ОДУ, SVD |
| **AI.Solvers.Math** | Парсер выражений, CAS-упрощение, решатели |
| **AI.Fuzzy** | Нечёткий вывод (Мамдани, Сугено), нечёткий PID |
| **AI.DataPrepaire** | Нормализация, токенизация, DataTable/CSV |
| **AI.Charts** | Plotly-графики и визуализации |
| **AI.Faiss** | KNN-поиск, кластеризация |
| **AI.ONNX** | Dense, Softmax, BERT-эмбеддинги |

---

## Архитектура сборок

```mermaid
flowchart TD
  AI["AI<br/>(ядро: Vector, Matrix, Tensor,<br/>алгебра, статистика)"]
  CM["AI.ClassicMath"]
  SM["AI.Solvers.Math<br/>(парсер, CAS, решатели)"]
  ML["AI.ML<br/>(классификация, кластеризация,<br/>регрессия, PCA, GA)"]
  NN["AI.NeuralNetworks<br/>(V2 Tensor, autograd)"]
  NNG["AI.NeuralNetworks.Gpu<br/>(ILGPU / CUDA)"]
  NNO["AI.NeuralNetworks.Onnx"]
  FZ["AI.Fuzzy<br/>(нечёткая логика)"]
  LOG["AI.Logic"]
  NLP["AI.NLP<br/>(текст, BoW, TF-IDF)"]
  CS["AI.ControlSystems<br/>(PID, LQR, KF, MPC)"]
  KNN["AI.KNN"]
  DSP["AI.DSP<br/>(сигналы, FFT)"]
  SL["AI.SignalLabs<br/>(модуляция, АРУ)"]
  DP["AI.DataPrepaire"]
  ONNX["AI.ONNX"]
  EX["AI.ExplainitALL"]
  ALG["AI.Algorithms<br/>(графы, потоки, VRP, MAPF)"]
  GEO["AI.Geometry<br/>(преобразования, кривые)"]
  CV["AI.ComputerVision<br/>(фильтры, FFT2D, HOG)"]
  IE["AI.ImageEditor<br/>(SkiaSharp, без зависимостей<br/>от других сборок)"]
  FAISS["AI.Faiss<br/>(векторный поиск)"]
  CH["AI.Charts / .JS / .WinForms / .Avalonia"]
  LLM["AI.LLM<br/>(агенты, LLM, MCP, SK)"]

  AI --> CM
  CM --> SM
  AI --> ALG
  AI --> GEO
  CM --> ML
  ML --> NN
  NN --> NNG
  NN --> NNO
  AI --> FZ
  ML --> FZ
  KNN --> FZ
  AI --> NLP
  AI --> CS
  ML --> CS
  FZ --> LOG
  ML --> KNN
  ML --> DSP
  DSP --> CV
  DSP --> SL
  KNN --> DSP
  DSP --> DP
  KNN --> DP
  ML --> DP
  NLP --> DP
  FZ --> DP
  DP --> ONNX
  FZ --> ONNX
  AI --> EX
  DP --> EX
  FZ --> EX
  AI --> FAISS
  AI --> CH
  AI --> LLM
```

---

## Модули (27 библиотек)

| Сборка | Назначение |
|--------|------------|
| **AI.LLM** | AI-агенты (ReAct, function calling, prompt fallback), LLM-клиенты (OpenAI, OpenRouter, DeepSeek, Google AI), MCP-сервер, Semantic Kernel интеграция, память, гарды, биллинг. |
| **AI** | Базовые типы (`Vector`, `Matrix`, `Tensor`, `NDTensor`), линейная алгебра, статистика. Интерфейсы `IAlgorithm`, `IEstimator`, `ITransformer`. |
| **AI.ClassicMath** | Численное интегрирование, интерполяция, ОДУ, SVD, калькулятор выражений. |
| **AI.Solvers.Math** | Парсер математических выражений, CAS-упрощение (степени, дроби, приведение подобных, тригонометрия), решатели. |
| **AI.ML** | Нейросети (MLP, RNN, CNN), классификаторы (`IClassifier`), кластеризация, регрессия, PCA, GA. OpenBLAS. |
| **AI.NeuralNetworks** | Tensor Engine V2: autograd, Module/Parameter API, слои (Linear, Conv1d/2d, RNN/LSTM/GRU, Attention), нормализации, losses, DataLoader. |
| **AI.NeuralNetworks.Gpu** | GPU-ускорение V2 через ILGPU: matmul, RNN/LSTM/GRU ядра. |
| **AI.NeuralNetworks.Onnx** | Маппинг V2 Tensor → ONNX Runtime. |
| **AI.KNN** | k-ближайших соседей: классификация, регрессия, мультирегрессия. |
| **AI.Fuzzy** | Нечёткая логика (Мамдани / Ларсена / Сугено / Цукамото), нечёткий PID. |
| **AI.NLP** | Стеммер, лемматизация, BoW, TF-IDF, токенизация, NER, суммаризация. |
| **AI.ControlSystems** | PID, LQR, LQG, KF, EKF, MPC, скользящий режим, MRAC, RLS, размещение полюсов. |
| **AI.DSP** | Фильтры (IIR/FIR), FFT, спектральный анализ (Уэлч). |
| **AI.SignalLabs** | Генераторы сигналов, модуляция/демодуляция, определение типа модуляции, АРУ. |
| **AI.ComputerVision** | 2D FFT (CPU + cuFFT GPU), Sobel, HOG, эквализация, цветовая обработка. |
| **AI.ImageEditor** | Кроссплатформенный редактор изображений на SkiaSharp: фильтры (свёртки, точечные, Retinex), кисти, история команд, сессии. |
| **AI.Algorithms** | Графы, сетевые потоки, паросочетания, MAPF, VRP/TSP, транспортная задача. |
| **AI.Geometry** | Аффинные, гомография, кватернионы, RANSAC, Безье/Эрмит, SVD/LU/Холецкий. |
| **AI.DataPrepaire** | Нормализаторы (ZNorm, MinMax), токенизаторы, DataTable, CSV. |
| **AI.ONNX** | Загрузка и инференс ONNX-моделей (Dense, Softmax, BERT). |
| **AI.Faiss** | Векторный поиск: KNN, пакетный поиск, L2/IP, Assign-кластеризация. |
| **AI.Charts** | SkiaSharp-графики: scatter, line, bar, polar, PSD, 3D. |
| **AI.Charts.JS** | Plotly.js для Blazor Server. |
| **AI.Charts.WinForms** | WinForms-визуализатор. |
| **AI.Charts.Avalonia** | Avalonia-визуализатор. |
| **AI.Logic** | Логические структуры (используется в `AI.Fuzzy`). |
| **AI.ExplainitALL** | Метрики интерпретируемости и RAG-инструменты. |

---

## Структура репозитория

```
AIFramework3Open/
├── src/                              # Исходный код библиотек (27 проектов)
│   ├── AI/                           # Ядро (Vector, Matrix, Tensor, IAlgorithm)
│   ├── AI.LLM/                       # AI-агенты, LLM-клиенты, MCP, SK
│   ├── AI.ML/                        # Машинное обучение
│   ├── AI.NeuralNetworks/            # Tensor Engine V2, autograd
│   ├── AI.NeuralNetworks.Gpu/        # GPU-ускорение (ILGPU/CUDA)
│   ├── AI.Algorithms/                # Графы, потоки, MAPF, VRP/TSP
│   ├── AI.Geometry/                  # Геометрия, кривые, линейная алгебра
│   ├── AI.ComputerVision/            # Обработка изображений, 2D FFT
│   ├── AI.ImageEditor/               # Редактор изображений (фильтры, команды)
│   ├── AI.Fuzzy/                     # Нечёткая логика
│   ├── AI.NLP/                       # Обработка текста
│   ├── AI.ControlSystems/            # Системы автоматического управления
│   ├── AI.DSP/                       # Цифровая обработка сигналов
│   ├── AI.SignalLabs/                # Генераторы, модуляция, АРУ
│   ├── AI.Solvers.Math/              # Парсер выражений, CAS, решатели
│   ├── AI.Charts/                    # Графика (SkiaSharp)
│   ├── AI.Charts.JS/                 # Plotly.js для Blazor
│   └── ...                           # AI.KNN, AI.DataPrepaire, AI.ONNX, AI.Faiss и др.
├── Demo/
│   └── WebUI/AiFrameworkDemo/        # Unified Blazor Server демонстратор
├── Tests/
│   ├── unit/                         # xUnit автотесты (AIFramework.UnitTests, AI.LLM.UnitTests)
│   ├── shared/                       # Общий код для тестов (TestHelpers)
│   └── ...                           # Консольные и демо-тесты по доменам
├── Tools/
│   └── DocsLint/                     # Линтер канона документации (CI)
├── Docs/                             # Документация
│   ├── Architecture/                 # Архитектурные описания
│   └── Tutorials/                    # Туториалы (Markdown + LaTeX)
├── SLNS/                             # Частичные решения по доменам
├── CODING_STANDARD.md                # Единый стандарт кода
├── Directory.Build.props             # Общие настройки сборки
├── .editorconfig                     # Правила форматирования
├── AIFramework.sln                   # Основное решение
└── AIFramework-Core.sln              # Только ядро
```

### Решения (.sln)

| Решение | Когда использовать |
|---------|--------------------|
| [AIFramework.sln](AIFramework.sln) | Каноническое: все библиотеки, тесты и CI. |
| [AIFramework-Core.sln](AIFramework-Core.sln) | Только ядро — быстрая сборка без UI-зависимостей. |
| [AIFramework3(WebUI).sln](<AIFramework3(WebUI).sln>) | Работа над Blazor-демонстратором. |
| [AIFramework3.Deploy.sln](AIFramework3.Deploy.sln) | Сценарии публикации. |
| [SLNS/](SLNS/) | Частичные решения по доменам (ML, NLP, сигналы, логика, статистика). |

### Тесты

```bash
# все автотесты решения
dotnet test AIFramework.sln -c Release

# только xUnit-проекты
dotnet test Tests/unit/AIFramework.UnitTests/AIFramework.UnitTests.csproj -c Release
dotnet test Tests/unit/AI.LLM.UnitTests/AI.LLM.UnitTests.csproj -c Release
```

Консольные проекты в `Tests/` — дополнительные демо и дымовые проверки; критичные регрессии дублируйте в xUnit.
Документация теории проверяется линтером [Tools/DocsLint](Tools/DocsLint/) в CI по канону
[Docs/Tutorials/STRUCTURE.md](Docs/Tutorials/STRUCTURE.md).

---

## Стандарт кода

Проект следует единому [CODING_STANDARD.md](CODING_STANDARD.md):

- **Форматирование** — UTF-8, CRLF, 4 пробела, file-scoped namespaces.
- **Именование** — PascalCase для типов/членов, `_camelCase` для приватных полей, `I`-префикс для интерфейсов.
- **Публичный API** — только `Vector` / `Matrix` / `NDTensor` в сигнатурах (не `double[]` / `double[,]`).
- **Интерфейсы** — `IAlgorithm` → `IEstimator<TInput,TLabel>` (Fit/Predict), `ITransformer<TInput,TOutput>` (Fit/Transform).
- **XML-документация** — на русском языке для public/protected членов.
- **Анализаторы** — `EnforceCodeStyleInBuild`, .NET Analyzers, `.editorconfig`.

---

## Документация

| Ресурс | Содержание |
|--------|------------|
| [CODING_STANDARD.md](CODING_STANDARD.md) | Единый стандарт API и кода. |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Сборка, тесты, CI, NuGet, состав модулей. |
| [Docs/Architecture/](Docs/Architecture/) | Архитектурные описания всех модулей. |
| [Docs/Tutorials/](Docs/Tutorials/) | Туториалы (Markdown + LaTeX). |
| [Docs/Tutorials/STRUCTURE.md](Docs/Tutorials/STRUCTURE.md) | Канон оформления теоретической документации. |
| [Docs/Tutorials/LLM/](Docs/Tutorials/LLM/) | AI-агенты, LLM, MCP, Semantic Kernel. |
| [Docs/Tutorials/Algorithms/](Docs/Tutorials/Algorithms/) | Графовые алгоритмы, потоки, MAPF, VRP/TSP. |
| [Docs/INFO.md](Docs/INFO.md) | Контрибьюторы, атрибуция, лицензии. |
| [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) · [SECURITY.md](SECURITY.md) | Правила сообщества и сообщение об уязвимостях. |

---

## Лицензия и атрибуция

Проект распространяется под **Apache 2.0**. Список контрибьюторов, сторонний код и тексты лицензий MIT для отдельных заимствований — в [Docs/INFO.md](Docs/INFO.md).
