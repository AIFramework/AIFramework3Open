# Подгонка прямой: OLS, TLS, RANSAC

Задача подгонки прямой по набору точек — фундаментальная в статистике и компьютерном зрении.
OLS минимизирует вертикальные отклонения, TLS — ортогональные, RANSAC — робастен к выбросам.
Выбор метода зависит от характера шума и наличия выбросов.

## OLS (Ordinary Least Squares)

Модель $y = kx + b$. Минимизируем:

$$\sum_{i=1}^{n}(y_i - k\,x_i - b)^2 \to \min$$

Решение:

$$k = \frac{n\sum x_i y_i - \sum x_i \sum y_i}{n\sum x_i^2 - (\sum x_i)^2}, \quad b = \bar{y} - k\,\bar{x}$$

**Ограничение**: не работает для вертикальных прямых ($k \to \infty$).

## TLS (Total Least Squares)

Минимизирует ортогональные расстояния до прямой $ax + by + c = 0$, $a^2 + b^2 = 1$.

### Алгоритм через PCA

1. Вычислить центроид $(\bar{x}, \bar{y})$.
2. Построить ковариационную матрицу центрированных данных.
3. Направление прямой — собственный вектор, соответствующий **наибольшему** собственному числу.
4. Нормаль — собственный вектор наименьшего собственного числа.

**Преимущество**: работает для любой ориентации прямой.

## RANSAC (Random Sample Consensus)

Робастный метод при наличии выбросов (до 50%).

### Алгоритм

1. Случайно выбрать 2 точки → построить прямую.
2. Подсчитать число инлайеров (точки ближе порога $\delta$).
3. Повторить $N$ раз.
4. Взять прямую с наибольшим числом инлайеров.
5. Переподогнать (OLS/TLS) по инлайерам.

### Число итераций

$$N = \frac{\ln(1 - p)}{\ln(1 - w^s)}$$

где $p$ — желаемая вероятность успеха, $w$ — доля инлайеров, $s = 2$ — размер выборки.

## Числовые замечания

- OLS: при $\sum x_i^2 - n\bar{x}^2 \approx 0$ прямая почти вертикальна — используйте TLS.
- RANSAC: порог $\delta$ влияет на результат; выбирайте исходя из ожидаемого уровня шума.

## API

Пространство имён `AI.Geometry.Fitting`, статический класс `LineFit`. Методы называются без префикса `Fit`, и — важно — **возвращают разное**: OLS и RANSAC дают коэффициенты прямой, а TLS — точку и направление, потому что вертикальную прямую наклоном не описать.

| Член | Описание |
|------|----------|
| `LineFit.Ols(Vector[] points)` | `(double slope, double intercept)` — минимум по вертикали |
| `LineFit.Tls(Vector[] points)` | `(Vector direction, Vector point)` — минимум по перпендикуляру |
| `LineFit.Ransac(Vector[] points, int iterations, double threshold, Random rng)` | `(double slope, double intercept, bool[] inliers)` |

Исходник: `src/AI.Geometry/Fitting/LineFit.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Fitting;

var rng = new Random(42);
var points = new Vector[60];

// Первые 9 точек — выбросы, остальные лежат на прямой y = 1.5x + 0.5
for (int i = 0; i < points.Length; i++)
{
    double x = rng.NextDouble() * 6 - 1;
    double y = i < 9
        ? rng.NextDouble() * 10 - 3
        : 1.5 * x + 0.5 + (rng.NextDouble() - 0.5) * 0.6;
    points[i] = new Vector(new[] { x, y });
}

var ols = LineFit.Ols(points);
Console.WriteLine($"OLS:    y = {ols.slope:F3}x + {ols.intercept:F3}");

var ransac = LineFit.Ransac(points, iterations: 500, threshold: 0.9, rng);
Console.WriteLine($"RANSAC: y = {ransac.slope:F3}x + {ransac.intercept:F3}");
Console.WriteLine($"Инлаеров: {ransac.inliers.Count(v => v)} из {points.Length}");

// OLS «утягивают» 9 выбросов, RANSAC их игнорирует и попадает в 1.5 / 0.5
```

TLS минимизирует перпендикулярные расстояния и потому симметричен по осям — в отличие от OLS, где x считается точным:

```csharp
var tls = LineFit.Tls(points);
Console.WriteLine($"TLS: точка ({tls.point[0]:F3}, {tls.point[1]:F3}), " +
                  $"направление ({tls.direction[0]:F3}, {tls.direction[1]:F3})");

// Перевод в наклон возможен, только если направление не вертикально
if (Math.Abs(tls.direction[0]) > 1e-12)
{
    double k = tls.direction[1] / tls.direction[0];
    double b = tls.point[1] - k * tls.point[0];
    Console.WriteLine($"TLS: y = {k:F3}x + {b:F3}");
}
```
