# Графики: ChartView, WinForms и тепловая карта

**Сборки:** `AI.Charts` (ядро), `AI.Charts.WinForms`, `AI.Charts.Avalonia`  
**Пространства имён:** `AI.Charts`, `AI.Charts.WinForms`, `AI.Charts.Forms`

---

## 1. Назначение модулей

| Сборка | Назначение |
|--------|------------|
| **AI.Charts** | Логика графиков без UI: класс **`ChartView`**, элементы серий (линии, столбцы, точки, области и др.), отрисовка в **`SKCanvas`** / растр через SkiaSharp. Целевой фреймворк: **.NET (net9.0)**. |
| **AI.Charts.WinForms** | Элементы **`ChartVisual`**, **`HeatMapControl`**, форма **`FormChart`**, пространство имён серий **`AI.Charts.WinForms`**. Только **Windows** (net*-windows). |
| **AI.Charts.Avalonia** | Контрол **`ChartViewControl`**, свойство **`Chart`** типа **`ChartView`** — для кроссплатформенных UI на Avalonia. |

Ядро не зависит от WinForms; визуализация данных опирается на типы **`AI.DataStructs.Algebraic.Vector`** и **`Matrix`**.

---

## 2. ChartView: серии и масштаб

Центральный тип — **`ChartView`** (`namespace AI.Charts`).

### 2.1. Подключение данных

Типичный сценарий — две оси задаются векторами **`Vector x`**, **`Vector y`** одинаковой длины.

Часто используемые методы:

| Метод | Описание |
|-------|----------|
| **`AddPlot(x, y, name, color?, width, isSpline)`** | Линия (или сглайн при `isSpline == true`). Если **`color`** не задан (`null`), цвет берётся из **встроенной палитры** по очереди. |
| **`AddPlotBlack(x, y, name, ...)`** | Устаревшее имя: фактически добавляет кривую со **следующим цветом палитры**, а не обязательно чёрным. Для явно чёрной линии передайте цвет: **`AddPlot(x, y, name, SKColors.Black)`**. |
| **`AddBar`**, **`AddScatter`**, **`AddArea`**, **`Clear`** | Столбцы, точки, область под кривой, сброс серий и сброс индекса палитры. |
| **`BarBlack` / `AddBarBlack`** | Аналогично столбцам с палитрой или явным цветом. |
| **`AutoScale()`** | Вызывается при добавлении серий из обёрток; подгоняет диапазоны осей к данным. |
| **`ToBitmap(width, height)`** | Растровое изображение графика (например для экспорта или отображения в Avalonia). |

Свойства подписей: **`ChartName`**, **`LabelX`**, **`LabelY`**; фон/текст осей: **`BackgroundColor`**, **`ForegroundColor`**.

### 2.2. Цвета без явного указания

Если **`SKColor?`** / цвет WinForms не передан, используется циклическая палитра (**`ChartSeriesPalette`** в сборке `AI.Charts`): синий, красный, зелёный, оранжевый и т.д. Счётчик сбрасывается при **`Clear()`**.

### 2.3. Легенда и порядок отрисовки

Легенда строится по сериям с непустым **`Name`**, цвет строки совпадает с **`ElementColor`** серии. Серии **линий** рисуются в **обратном** порядке добавления, чтобы **первая** добавленная серия была **сверху** и соответствовала **верхней** строке легенды. Легенда рисуется **после** кривых, с полупрозрачной **подложкой** и обрезкой длинного текста с многоточием.

---

## 3. WinForms: ChartVisual

Контрол **`ChartVisual`** содержит **`SkiaSharp.Views.Desktop.SKControl`** и дублирует API **`ChartView`** для удобства (в т.ч. перегрузки с **`System.Drawing.Color`**).

Подключение проекта:

```xml
<ProjectReference Include="..\..\src\AI.Charts\AI.Charts.csproj" />
<ProjectReference Include="..\..\src\AI.Charts.WinForms\AI.Charts.WinForms.csproj" />
```

Пример нескольких кривых с автоматическими цветами:

```csharp
chartVisual1.Clear();
chartVisual1.ChartName = "График";
chartVisual1.AddPlot(t, xNoise, "Исходные данные");           // цвет 1 палитры
chartVisual1.AddPlot(t1, xBinaryTest, "Сигнал без шума");     // цвет 2
chartVisual1.AddPlotBlack(pred);                              // цвет 3 (не чёрный по умолчанию)
```

Явный цвет (Skia):

```csharp
chartVisual1.AddPlot(t, y, "Серия", new SkiaSharp.SKColor(80, 80, 80), width: 2);
```

---

## 4. Тепловая карта: HeatMapControl

Класс **`HeatMapControl`** (`AI.Charts.WinForms`): метод **`CalculateHeatMap(Matrix matrix)`** или **`CalculateHeatMap(double[,])`**. Градиент и подписи min/max строятся по данным матрицы.

Используется в демо **`Tests/WorldModel`** вместе с **`ChartVisual`**.

---

## 5. Avalonia: ChartViewControl

1. Создайте экземпляр **`ChartView`** и заполните серии так же, как в п. 2.
2. Присвойте его свойству **`Chart`** элемента **`ChartViewControl`**.

Отрисовка идёт через **`ChartView.ToBitmap`** и отображение PNG в Avalonia (см. исходник **`ChartViewControl.cs`**).

---

## 6. Где смотреть код

- Ядро: `src/AI.Charts/Charts/ChartView.cs`, `Charts/Rendering/SkiaChartFrame.cs`
- Палитра: `Charts/Rendering/ChartSeriesPalette.cs`
- WinForms: `src/AI.Charts.WinForms/ChartVisual.cs`, `HeatMapControl.cs`
- Avalonia: `src/AI.Charts.Avalonia/ChartViewControl.cs`
