# Расстояние от точки до прямой

Задача вычисления расстояния от точки до прямой — одна из базовых в вычислительной геометрии.
Существуют компактные формулы для 2D и 3D случаев.
Результат используется в подгонке, коллизиях, snap-привязке и кластеризации.

## 2D: прямая задана уравнением

Прямая $ax + by + c = 0$, точка $P = (x_0, y_0)$:

$$d = \frac{|a\,x_0 + b\,y_0 + c|}{\sqrt{a^2 + b^2}}$$

### Прямая через две точки

Прямая через $A = (x_1, y_1)$ и $B = (x_2, y_2)$:

$$d = \frac{|(x_2 - x_1)(y_1 - y_0) - (x_1 - x_0)(y_2 - y_1)|}{\|B - A\|}$$

## 3D: прямая через точку и направление

Прямая проходит через точку $A$ с направлением $\hat{d}$, точка $P$:

$$d = \frac{\|(P - A) \times \hat{d}\|}{\|\hat{d}\|}$$

Если $\hat{d}$ уже нормализован, знаменатель равен 1.

### Ближайшая точка на прямой

$$t = \frac{(P - A) \cdot \hat{d}}{\hat{d} \cdot \hat{d}}, \quad Q = A + t\,\hat{d}$$

Для отрезка $[A, B]$ ограничьте $t \in [0, 1]$.

## Числовые замечания

- В 2D нормализуйте $(a, b)$ заранее, чтобы избежать деления на каждый вызов.
- Для больших массивов точек вычисляйте $1/\sqrt{a^2+b^2}$ один раз.
- При расстоянии до отрезка (не прямой) необходимо проверить проекцию: если $t < 0$ или $t > 1$, ближайшая точка — один из концов.

## API

Пространство имён `AI.Geometry.Distances`. Классов не один, а несколько — по типу пары объектов.

| Член | Описание |
|------|----------|
| `Line2D.FromTwoPoints(a, b)` | Прямая через две точки |
| `Line2D.FromGeneral(a, b, c)` | Из общего уравнения $ax + by + c = 0$ |
| `.ToGeneral()`, `.PointAt(t)` | Коэффициенты и точка на прямой |
| `PointLine.Distance2D(Vector point, Line2D line)` | Расстояние точка—прямая в 2D |
| `PointLine.Distance3D(Vector point, Line3D line)` | То же в 3D |
| `PointLine.ClosestPoint(Vector point, Line3D line)` | Ближайшая точка на прямой |
| `PointSegment.Distance(Vector point, Segment seg)` | Расстояние до **отрезка** |
| `PointSegment.ClosestPoint(point, seg)` | Ближайшая точка отрезка |
| `PointPlane.SignedDistance(point, plane)` | Знаковое расстояние до плоскости |

Отрезок и прямая различаются принципиально: у отрезка ближайшая точка может оказаться на конце, поэтому `PointSegment` — отдельный класс, а не режим `PointLine`.

Исходники: `src/AI.Geometry/Distances/`, `src/AI.Geometry/Primitives/`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Distances;
using AI.Geometry.Primitives;

var line = Line2D.FromTwoPoints(
    new Vector(new[] { 0.0, 0.0 }),
    new Vector(new[] { 4.0, 3.0 }));

var p = new Vector(new[] { 0.0, 5.0 });
Console.WriteLine($"Расстояние до прямой: {PointLine.Distance2D(p, line):F4}");   // 4

var (a, b, c) = line.ToGeneral();
Console.WriteLine($"Общее уравнение: {a:F2}x + {b:F2}y + {c:F2} = 0");
```

Точка «за концом» отрезка — тот случай, где формулы для прямой дают неверный ответ:

```csharp
var seg = new Segment(
    new Vector(new[] { 0.0, 0.0 }),
    new Vector(new[] { 2.0, 0.0 }));

var far = new Vector(new[] { 5.0, 0.0 });

// До бесконечной прямой — 0, до отрезка — 3: расстояние до его конца
Console.WriteLine($"До отрезка: {PointSegment.Distance(far, seg):F4}");

var closest = PointSegment.ClosestPoint(far, seg);
Console.WriteLine($"Ближайшая: ({closest[0]:F2}, {closest[1]:F2})");   // (2, 0)
```
