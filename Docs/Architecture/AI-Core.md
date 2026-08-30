# Ядро `AI` (AI.dll)

Сборка **`AI`** (`AI.dll`, **.NET 9.0**) — **общая основа** почти всех библиотек в `src/`: линейная алгебра, тензоры базового уровня, статистика, расстояния, часть численных и сериализационных утилит. Без внешних зависимостей NuGet (кроме стандартной базы BCL для целевой платформы).

---

## Ключевые области

| Область / пространство имён | Назначение |
|------------------------------|------------|
| **`AI.DataStructs.Algebraic`** | **`Vector`**, **`Matrix`**, **`NDTensor`**, интерфейсы алгебраических структур. Основа для данных во всех модулях. |
| **`AI.DataStructs.WithComplexElements`** | **`ComplexVector`** и связанные типы для комплексных рядов. |
| **`AI.DataStructs.Shapes`** | Описание размерностей **`Shape`**, `Shape1D`–`Shape4D` для тензоров. |
| **`AI.Statistics`** | Описательная статистика, **`Statistic`**, гистограммы, распределения (`AI.Statistics.Distributions`), смеси (`MixtureModeling`), MCMC-заготовки, квантили. |
| **`AI.Distances`** | Метрики и расстояния между векторами и распределениями. |
| **`AI.Extensions`** | Расширения для массивов, строк, алгебраических типов, потоков данных. |
| **`AI.HighLevelFunctions`** | Вспомогательные функции (аналитическая геометрия, поэлементные операции). |
| **`AI.Units`** | Физические величины: размерности, единицы измерения, перенос неопределённости, константы CODATA. См. раздел ниже. |
| **`AI.Insights`** | Контракт объяснимости: `IInterpretable`, `Interpretation`, `InterpretationBuilder`. См. раздел ниже. |

Отдельные файлы: **`Convolution`**, **`Correlation`**, **`Sound`**, **`IntervalData`**, **`InMemoryDataStream`**, настройки **`AISettings`**.

---

## Физические величины (`AI.Units`)

Слой, дающий числам размерность. До него весь публичный API оперировал безразмерным `double`,
и результат `AI.Microwave` нельзя было отличить от результата `AI.Economics` иначе как по имени
переменной. Величина хранится в базовых единицах СИ, поэтому данные из разных модулей
складываются напрямую, а несовпадение размерностей обнаруживается в точке операции.

| Тип | Назначение |
|-----|------------|
| **`Dimension`** | Вектор показателей степени семи базовых величин СИ. Показатели хранятся в половинах — представимы корни второй степени (В/√Гц). |
| **`Unit`** | Единица измерения: символ, размерность, множитель перевода в СИ и смещение для аффинных шкал (°C, °F). |
| **`Si`** | Готовые единицы: семь базовых, производные со специальными названиями, употребительные внесистемные. |
| **`UnitRegistry`** | Разбор символьной записи («kW·h», «m/s^2», «mg/L»), десятичные приставки, выбор единицы вывода. |
| **`Quantity`** | Величина: значение в СИ плюс размерность. Арифметика, сравнение, разбор и форматирование. |
| **`Measurement`** | Величина со стандартной неопределённостью и её переносом по линейному закону. |
| **`QuantityVector`** | Ряд однородных величин — граница между размерным миром и алгоритмами, работающими с `Vector`. |
| **`PhysicalConstants`** | Константы CODATA 2022; измеряемые — с неопределённостью в `PhysicalConstants.WithUncertainty`. |

```csharp
using AI.Units;

Quantity speed = Quantity.Of(90, "km/h");
double siSpeed = speed.In(Si.MetrePerSecond);            // 25

Quantity wavelength = PhysicalConstants.SpeedOfLight / Quantity.Of(2.45, "GHz");
Console.WriteLine(wavelength.In("mm"));                   // ≈ 122.36

Quantity force = Quantity.Of(2, Si.Kilogram) * Quantity.Of(3, "m/s^2");
Console.WriteLine(force.In(Si.Newton));                   // 6
_ = Quantity.Of(1, Si.Metre) + Quantity.Of(1, Si.Second); // DimensionMismatchException
```

