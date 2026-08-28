# AI.Script

Язык скриптования конвейеров обработки данных над библиотеками AIFramework 3.0.

**Статус: план пройден целиком, этапы M0–M5.** Ядро языка, проверка до запуска с выводом
типов, слой привязки, таблицы, файловый ввод-вывод в песочнице, манифест возможностей, CLI
`aisc` и **482 функции в 31 пространстве**: `core`, `math`, `vec`, `mat`, `stat`, `str`,
`re`, `date`, `table`, `io`, `prep`, `ml`, `signal`, `dsp`, `nlp`, `solve`, `graph`, `geom`,
`fuzzy`, `ctrl`, `plot`, `llm`, `search`, `econ`, `mw`, `chem`, `logic`, `siglab`, `explain`,
`nn`, `cv`. Конвейеры: стадии с кэшем (память и диск), повторы, таймауты, граф прогона,
параллельная карта. LLM-контур: запросы и поиск из скрипта, скрипт как инструмент агента,
профили доверия, потолки расходов, цикл «написал → проверил → исполнил». Тяжёлые по
зависимостям пространства подключаются отдельными вызовами: `UseCharts`, `UseLlm`, `UseChem`,
`UseNeuralNetworks`, `UseVision`.
См. [состояние реализации](DESIGN.md#171-состояние-реализации).

| Файл | Что внутри |
|------|-----------|
| [DESIGN.md](DESIGN.md) | Полный проект языка: принципы, синтаксис, система значений, стандартная библиотека, архитектура рантайма, план работ |
| [PROMPT_CARD.md](PROMPT_CARD.md) | Одностраничные правила языка — то, что кладётся в системный промпт LLM |
| [examples/00_m0_tour.ais](examples/00_m0_tour.ais) | Обзор ядра языка; исполняется приёмочным тестом |
| [examples/01_m1_pipeline.ais](examples/01_m1_pipeline.ais) | Табличный конвейер от CSV до матрицы признаков; исполняется приёмочным тестом |
| [examples/02_m3_ml.ais](examples/02_m3_ml.ais) | Классификация, кластеризация, PCA, метрики и графики; исполняется приёмочным тестом |
| [examples/03_m3_dsp.ais](examples/03_m3_dsp.ais) | Генерация, фильтрация, спектральный анализ; исполняется приёмочным тестом |
| [examples/04_m3_control.ais](examples/04_m3_control.ais) | Идентификация объекта по логу, настройка ПИД, замкнутая симуляция; исполняется приёмочным тестом |
| [examples/05_m4_stages.ais](examples/05_m4_stages.ais) | Конвейер из стадий с кэшем, повторами и параллельным перебором; исполняется приёмочным тестом |
| [examples/06_m5_rag.ais](examples/06_m5_rag.ais) | Поиск по корпусу и ответ модели по найденному; исполняется без сети приёмочным тестом |
| [examples/07_domains.ais](examples/07_domains.ais) | СВЧ-сушка: физика задаёт производительность, та — экономику, химия — расход реагента |
| [examples/08_vision_nn.ais](examples/08_vision_nn.ais) | Картинка → признаки → сеть: распознавание направления полос; исполняется приёмочным тестом |
| [examples/planned/](examples/planned) | Примеры замысла: написаны от желаемого языка и **не исполняются** — по ним видно, чего ещё нет |

## Как запустить

```csharp
using AI.Script.Hosting;
using AI.Script.Std;

// Графики — отдельная сборка: ядру языка незачем тянуть графический слой.
var host = StandardLibrary.CreateHost().UseCharts();

// Проверка без запуска: миллисекунды, без побочных эффектов.
CheckResult check = host.Check(source);
if (!check.Success) Console.WriteLine(check.Render());

RunResult result = await host.RunAsync(source, new RunOptions { Seed = 42 });

Console.WriteLine(string.Join("\n", result.Transcript));
double silhouette = (double)result.Emitted["silhouette"]!;
```

Данные подаются переменными, а не текстом внутри скрипта; файлы — только через песочницу:

```csharp
var options = new RunOptions
{
    Seeded = new Dictionary<string, object?> { ["prices"] = priceVector },

    // Без этого io.* вообще недоступен: запрет по умолчанию, а не разрешение.
    Sandbox = new WorkspaceSandbox("./workspace", readOnly: false),
};
```

## Утилита `aisc`

```bash
dotnet run --project Tools/aisc -- check src/AI.Script/examples/01_m1_pipeline.ais
```

| Команда | Что делает |
|---------|-----------|
| `aisc check <файл> [--json]` | Проверяет скрипт, не выполняя его; код возврата 1 при ошибках |
| `aisc run <файл> [--seed N] [--timeout 30s] [--parallel N] [--workdir DIR] [--read-only] [--no-files] [--cache DIR \| --no-cache] [--graph[=mermaid]] [--progress] [--json] [--stats]` | Проверяет и выполняет; рабочая папка по умолчанию — папка скрипта |
| `aisc docs [пространство …] [--index] [--compact] [--json] [--max N] [--out ФАЙЛ]` | Манифест возможностей; `--index` даёт то, что кладётся в системный промпт модели |
| `aisc help <запрос>` | Сигнатура и описание функции плюс похожие |

Коды возврата различают три исхода: `0` — успех, `1` — ошибка в скрипте, `2` — ошибка вызова
самой утилиты. Иначе любой скрипт сборки вынужден разбирать текст, чтобы понять, чья ошибка.

Результаты стадий с `@cache` утилита складывает в `.aisc-cache` рядом со скриптом, поэтому
повторный запуск их не считает — это видно по `--stats` и по `--graph`:

```bash
dotnet run --project Tools/aisc -- run src/AI.Script/examples/05_m4_stages.ais --stats --graph
```

## Конвейер из стадий

Стадия — функция, за которой наблюдают и результат которой можно не считать заново. Она видит
только свои параметры: это не строгость ради строгости, а условие кэшируемости — результат,
зависящий от того, что лежало снаружи, кэшировать было бы неверно.

```python
@cache
@retry(2)
@timeout(30s)
stage признаки(t: table) -> mat {
    t |> table.select(["x", "y"]) |> table.to_matrix() |> mat.zscore()
}
```

```csharp
// Кэш живёт столько же, сколько объект: в поле хоста — между прогонами.
var cache = new MemoryStageCache();          // либо new FileStageCache("./.cache")

var options = new RunOptions
{
    Cache = cache,
    Parallelism = 4,                          // сколько ветвей у core.map(parallel: true)
    Progress = new DelegateProgressSink((stage, done) => Console.WriteLine(stage)),
};

RunResult result = await host.RunAsync(source, options);

Console.WriteLine(result.Graph.Render());     // стадии прогона с итогами
Console.WriteLine(result.Graph.ToMermaid());  // тот же граф для вставки в отчёт
Console.WriteLine($"из кэша: {result.Stats.CachedStages} из {result.Stats.Stages}");
```

Ключ кэша складывается из текста стадии, версий модулей и значений аргументов: правка тела или
других данных даёт другой ключ и честный пересчёт. Дескриптор в аргументах делает стадию
некэшируемой — с указанием причины в узле графа, а не молча.

## Как добавить свои функции

Ядро языка не знает о содержимом фреймворка: новая библиотека подключается модулем.

```csharp
[ScriptModule("myco", "Функции нашего проекта")]
public static class MyModule
{
    [ScriptFn("score", "Скоринг заявки", Example = "myco.score(features, threshold: 0.5)")]
    public static double Score(
        [ScriptParam("вектор признаков")] Vector features,
        [ScriptParam("порог отсечения")] double threshold = 0.5) => /* ... */;
}

host.Use(ScriptModule.FromType(typeof(MyModule)));
```

Описание из атрибутов попадает и в `help("myco")`, и в манифест возможностей, и в диагностику
«возможно, имелось в виду»: документация выводится из того же объекта, что и вызов, и
разойтись с ним не может.

## Скрипт как инструмент агента

Агент получает не тридцать разрозненных инструментов, а один способ их соединить: данные
остаются в процессе, наружу уходят числа-результаты и перечень показанного.

```csharp
using AI.Script.Llm;

// Модель и эмбеддер создаёт хост — со своими ключами, прокси и биллингом.
var host = StandardLibrary.CreateHost().UseCharts().UseLlm(client, embedder);

// Профиль, а не восемь настроек поштучно: сеть выключена, файлы только на чтение,
// таймаут 60 с, часть опций закреплена за хостом.
var tool = new ScriptTool(host, () => RunProfiles.Untrusted("./workspace"));

ToolRegistry tools = ToolRegistry.FromObjects(tool);   // run_script, check_script, script_help
```

Цикл «написал → проверил → исполнил» — то, ради чего проверка вообще существует: опечатка
модели стоит одного дешёвого ответа с диагностикой, а не полного прогона.

```csharp
var writer = new ScriptWriter(client, host, new ScriptWriterOptions { MaxRepairs = 2 });

ScriptSolution solution = await writer.SolveAsync("Посчитай выручку по городам из sales.csv");

if (solution.Success) Console.WriteLine(solution.Result!.Emitted["выручка"]);
```

Диагностики уходят модели дословно — они и написаны так, чтобы по ним исправляли не
догадываясь. Сорвавшийся прогон чинится тем же циклом: для модели это такая же ошибка с
позицией, разница лишь в том, когда её заметили.

Расходы ограничены прогоном, а не доверием к скрипту:

```csharp
var options = RunProfiles.UntrustedWithNetwork(
    workdir: "./workspace", calls: 20, tokens: 100_000, cost: 0.5m, hosts: "openrouter.ai");

options.Secrets = [apiKey];   // маскируется в выводе, артефактах и сообщениях об отказах
```

Эталонный набор из десяти задач — `ScriptBenchmark.Tasks`; прогнать его моделью:
`await ScriptBenchmark.RunAsync(writer)`.

## Манифест для языковой модели

```csharp
// Уровень 1 — в системный промпт: список пространств имён, меньше 2 тыс. символов.
string index = host.DescribeCapabilities(ManifestOptions.Index);

// Уровень 2 — по запросу модели: точная сигнатура и поиск по словам задачи.
string signature = host.Describe("stat.corr");
IReadOnlyList<ManifestMatch> found = host.Search("корреляция");
```

То же доступно изнутри скрипта — `help("stat")` и `find_fn("корреляция")`.

## Короткая суть языка

- конвейер пишется линейно через `|>`, а не вложенными вызовами;
- позиционно передаются обязательные аргументы, необязательные — только по имени;
- всё квалифицировано пространством имён (`math.sqrt`, `stat.mean`), импортов нет;
- ошибки в именах, аргументах и типах ловятся `Check` до запуска, с подсказкой;
- скрипт исполняется с лимитами по шагам, памяти, глубине вызовов и времени;
- файлы — только внутри выданной хостом рабочей папки.

```python
options { seed: 42 }

let sales =
    io.read_csv("sales.csv")
    |> table.filter(row => row.amount > 0)

let clients =
    sales
    |> table.group_by("client", agg: {
           revenue: rows => vec.sum(rows["amount"]),
           orders:  rows => len(rows)
       })
    |> table.derive(cols: { avg_check: row => row.revenue / row.orders })
    |> table.sort(by: "revenue", desc: true)

let features =
    sales
    |> table.select(["region", "amount"])
    |> table.one_hot(["region"])
    |> table.to_matrix()
    |> mat.zscore()

emit лидер = clients[0].client
show table.describe(clients)
```

Решения, которые ещё не приняты, собраны в [§18 DESIGN.md](DESIGN.md#18-решения-которые-нужно-принять).
