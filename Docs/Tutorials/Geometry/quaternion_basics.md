# Кватернионы

Кватернионы — расширение комплексных чисел для представления вращений в 3D.
В отличие от углов Эйлера, они не подвержены шарнирному замку (gimbal lock).
Кватернион компактнее матрицы вращения (4 числа против 9) и легко интерполируется.

## Определение

$$q = w + x\,i + y\,j + z\,k$$

где $i^2 = j^2 = k^2 = ijk = -1$.

Сокращённая запись: $q = (w, \mathbf{v})$, где $\mathbf{v} = (x, y, z)$.

## Основные операции

### Умножение

$$q_1 q_2 = (w_1 w_2 - \mathbf{v}_1 \cdot \mathbf{v}_2,\; w_1 \mathbf{v}_2 + w_2 \mathbf{v}_1 + \mathbf{v}_1 \times \mathbf{v}_2)$$

Умножение **некоммутативно**: $q_1 q_2 \neq q_2 q_1$.

### Сопряжение

$$q^* = w - x\,i - y\,j - z\,k = (w, -\mathbf{v})$$

### Норма

$$\|q\| = \sqrt{w^2 + x^2 + y^2 + z^2}$$

Для единичного кватерниона $\|q\| = 1$, и тогда $q^{-1} = q^*$.

## Вращение точки

Точку $p = (p_x, p_y, p_z)$ представляем как чистый кватернион $\mathbf{p} = (0, p)$. Повёрнутая точка:

$$\mathbf{p}' = q\,\mathbf{p}\,q^*$$

Кватернион вращения на угол $\theta$ вокруг единичной оси $\hat{u}$:

$$q = \left(\cos\frac{\theta}{2},\; \hat{u}\,\sin\frac{\theta}{2}\right)$$

## Slerp кватернионов

$$\text{slerp}(q_0, q_1, t) = q_0\,(q_0^{-1}\,q_1)^t = \frac{\sin((1-t)\theta)}{\sin\theta}\,q_0 + \frac{\sin(t\theta)}{\sin\theta}\,q_1$$

При $q_0 \cdot q_1 < 0$ инвертируйте один из кватернионов для кратчайшего пути.

## Числовые замечания

- Периодически перенормализуйте: $q \leftarrow q / \|q\|$, чтобы избежать дрейфа.
- Переход к матрице вращения 3×3 выполняется без тригонометрии.

## API

Пространство имён `AI.Geometry.Transforms`. `Quaternion` — **структура** (`readonly struct`), а не класс; умножение задано оператором `*`, а `Conjugate` и `Normalize` — свойства, а не методы.

| Член | Описание |
|------|----------|
| `new Quaternion(w, x, y, z)` | Компоненты; поля `W`, `X`, `Y`, `Z` доступны только для чтения |
| `Quaternion.Identity` | Единичный кватернион (без поворота) |
| `Quaternion.FromAxisAngle(Vector axis, double angle)` | Поворот вокруг оси на угол в радианах |
| `Quaternion.FromEuler(yaw, pitch, roll)` | Из углов Эйлера |
| `Quaternion.FromRotationMatrix(Matrix m)` | Из матрицы поворота |
| `a * b` | Композиция поворотов (оператор, не `Multiply`) |
| `.Conjugate`, `.Inverse`, `.Normalize` | **Свойства**, возвращают новый кватернион |
| `.Norm` | Длина |
| `.Rotate(Vector point)` | Повернуть точку (не `RotatePoint`) |
| `.ToRotationMatrix3()` | Матрица 3×3 (не `ToMatrix`) |
| `Quaternion.Slerp(a, b, t)` | Сферическая интерполяция ориентаций |

Исходник: `src/AI.Geometry/Transforms/Quaternion.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using Quaternion = AI.Geometry.Transforms.Quaternion;

var axis = new Vector(new[] { 0.0, 0.0, 1.0 });
var q90  = Quaternion.FromAxisAngle(axis, Math.PI / 2);

var point   = new Vector(new[] { 1.0, 0.0, 0.0 });
var rotated = q90.Rotate(point);
Console.WriteLine($"[{rotated[0]:F3}, {rotated[1]:F3}, {rotated[2]:F3}]");   // [0, 1, 0]

// Композиция: два поворота по 90° дают 180°
var q180 = q90 * q90;
var back = q180.Rotate(point);
Console.WriteLine($"[{back[0]:F3}, {back[1]:F3}, {back[2]:F3}]");            // [-1, 0, 0]

// Обратный поворот возвращает точку на место
var restored = q90.Inverse.Rotate(rotated);
Console.WriteLine($"[{restored[0]:F3}, {restored[1]:F3}, {restored[2]:F3}]");
```

Slerp по кватернионам даёт равномерное вращение — в отличие от покомпонентной интерполяции углов Эйлера, страдающей шарнирным замком:

```csharp
for (int i = 0; i <= 4; i++)
{
    double t = i / 4.0;
    var qi = Quaternion.Slerp(Quaternion.Identity, q90, t);
    var pi = qi.Rotate(point);
    Console.WriteLine($"t={t:F2}  ({pi[0]:F3}, {pi[1]:F3})  |q|={qi.Norm:F6}");
}
```
