# Пересечение луча с треугольником (Möller–Trumbore)

Алгоритм Мёллера–Трумбора — быстрый метод проверки пересечения луча с треугольником.
Не требует предварительного вычисления плоскости треугольника.
Возвращает параметр $t$ вдоль луча и барицентрические координаты $(u, v)$ точки пересечения.

## Постановка

Луч: $R(t) = O + t\,D$, $t > 0$.

Треугольник с вершинами $V_0, V_1, V_2$. Точка внутри треугольника:

$$P = (1 - u - v)\,V_0 + u\,V_1 + v\,V_2, \quad u \geq 0,\; v \geq 0,\; u + v \leq 1$$

## Алгоритм

Обозначим:

$$E_1 = V_1 - V_0, \quad E_2 = V_2 - V_0, \quad T = O - V_0$$

$$P = D \times E_2, \quad Q = T \times E_1$$

$$\det = P \cdot E_1$$

Если $|\det| < \varepsilon$, луч параллелен плоскости треугольника.

$$t = \frac{Q \cdot E_2}{\det}, \quad u = \frac{P \cdot T}{\det}, \quad v = \frac{Q \cdot D}{\det}$$

### Условия пересечения

1. $u \geq 0$
2. $v \geq 0$
3. $u + v \leq 1$
4. $t > 0$ (пересечение впереди начала луча)

## Вычислительная сложность

- 1 кросс-произведение, 2 скалярных произведения для раннего отсечения.
- Всего: ~27 умножений + ~17 сложений.

## Числовые замечания

- Порог $\varepsilon \sim 10^{-8}$ для `double`.
- Для односторонней проверки (backface culling) отбрасывайте случай $\det < 0$.

## API

Пространство имён `AI.Geometry.Intersections`; класс называется `RayTriangleIntersection`. Никаких `out`-параметров: метод возвращает `double?` — параметр $t$ вдоль луча или `null`, если пересечения нет. Барицентрические координаты получают отдельно, у самого треугольника.

| Член | Описание |
|------|----------|
| `new Ray(Vector Origin, Vector Direction)` | Луч; `.PointAt(t)` — точка на нём |
| `new Triangle(Vector A, Vector B, Vector C)` | Треугольник |
| `.Area()`, `.Normal()`, `.Centroid` | Площадь, нормаль, центроид |
| `.BarycentricCoords(Vector p)` | `(double u, double v, double w)` |
| `RayTriangleIntersection.Intersect(Ray, Triangle)` | `double?` — расстояние вдоль луча |

Исходники: `src/AI.Geometry/Intersections/RayTriangleIntersection.cs`, `Primitives/Triangle.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Intersections;
using AI.Geometry.Primitives;

var tri = new Triangle(
    new Vector(new[] { 1.0, 1.0, 0.0 }),
    new Vector(new[] { 4.0, 1.0, 0.0 }),
    new Vector(new[] { 2.5, 4.0, 0.0 }));

// Луч летит вдоль +Z и должен пробить треугольник
var ray = new Ray(
    Origin:    new Vector(new[] { 2.5, 2.0, -1.0 }),
    Direction: new Vector(new[] { 0.0, 0.0,  1.0 }));

double? t = RayTriangleIntersection.Intersect(ray, tri);

if (t is null)
{
    Console.WriteLine("Промах");
}
else
{
    var hit = ray.PointAt(t.Value);
    Console.WriteLine($"t = {t.Value:F4}, точка ({hit[0]:F2}, {hit[1]:F2}, {hit[2]:F2})");

    // Барицентрические координаты: все три в [0,1] — точка внутри
    var (u, v, w) = tri.BarycentricCoords(hit);
    Console.WriteLine($"u={u:F3} v={v:F3} w={w:F3}, сумма={u + v + w:F6}");
}
```
