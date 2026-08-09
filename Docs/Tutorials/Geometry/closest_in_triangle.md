# Ближайшая точка в треугольнике

Задача: найти точку внутри треугольника (или на его границе), ближайшую к заданной точке $P$.
Решение основано на барицентрической проекции с последующей классификацией по зонам Вороного.
Используется в коллизиях, вычислении расстояний и физических симуляциях.

## Барицентрические координаты

Точка $Q$ внутри треугольника $V_0 V_1 V_2$:

$$Q = (1 - u - v)\,V_0 + u\,V_1 + v\,V_2, \quad u \geq 0,\; v \geq 0,\; u + v \leq 1$$

## Алгоритм

1. Вычислить проекцию $P$ на плоскость треугольника → барицентрические координаты $(u, v)$.
2. Если $(u, v)$ удовлетворяет ограничениям — проекция лежит внутри, это и есть ответ.
3. Иначе — определить ближайшую область (зону Вороного).

## Зоны Вороного

Пространство разбивается на 7 зон:

| Зона | Ближайший элемент | Условие |
|------|-------------------|---------|
| Внутренность | Проекция на плоскость | $u \geq 0, v \geq 0, u+v \leq 1$ |
| Вершина $V_0$ | $V_0$ | $u < 0, v < 0$ |
| Вершина $V_1$ | $V_1$ | $u > 1$ |
| Вершина $V_2$ | $V_2$ | $v > 1$ |
| Ребро $V_0 V_1$ | Проекция на ребро | $v < 0, 0 \leq u \leq 1$ |
| Ребро $V_0 V_2$ | Проекция на ребро | $u < 0, 0 \leq v \leq 1$ |
| Ребро $V_1 V_2$ | Проекция на ребро | $u + v > 1$ |

Проекция на ребро — ограниченная (clamp) проекция точки на отрезок.

## Реализация через скалярные произведения

Обозначим $E_0 = V_1 - V_0$, $E_1 = V_2 - V_0$, $D = V_0 - P$:

$$a = E_0 \cdot E_0, \quad b = E_0 \cdot E_1, \quad c = E_1 \cdot E_1$$
$$d = E_0 \cdot D, \quad e = E_1 \cdot D$$

$$\det = ac - b^2, \quad u = (be - cd)/\det, \quad v = (bd - ae)/\det$$

Далее проверяются зоны и при необходимости выполняется clamp.

## Числовые замечания

- $\det = 0$ означает вырожденный треугольник (нулевая площадь).
- Алгоритм branchless-реализуем для SIMD.

## API

Ближайшая точка ищется **статическими классами**, а не методом самого треугольника; их два, с разными сигнатурами.

| Член | Описание |
|------|----------|
| `AI.Geometry.Polygons.ClosestInTriangle.ClosestPoint(Vector p, Vector a, Vector b, Vector c)` | Вершины отдельными аргументами |
| `AI.Geometry.Distances.PointTriangle.ClosestPoint(Vector p, Triangle tri)` | Через примитив `Triangle` |
| `AI.Geometry.Distances.PointTriangle.Distance(Vector p, Triangle tri)` | Сразу расстояние |
| `Triangle.BarycentricCoords(Vector p)` | `(u, v, w)` — область проекции читается по знакам |

Исходники: `src/AI.Geometry/Polygons/ClosestInTriangle.cs`, `src/AI.Geometry/Distances/PointTriangle.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Distances;
using AI.Geometry.Polygons;
using AI.Geometry.Primitives;

var a = new Vector(new[] { 1.0, 1.0 });
var b = new Vector(new[] { 5.0, 1.0 });
var c = new Vector(new[] { 3.0, 4.5 });

// Точка снаружи: проекция попадёт на ребро или в вершину
var p = new Vector(new[] { 6.0, 0.0 });
var closest = ClosestInTriangle.ClosestPoint(p, a, b, c);
Console.WriteLine($"Ближайшая: ({closest[0]:F3}, {closest[1]:F3})");

// Точка внутри проецируется сама в себя
var inner = new Vector(new[] { 3.0, 2.0 });
var same = ClosestInTriangle.ClosestPoint(inner, a, b, c);
Console.WriteLine($"Внутри без изменений: {Math.Abs(same[0] - inner[0]) < 1e-9}");
```

Через примитив `Triangle` доступно и расстояние, и барицентрические координаты — по ним видно, в какую из семи областей попала точка:

```csharp
var tri = new Triangle(a, b, c);
Console.WriteLine($"Расстояние: {PointTriangle.Distance(p, tri):F4}");

var (u, v, w) = tri.BarycentricCoords(p);
Console.WriteLine($"u={u:F3} v={v:F3} w={w:F3}");
// Отрицательная координата означает, что точка лежит за противоположным ребром
Console.WriteLine(u >= 0 && v >= 0 && w >= 0 ? "внутри" : "снаружи");
```
