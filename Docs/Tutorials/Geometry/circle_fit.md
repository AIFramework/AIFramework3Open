# Подгонка окружности

Задача: по набору точек на плоскости найти окружность $(x - a)^2 + (y - b)^2 = R^2$, наилучшим образом описывающую данные.
Метод Kåsa даёт алгебраическое решение в замкнутой форме.
RANSAC обеспечивает робастность при наличии выбросов.

## Метод Kåsa (алгебраический)

Перепишем уравнение окружности:

$$x^2 + y^2 + Dx + Ey + F = 0$$

где $a = -D/2$, $b = -E/2$, $R = \sqrt{a^2 + b^2 - F}$.

### Система нормальных уравнений

Минимизируем $\sum_i(x_i^2 + y_i^2 + Dx_i + Ey_i + F)^2$. Линеаризация даёт систему $3 \times 3$:

$$\begin{pmatrix} \sum x_i^2 & \sum x_i y_i & \sum x_i \\ \sum x_i y_i & \sum y_i^2 & \sum y_i \\ \sum x_i & \sum y_i & n \end{pmatrix} \begin{pmatrix} D \\ E \\ F \end{pmatrix} = -\begin{pmatrix} \sum x_i(x_i^2+y_i^2) \\ \sum y_i(x_i^2+y_i^2) \\ \sum (x_i^2+y_i^2) \end{pmatrix}$$

### Смещение метода

Метод Kåsa минимизирует алгебраическое, а не геометрическое расстояние. Для данных, занимающих малую дугу, результат может быть смещён. Для высокой точности используйте итеративные методы (Taubin, Levenberg–Marquardt).

## RANSAC для окружности

1. Случайно выбрать **3** неколлинеарных точки → построить окружность.
2. Подсчитать инлайеры ($|d_i - R| < \delta$).
3. Повторить $N$ раз; взять лучшую модель.
4. Переподогнать по инлайерам (Kåsa или итеративно).

### Число итераций

$$N = \frac{\ln(1 - p)}{\ln(1 - w^3)}$$

## Числовые замечания

- Центрируйте данные перед подгонкой для улучшения обусловленности.
- $R^2 = a^2 + b^2 - F$ должно быть положительным; отрицательное значение сигнализирует о плохих данных.
- Минимум 3 неколлинеарных точки для определения окружности.

## API

Пространство имён `AI.Geometry.Fitting`, статический класс `CircleFit`. Метод Kåsa называется `AlgebraicFit`, а не `FitKasa`.

| Член | Описание |
|------|----------|
| `CircleFit.AlgebraicFit(Vector[] points)` | `Circle` — метод Kåsa через линейную систему |
| `CircleFit.Ransac(points, iterations = 500, threshold = 1.0, Random rng = null)` | `(Circle circle, bool[] inliers)` |
| `Circle` | `record Circle(Vector Center, double Radius)`; `.Area`, `.Circumference`, `.Contains(point)` |

`Ransac` может вернуть `circle == null`, если за отведённые итерации не нашлось согласованной модели, — это стоит проверять.

Исходники: `src/AI.Geometry/Fitting/CircleFit.cs`, `src/AI.Geometry/Primitives/Circle.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Fitting;

var rng = new Random(42);
var points = new Vector[50];

// 5 выбросов + 45 точек на окружности радиуса 2 с центром (3, 3)
for (int i = 0; i < points.Length; i++)
{
    if (i < 5)
    {
        points[i] = new Vector(new[] { rng.NextDouble() * 6, rng.NextDouble() * 6 });
        continue;
    }

    double th = rng.NextDouble() * 2 * Math.PI;
    double r = 2 + (rng.NextDouble() - 0.5) * 0.3;
    points[i] = new Vector(new[] { 3 + r * Math.Cos(th), 3 + r * Math.Sin(th) });
}

var kasa = CircleFit.AlgebraicFit(points);
Console.WriteLine($"Kåsa:   центр ({kasa.Center[0]:F3}, {kasa.Center[1]:F3}), R = {kasa.Radius:F3}");

var (circle, inliers) = CircleFit.Ransac(points, iterations: 500, threshold: 0.6, rng);

if (circle is null)
{
    Console.WriteLine("RANSAC не нашёл согласованной модели");
}
else
{
    Console.WriteLine($"RANSAC: центр ({circle.Center[0]:F3}, {circle.Center[1]:F3}), R = {circle.Radius:F3}");
    Console.WriteLine($"Инлаеров: {inliers.Count(v => v)} из {points.Length}");
    Console.WriteLine($"Площадь: {circle.Area:F3}, длина: {circle.Circumference:F3}");
}

// Kåsa минимизирует алгебраическую невязку и смещается к выбросам,
// RANSAC отбрасывает их и попадает точнее в (3, 3) с R = 2
```
