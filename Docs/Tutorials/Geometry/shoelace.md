# Площадь полигона (формула шнурков)

Формула шнурков (Shoelace formula) — простой и эффективный способ вычисления площади простого (без самопересечений) полигона.
Работает для любого полигона, заданного упорядоченной последовательностью вершин.
Сложность — $O(n)$, где $n$ — число вершин.

## Формула

Для полигона с вершинами $(x_1, y_1), (x_2, y_2), \ldots, (x_n, y_n)$, где $(x_{n+1}, y_{n+1}) = (x_1, y_1)$:

$$S = \frac{1}{2}\left|\sum_{i=1}^{n}(x_i\,y_{i+1} - x_{i+1}\,y_i)\right|$$

### Развёрнутая форма

$$S = \frac{1}{2}|x_1(y_2 - y_n) + x_2(y_3 - y_1) + \cdots + x_n(y_1 - y_{n-1})|$$

## Знак и ориентация

Без модуля знаковая площадь:

$$S_{\text{signed}} = \frac{1}{2}\sum_{i=1}^{n}(x_i\,y_{i+1} - x_{i+1}\,y_i)$$

- $S_{\text{signed}} > 0$ — вершины обходятся **против часовой стрелки**.
- $S_{\text{signed}} < 0$ — **по часовой стрелке**.

## Центроид полигона

Используя ту же сумму, координаты центроида:

$$C_x = \frac{1}{6S}\sum_{i=1}^{n}(x_i + x_{i+1})(x_i y_{i+1} - x_{i+1} y_i)$$

$$C_y = \frac{1}{6S}\sum_{i=1}^{n}(y_i + y_{i+1})(x_i y_{i+1} - x_{i+1} y_i)$$

## Числовые замечания

- Для координат большой величины используйте сдвиг: вычтите центр масс перед суммированием для снижения ошибки округления.
- Формула корректна только для **простых** полигонов (без самопересечений).

## API

Пространство имён `AI.Geometry.Polygons`. Единого класса `Polygon` нет — площадь и центроид считают разные статические классы, полигон передаётся массивом `Vector[]`.

| Член | Описание |
|------|----------|
| `ShoelaceArea.SignedArea(Vector[] polygon)` | Знаковая площадь: > 0 — обход против часовой, < 0 — по часовой |
| `ShoelaceArea.Area(Vector[] polygon)` | Модуль знаковой площади |
| `PolygonCentroid.Centroid(Vector[] polygon)` | Центроид (центр масс площади) |
| `Orientation2D.Orient(a, b, c)` | Ориентация тройки: `+1`, `−1` или `0` |

Полигон задаётся вершинами по порядку и **не замыкается**: повторять первую вершину в конце не нужно.

Исходники: `src/AI.Geometry/Polygons/`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Polygons;

// Единичный квадрат, обход против часовой стрелки
var ccw = new[]
{
    new Vector(new[] { 0.0, 0.0 }),
    new Vector(new[] { 2.0, 0.0 }),
    new Vector(new[] { 2.0, 2.0 }),
    new Vector(new[] { 0.0, 2.0 }),
};

Console.WriteLine($"Знаковая площадь: {ShoelaceArea.SignedArea(ccw):F4}");   // +4
Console.WriteLine($"Площадь:          {ShoelaceArea.Area(ccw):F4}");        //  4

// Тот же полигон в обратном обходе: знак меняется, модуль нет
var cw = ccw.Reverse().ToArray();
Console.WriteLine($"Обход по часовой: {ShoelaceArea.SignedArea(cw):F4}");   // −4

var c = PolygonCentroid.Centroid(ccw);
Console.WriteLine($"Центроид: ({c[0]:F3}, {c[1]:F3})");                     // (1, 1)
```

Знак площади — самый дешёвый способ определить ориентацию контура, что важно при триангуляции и заполнении:

```csharp
bool isCounterClockwise = ShoelaceArea.SignedArea(ccw) > 0;
Console.WriteLine($"Против часовой: {isCounterClockwise}");

// Невыпуклый L-образный полигон считается той же формулой
var lShape = new[]
{
    new Vector(new[] { 0.0, 0.0 }), new Vector(new[] { 3.0, 0.0 }),
    new Vector(new[] { 3.0, 1.0 }), new Vector(new[] { 1.0, 1.0 }),
    new Vector(new[] { 1.0, 3.0 }), new Vector(new[] { 0.0, 3.0 }),
};
Console.WriteLine($"Площадь L: {ShoelaceArea.Area(lShape):F4}");   // 5
```
