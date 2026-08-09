# AABB, OBB и теорема о разделяющей оси (SAT)

AABB (Axis-Aligned Bounding Box) — параллелепипед, выровненный по координатным осям.
OBB (Oriented Bounding Box) — произвольно ориентированный параллелепипед.
SAT (Separating Axis Theorem) — универсальный метод проверки пересечения выпуклых тел.

## AABB

Задаётся двумя точками: $\text{min} = (x_{\min}, y_{\min}, z_{\min})$, $\text{max} = (x_{\max}, y_{\max}, z_{\max})$.

### Проверка пересечения двух AABB

Боксы **не** пересекаются, если хотя бы по одной оси интервалы не перекрываются:

$$A_{\max,i} < B_{\min,i} \;\text{ или }\; B_{\max,i} < A_{\min,i}, \quad i \in \{x,y,z\}$$

### Slab-метод (луч–AABB)

Для каждой оси $i$ вычисляем параметры входа и выхода:

$$t_{\min,i} = \frac{\text{min}_i - O_i}{D_i}, \quad t_{\max,i} = \frac{\text{max}_i - O_i}{D_i}$$

$$t_{\text{enter}} = \max_i(\min(t_{\min,i}, t_{\max,i})), \quad t_{\text{exit}} = \min_i(\max(t_{\min,i}, t_{\max,i}))$$

Луч пересекает AABB, если $t_{\text{enter}} \leq t_{\text{exit}}$ и $t_{\text{exit}} \geq 0$.

## OBB

Задаётся центром $C$, тремя ортонормированными осями $u_0, u_1, u_2$ и полуразмерами $e_0, e_1, e_2$.

## SAT для OBB–OBB в 3D

Два выпуклых тела не пересекаются <-> существует разделяющая ось. Для двух OBB проверяются **15 осей**:

| Группа | Количество | Оси |
|--------|-----------|-----|
| Оси первого OBB | 3 | $u_0, u_1, u_2$ |
| Оси второго OBB | 3 | $v_0, v_1, v_2$ |
| Попарные кросс-произведения | 9 | $u_i \times v_j$ |

Для каждой оси проецируем оба OBB и проверяем перекрытие интервалов. Если хотя бы на одной оси перекрытия нет — тела не пересекаются.

## Числовые замечания

- При $D_i \approx 0$ в slab-методе используйте $t = \pm\infty$ вместо деления.
- Кросс-произведения $u_i \times v_j$ могут быть почти нулевыми при параллельных осях — проверяйте длину.

## API

Класса `BoundingBox` нет: примитивы лежат в `AI.Geometry.Primitives`, тесты пересечения — в `AI.Geometry.Intersections`, каждый отдельным статическим классом.

| Член | Описание |
|------|----------|
| `new Aabb(Vector Min, Vector Max)` | Осевой бокс |
| `.Center`, `.HalfExtents` | Центр и полуразмеры |
| `.Contains(Vector point)` | Точка внутри |
| `.Intersects(Aabb other)` | Пересечение с другим боксом (метод самого бокса) |
| `Aabb.FromPoints(points)` | Обёртка вокруг облака точек |
| `AabbAabbIntersection.Test(a, b)` | То же тестом-функцией |
| `RayAabbIntersection.Intersect(ray, box)` | `(double tMin, double tMax)?` — интервал вдоль луча |
| `new Obb(Vector Center, Vector HalfExtents, Matrix Rotation)` | Ориентированный бокс; `.Contains`, `.Corners()` |
| `ObbObbIntersection.Test(a, b)` | Тест по теореме о разделяющей оси |

Исходники: `src/AI.Geometry/Primitives/`, `src/AI.Geometry/Intersections/`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Intersections;
using AI.Geometry.Primitives;

var a = new Aabb(new Vector(new[] { 0.0, 0.0 }), new Vector(new[] { 2.0, 2.0 }));
var b = new Aabb(new Vector(new[] { 1.5, 1.5 }), new Vector(new[] { 3.0, 3.0 }));
var c = new Aabb(new Vector(new[] { 5.0, 5.0 }), new Vector(new[] { 6.0, 6.0 }));

Console.WriteLine($"a ∩ b: {AabbAabbIntersection.Test(a, b)}");   // true
Console.WriteLine($"a ∩ c: {AabbAabbIntersection.Test(a, c)}");   // false

// Обёртка вокруг облака точек — типовой шаг построения BVH
var cloud = new[]
{
    new Vector(new[] { 1.0, 4.0 }),
    new Vector(new[] { -2.0, 0.5 }),
    new Vector(new[] { 3.0, 2.0 }),
};
var box = Aabb.FromPoints(cloud);
Console.WriteLine($"AABB: [{box.Min[0]}, {box.Min[1]}] .. [{box.Max[0]}, {box.Max[1]}]");
```

Луч против бокса — базовый запрос трассировки; метод отдаёт интервал, а не одну точку:

```csharp
var ray = new Ray(
    Origin:    new Vector(new[] { -5.0, 1.0, 1.0 }),
    Direction: new Vector(new[] {  1.0, 0.0, 0.0 }));

var box3 = new Aabb(
    new Vector(new[] { 0.0, 0.0, 0.0 }),
    new Vector(new[] { 2.0, 2.0, 2.0 }));

var hit = RayAabbIntersection.Intersect(ray, box3);
if (hit.HasValue)
{
    var (tMin, tMax) = hit.Value;
    Console.WriteLine($"Вход t={tMin:F2}, выход t={tMax:F2}");
}
else
{
    Console.WriteLine("Луч проходит мимо бокса");
}
```
