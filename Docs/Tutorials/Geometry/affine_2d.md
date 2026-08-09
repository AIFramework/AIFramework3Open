# Аффинные преобразования 2D

Аффинное преобразование — линейное отображение с последующим переносом.
В однородных координатах любое аффинное преобразование записывается как умножение на матрицу 3×3.
Композиция преобразований — произведение матриц; порядок умножения важен.

## Однородные координаты

Точка $(x, y)$ записывается как $(x, y, 1)^T$. Преобразование:

$$\begin{pmatrix} x' \\ y' \\ 1 \end{pmatrix} = M \begin{pmatrix} x \\ y \\ 1 \end{pmatrix}$$

## Базовые преобразования

### Перенос (Translation)

$$T(t_x, t_y) = \begin{pmatrix} 1 & 0 & t_x \\ 0 & 1 & t_y \\ 0 & 0 & 1 \end{pmatrix}$$

### Масштаб (Scale)

$$S(s_x, s_y) = \begin{pmatrix} s_x & 0 & 0 \\ 0 & s_y & 0 \\ 0 & 0 & 1 \end{pmatrix}$$

### Поворот (Rotation) на угол $\theta$

$$R(\theta) = \begin{pmatrix} \cos\theta & -\sin\theta & 0 \\ \sin\theta & \cos\theta & 0 \\ 0 & 0 & 1 \end{pmatrix}$$

### Сдвиг (Shear)

$$H(h_x, h_y) = \begin{pmatrix} 1 & h_x & 0 \\ h_y & 1 & 0 \\ 0 & 0 & 1 \end{pmatrix}$$

## Композиция

Поворот вокруг точки $(c_x, c_y)$:

$$M = T(c_x, c_y) \cdot R(\theta) \cdot T(-c_x, -c_y)$$

Матрицы умножаются справа налево: сначала перенос в начало, затем поворот, затем перенос обратно.

## Обратное преобразование

$$M^{-1} \text{ существует, если } \det(M) \neq 0$$

Для чистого поворота: $R^{-1}(\theta) = R(-\theta) = R^T$.

## API

Пространство имён `AI.Geometry.Transforms`. Класс называется `Affine2D`; фабрики — существительные (`Translation`, `Rotation`), а не глаголы.

| Член | Описание |
|------|----------|
| `Affine2D.Identity()` | Тождественное преобразование |
| `Affine2D.Translation(dx, dy)` | Перенос |
| `Affine2D.Scale(sx, sy)` | Масштабирование |
| `Affine2D.Rotation(angle)` | Поворот на угол в радианах |
| `Affine2D.Shear(shx, shy)` | Сдвиг |
| `.Compose(Affine2D other)` | Композиция |
| `.Inverse()` | Обратное преобразование |
| `.Apply(Vector point)` | Применить к точке |
| `.M` | Матрица 3×3 в однородных координатах |

Для 3D есть `Affine3D` с `RotationX/Y/Z` и `FromQuaternion`.

Исходник: `src/AI.Geometry/Transforms/Affine2D.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Geometry.Transforms;

// Порядок Compose важен: сначала поворот, затем масштаб, затем перенос
var xf = Affine2D.Rotation(Math.PI / 6)
    .Compose(Affine2D.Scale(2, 2))
    .Compose(Affine2D.Translation(1, 0.5));

var square = new[]
{
    new Vector(new[] { 0.0, 0.0 }),
    new Vector(new[] { 1.0, 0.0 }),
    new Vector(new[] { 1.0, 1.0 }),
    new Vector(new[] { 0.0, 1.0 }),
};

foreach (var p in square)
{
    var t = xf.Apply(p);
    Console.WriteLine($"({p[0]:F1}, {p[1]:F1}) -> ({t[0]:F3}, {t[1]:F3})");
}

// Обратное преобразование возвращает исходные координаты
var inv = xf.Inverse();
var back = inv.Apply(xf.Apply(square[2]));
Console.WriteLine($"Восстановлено: ({back[0]:F6}, {back[1]:F6})");
```
