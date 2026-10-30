# AIFramework

Открытая библиотека на C# и .NET 9 для искусственного интеллекта и научных расчетов: языковые модели и автономные агенты, нейросети, машинное обучение, обработка сигналов и текстов, компьютерное зрение, решатели и численные методы.

Фреймворк разбит на отдельные пакеты, поэтому подключать стоит только нужные. Пакет [AI](https://www.nuget.org/packages/AI) содержит ядро: векторы, матрицы, тензоры и статистику. Остальные пакеты подтягивают его сами.

```bash
dotnet add package AI.LLM
dotnet add package AI.NeuralNetworks
dotnet add package AI.DSP
```

## Пакеты

| Направление | Пакеты |
|---|---|
| Агенты и языковые модели | [AI.LLM](https://www.nuget.org/packages/AI.LLM) |
| Машинное обучение | [AI.ML](https://www.nuget.org/packages/AI.ML), [AI.KNN](https://www.nuget.org/packages/AI.KNN), [AI.Fuzzy](https://www.nuget.org/packages/AI.Fuzzy), [AI.ExplainitALL](https://www.nuget.org/packages/AI.ExplainitALL) |
| Нейросети | [AI.NeuralNetworks](https://www.nuget.org/packages/AI.NeuralNetworks), [AI.NeuralNetworks.Gpu](https://www.nuget.org/packages/AI.NeuralNetworks.Gpu), [AI.NeuralNetworks.Onnx](https://www.nuget.org/packages/AI.NeuralNetworks.Onnx), [AI.ONNX](https://www.nuget.org/packages/AI.ONNX) |
| Сигналы | [AI.DSP](https://www.nuget.org/packages/AI.DSP), [AI.SignalLabs](https://www.nuget.org/packages/AI.SignalLabs), [AI.Microwave](https://www.nuget.org/packages/AI.Microwave) |
| Текст и данные | [AI.NLP](https://www.nuget.org/packages/AI.NLP), [AI.DataPrepaire](https://www.nuget.org/packages/AI.DataPrepaire) |
| Зрение и графика | [AI.ComputerVision](https://www.nuget.org/packages/AI.ComputerVision), [AI.ImageEditor](https://www.nuget.org/packages/AI.ImageEditor), [AI.Charts](https://www.nuget.org/packages/AI.Charts), [AI.Charts.JS](https://www.nuget.org/packages/AI.Charts.JS), [AI.Charts.WinForms](https://www.nuget.org/packages/AI.Charts.WinForms), [AI.Charts.Avalonia](https://www.nuget.org/packages/AI.Charts.Avalonia) |
| Математика и алгоритмы | [AI.ClassicMath](https://www.nuget.org/packages/AI.ClassicMath), [AI.Geometry](https://www.nuget.org/packages/AI.Geometry), [AI.Algorithms](https://www.nuget.org/packages/AI.Algorithms), [AI.Logic](https://www.nuget.org/packages/AI.Logic), [AI.Solvers.Math](https://www.nuget.org/packages/AI.Solvers.Math), [AI.Solvers.Optimization](https://www.nuget.org/packages/AI.Solvers.Optimization), [AI.Solvers.Constraints](https://www.nuget.org/packages/AI.Solvers.Constraints), [AI.Solvers.Pde](https://www.nuget.org/packages/AI.Solvers.Pde) |
| Науки | [AI.Physics](https://www.nuget.org/packages/AI.Physics), [AI.Biology](https://www.nuget.org/packages/AI.Biology), [AI.Earth](https://www.nuget.org/packages/AI.Earth), [AI.Solvers.Chem](https://www.nuget.org/packages/AI.Solvers.Chem), [AI.Psychology](https://www.nuget.org/packages/AI.Psychology) |
| Экономика, управление, моделирование | [AI.Economics](https://www.nuget.org/packages/AI.Economics), [AI.Econometrics](https://www.nuget.org/packages/AI.Econometrics), [AI.ControlSystems](https://www.nuget.org/packages/AI.ControlSystems), [AI.Simulation](https://www.nuget.org/packages/AI.Simulation) |
| Язык сценариев AIScript | [AI.Script](https://www.nuget.org/packages/AI.Script), [AI.Script.Std](https://www.nuget.org/packages/AI.Script.Std), [AI.Script.Charts](https://www.nuget.org/packages/AI.Script.Charts), [AI.Script.Chem](https://www.nuget.org/packages/AI.Script.Chem), [AI.Script.Data](https://www.nuget.org/packages/AI.Script.Data), [AI.Script.Llm](https://www.nuget.org/packages/AI.Script.Llm), [AI.Script.Media](https://www.nuget.org/packages/AI.Script.Media), [AI.Script.Nn](https://www.nuget.org/packages/AI.Script.Nn), [AI.Script.Office](https://www.nuget.org/packages/AI.Script.Office), [AI.Script.Science](https://www.nuget.org/packages/AI.Script.Science), [AI.Script.Vision](https://www.nuget.org/packages/AI.Script.Vision) |

## Ссылки

- [Исходный код и примеры](https://github.com/AIFramework/AIFramework3Open)
- [Учебные материалы](https://github.com/AIFramework/AIFramework3Open/tree/main/Docs/Tutorials)
- [Устройство модулей](https://github.com/AIFramework/AIFramework3Open/tree/main/Docs/Architecture)
- [Интерактивный демонстратор](https://github.com/AIFramework/AIFramework3Open/tree/main/Demo/WebUI/AiFrameworkDemo)
- [Сообщить об ошибке](https://github.com/AIFramework/AIFramework3Open/issues)
- [Канал в Telegram](https://t.me/ai_prog_math)

Лицензия [Apache-2.0](https://github.com/AIFramework/AIFramework3Open/blob/main/LICENSE). Сторонний код и его лицензии перечислены в файле `THIRD-PARTY-NOTICES.md` внутри пакета.

## English

Open-source C# / .NET 9 library for artificial intelligence and scientific computing: LLM clients and autonomous agents, neural networks, machine learning, signal and text processing, computer vision, solvers and numerical methods.

The framework is split into separate packages, so install only what you need: see the table above. The [AI](https://www.nuget.org/packages/AI) package is the core with vectors, matrices, tensors and statistics, and the other packages bring it in automatically.

Source code, documentation and samples: <https://github.com/AIFramework/AIFramework3Open>
