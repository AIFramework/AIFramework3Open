# Вспомогательные и интеграционные модули

Краткий указатель сборок под `src/`, которые не вынесены в отдельные большие обзоры **[README.md](README.md)**, но важны для полной картины решения.

| Сборка | Назначение (кратко) |
|--------|---------------------|
| **`AI.DataPrepaire`** | Подготовка данных: нормализаторы (ZNorm, MinMax), токенизаторы (Word, BPE, Sentence), пайплайны, DataTable, CSV. Связка с **`AI.DSP`**, **`AI.ML`**, **`AI.KNN`**, **`AI.NLP`**, **`AI.Fuzzy`**. |
| **`AI.ONNX`** | Загрузка и инференс ONNX-моделей: Dense Layer, Softmax-классификатор, Tensor2Tensor, BERT-эмбеддинги. |
| **`AI.NeuralNetworks.Onnx`** | Маппинг V2 Tensor → ONNX Runtime (экспериментально). |
| **`AI.KNN`** | k-ближайших соседей: классификация (`KNNCl`, реализует `IClassifier`), регрессия, мультирегрессия, корреляционная регрессия. |
| **`AI.Faiss`** | Векторный поиск: KNN (единичный/пакетный), метрики L2/Inner Product, Assign-кластеризация. Обёртка поверх Faiss с C#-fallback. |
| **`AI.Logic`** | Логические структуры (используется в **`AI.Fuzzy`**, см. [FuzzyLogic.md](FuzzyLogic.md)). |
| **`AI.ExplainitALL`** | Метрики интерпретируемости и RAG-вспомогательные средства. Ссылается на **`AI.Fuzzy`**, **`AI.DataPrepaire`**. |
| **`AI.Charts.JS`** | Plotly.js-рендеринг для Blazor Server: интерактивные 2D/3D графики в браузере. |
| **`AI.Charts.WinForms`** | WinForms-визуализатор (`ChartVisual` UserControl): scatter, line, bar, polar, 3D. |
| **`AI.Charts.Avalonia`** | Avalonia-визуализатор для кроссплатформенного desktop. |
| **`AI.Solvers.Math`** | Символьная математика: разбор выражений (`AdvancedMathParser`), CAS-упрощение (приведение подобных, степени, дроби, тригонометрия), решатели производных, интегралов, ОДУ и УрЧП (эллиптические, гиперболические, Гельмгольца, перенос), таблица Лапласа. Зависимости: **`AI`**, **`AI.ClassicMath`**. |
| **`AI.SignalLabs`** | Лаборатория радиосигналов: генераторы (синус, меандр), АРУ (прямая, логарифмическая, min-combine), фильтр «приподнятый косинус», цифровые модуляции (ASK, BPSK, QPSK, QAM8/16), квадратурная демодуляция, определитель типа модуляции. Зависимости: **`AI`**, **`AI.DSP`**. |
| **`AI.ImageEditor`** | Ядро растрового редактора: слои и документ, `PixelBuffer` поверх BGRA, кисть, история отмен (`EditSession`/`UndoStep`), реестр фильтров (свёртка, точечные, Retinex). Единственная зависимость — **SkiaSharp**: алгоритмы взяты из **`AI.ComputerVision`** по смыслу, но переписаны на прямой доступ к байтам, потому что путь через `Matrix(double)` для интерактивного редактора слишком медленный. |

Гибридные нечётко-ML типы (`FuzzyClassifier`, `LingVarGaussian` и т.п.) входят в **`AI.Fuzzy`** (отдельной сборки **`AI.FuzzyML`** в репозитории нет).

Зависимости между проектами смотрите в соответствующих `*.csproj`.

---

## Тесты и демо

- Каталог **`Tests/`** — консольные и демо-тесты для отдельных модулей.
- Каталог **`tests/unit/`** — xUnit автотесты (CI).
- Каталог **`Demo/WebUI/AiFrameworkDemo/`** — единый Blazor Server демонстратор, объединяющий **15 модулей** с интерактивной визуализацией.

---

## См. также

- [LLM.md](LLM.md) — **`AI.LLM`** (клиенты моделей, агенты, ReAct, MCP, Semantic Kernel)
- [AI-Core.md](AI-Core.md) — ядро **`AI`**
- [MachineLearning.md](MachineLearning.md) — **`AI.ML`** и цепочка **`ClassicMath`**
- [NeuralNetworks.md](NeuralNetworks.md) — **`AI.NeuralNetworks`** и GPU
- [Algorithms.md](Algorithms.md) — **`AI.Algorithms`** (графы, потоки, MAPF, VRP)
- [Geometry.md](Geometry.md) — **`AI.Geometry`**
- [ComputerVision.md](ComputerVision.md) — **`AI.ComputerVision`**
- [DSP.md](DSP.md) — **`AI.DSP`**
- [Charts.md](Charts.md) — **`AI.Charts`**
