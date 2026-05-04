# 3D-графики — AI.Charts

Библиотека **AI.Charts** поддерживает три типа 3D-графиков, рендеримых через **SkiaSharp**
с ортографической проекцией. Никаких внешних 3D-библиотек не требуется.

## Типы графиков

| Тип | Метод API | Данные |
|-----|-----------|--------|
| **Surface** (заливка) | `ChartView.AddSurface` | Сетка $x$, $y$, матрица $Z[i,j]$ |
| **Wireframe** (каркас) | `ChartView.AddWireframe` | Сетка $x$, $y$, матрица $Z[i,j]$ |
| **Scatter 3D** (облако) | `ChartView.AddScatter3D` | Три вектора $x, y, z$ |

## Быстрый старт

```csharp
using AI.Charts;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;

var cv = new ChartView();

// Сетка 40×40
var x = Vector.Seq(-3, 0.15, 3);
var y = Vector.Seq(-3, 0.15, 3);
var z = new double[x.Count, y.Count];

for (int i = 0; i < x.Count; i++)
for (int j = 0; j < y.Count; j++)
    z[i, j] = Math.Sin(x[i]) * Math.Cos(y[j]);

cv.AddSurface(x, y, z, "sin(x)·cos(y)");

// Настройка камеры
cv.Camera3D.Azimuth   = 45;   // градусы, горизонтальный поворот
cv.Camera3D.Elevation = 30;   // градусы, наклон
cv.Camera3D.Distance  = 2.5;  // масштаб (больше = мельче)

// Получение растрового изображения
using var bmp = cv.ToBitmap(800, 600);
```

## Камера (`Camera3D`)

| Свойство | Диапазон | По умолчанию | Описание |
|----------|----------|--------------|----------|
| `Azimuth` | 0 — 360° | 45 | Горизонтальный поворот вокруг вертикальной оси |
| `Elevation` | −89 — 89° | 30 | Наклон камеры (положительный — сверху) |
| `Distance` | > 0 | 2.5 | Масштаб (ортографическая проекция) |

Камера автоматически центрируется по bounding box данных при вызове `AddSurface` / `AddWireframe` / `AddScatter3D`.

## Палитры (`ColormapKind`)

Цвет граней / точек определяется значением $Z$, нормированным в $[0, 1]$.

| Палитра | Описание |
|---------|----------|
| `Jet` | Классический: синий → голубой → зелёный → жёлтый → красный |
| `Viridis` | Перцептивно-равномерный: фиолетовый → зелёный → жёлтый |
| `Thermal` | Тёплый: тёмно-синий → бирюзовый → жёлтый → красный |
| `Grayscale` | Чёрный → белый |

```csharp
cv.AddSurface(x, y, z, "peaks", ColormapKind.Viridis);
```

## Wireframe

```csharp
// С colormap (цвет по Z)
cv.AddWireframe(x, y, z, "Параболоид");

// Однотонный
cv.AddWireframe(x, y, z, "Параболоид", color: new SKColor(0, 180, 120));
```

## Scatter 3D

```csharp
var xs = new Vector(n);
var ys = new Vector(n);
var zs = new Vector(n);
// ... заполнение ...

cv.AddScatter3D(xs, ys, zs, "Облако", markSize: 5f);
```

## Интерактивность

### Avalonia (`ChartViewControl`)

Перетаскивание левой кнопкой мыши вращает камеру (azimuth / elevation).
Колёсико мыши изменяет `Distance`.

### WinForms (`ChartVisual`)

Аналогично: перетаскивание в 3D-режиме вращает камеру, колёсико масштабирует.

### Web (Blazor demo)

Углы камеры задаются слайдерами параметров `azimuth` и `elevation`.

## Surface с overlay wireframe

По умолчанию поверхность отрисовывается с тонкими рёбрами (`ShowEdges = true`).
Отключить:

```csharp
cv.AddSurface(x, y, z, "Без рёбер", showEdges: false);
```

## Painter's Algorithm

Грани (Surface) и точки (Scatter3D) сортируются по глубине от камеры и рисуются
back-to-front. Это обеспечивает корректное перекрытие без Z-буфера.