Проверка на границе публичного API — метод `RequireSi`: он сверяет размерность и возвращает
значение в СИ, поэтому внутренняя реализация продолжает работать с обычным `double`.

Потребители слоя: типизированные перегрузки `PowderAnalysis` и параметры `UnitCell` в `AI.Solvers.Chem`, класс `MicrowaveQuantities` в `AI.Microwave`, а также мост `UncertaintyBudget.ToMeasurement()`, переводящий бюджет неопределённости по GUM в `Measurement`.

```csharp
public double PowerDensity(Quantity power, Quantity area)
{
    double p = power.RequireSi(Dimension.Power, nameof(power));
    double a = area.RequireSi(Dimension.Area, nameof(area));
    return p / a;
}
```

Неопределённость переносится по линейному закону в предположении **независимости** операндов:
суммы и разности складывают абсолютные неопределённости в квадратуре, произведения и частные —
относительные. Для коррелированных величин выражение нужно упрощать аналитически до подстановки чисел.

```csharp
var length = Measurement.Of(2.00, 0.01, Si.Metre);
Console.WriteLine(length.Pow(3));                         // 8 ± 0.12 m³
Console.WriteLine(PhysicalConstants.WithUncertainty.GravitationalConstant.RelativeUncertainty); // 2.2e-5
```

## Объяснимость результатов (`AI.Insights`)

Контракт, по которому результат расчёта объясняет себя словами: итог, метрики с оценкой
относительно предметных порогов, выводы, нарушенные допущения и рекомендации. Текст
предназначен и человеку, и языковой модели — `Interpretation.ToLlmText()` отдаёт готовый
структурированный блок.

| Тип | Назначение |
|-----|------------|
| **`IInterpretable`** | Единственный метод `Interpret()`; пайплайн «посчитать и объяснить» пишется один раз на все методы. |
| **`Interpretation`** | Разбор результата: `Title`, `Summary`, `Metrics`, `Findings`, `Warnings`, `Recommendations`. |
| **`InterpretationBuilder`** | Построитель с условными добавлениями `FindingIf`, `WarningIf`, `RecommendationIf`. |
| **`Fmt`** | Форматирование чисел в инвариантной культуре. |

Контракт жил в `AI.Economics.Insights` и переехал в ядро, когда его потребовала химия:
доменной библиотеке незачем зависеть от другой доменной библиотеки. Предметные разборы
остаются в своих модулях — `AI.Economics/Insights/*.cs` и `AI.Solvers.Chem/Insights/*.cs`.

> **Аргументы `FindingIf` и `WarningIf` вычисляются всегда**, независимо от условия:
> это обычные параметры, а не лямбды. Интерполяцию, разыменовывающую возможный `null`,
> нужно готовить заранее.

```csharp
public sealed partial class HuckelSolution : IInterpretable
{
    public Interpretation Interpret() => new InterpretationBuilder("Расчёт π-системы по методу Хюккеля")
        .Summary(...)
        .Metric("Щель", Fmt.Num(Gap, 4), "|β|", "чем меньше, тем реакционноспособнее система")
        .FindingIf(ObeysHuckelRule, "Число π-электронов отвечает правилу 4n+2 ...")
        .Warning("Метод учитывает только π-подсистему: σ-остов и корреляция в расчёт не входят.")
        .Build();
}
```

---

## Соглашения

- Корневое пространство имён **`AI`** для части API; многие типы сосредоточены в **`AI.DataStructs.*`**.
- Векторы и матрицы активно используются в **`AI.ML`**, **`AI.DSP`**, **`AI.Charts`**, **`AI.Fuzzy`**, **`AI.ControlSystems`** и др. Перед сменой контрактов `Vector`/`Matrix` проверяйте потребителей в решении.

---

## Сборка

```bash
dotnet build src/AI/AI.csproj -c Release
```

См. также обзоры: [MachineLearning.md](MachineLearning.md) (цепочка через **`AI.ClassicMath`**), [DSP.md](DSP.md), [Charts.md](Charts.md).
