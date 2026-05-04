# Размещение полюсов (формула Аккермана)

**Пространство имён:** `AI.ControlSystems.Linear`  
**Класс:** `PolePlacement`

---

## Теория

### Задача размещения полюсов

Для управляемой системы $x[k+1] = Ax[k] + Bu[k]$ (SISO, $m=1$) требуется найти вектор усилений $K$ такой, чтобы замкнутая система $x[k+1] = (A - BK)x[k]$ имела заданные собственные значения (полюса) $\lambda_1, \ldots, \lambda_n$.

Желаемый характеристический полином:

$$p(\lambda) = (\lambda - \lambda_1)(\lambda - \lambda_2)\cdots(\lambda - \lambda_n) = \lambda^n + c_{n-1}\lambda^{n-1} + \cdots + c_0$$

### Матрица управляемости

Система $(A, B)$ управляема тогда и только тогда, когда матрица управляемости имеет полный ранг:

$$\mathcal{W} = \begin{bmatrix} B & AB & A^2B & \cdots & A^{n-1}B \end{bmatrix}$$

### Формула Аккермана

Для SISO-системы усиление вычисляется аналитически:

$$K = e_n^\top \mathcal{W}^{-1} \varphi(A)$$

где $e_n = [0, \ldots, 0, 1]^\top$ — последний стандартный базисный вектор, а $\varphi(A)$ — желаемый полином от матрицы:

$$\varphi(A) = A^n + c_{n-1}A^{n-1} + \cdots + c_0 I$$

Закон управления: $u = -Kx$.

### Выбор полюсов

- **Устойчивость**: все $|\lambda_i| < 1$ (дискретное время).
- **Быстродействие**: чем ближе полюса к нулю, тем быстрее затухание.
- **Осцилляции**: комплексные полюса $\lambda = r e^{\pm j\theta}$ дают колебания.
- Правило: полюса наблюдателя размещают в 2–5 раз быстрее полюсов регулятора.

---

## API

| Метод | Описание |
|-------|----------|
| `ControllabilityMatrix(A, B)` | Матрица управляемости $\mathcal{W}$ (n×n). |
| `AckermannGain(A, B, coeffs)` | Строка усилений $K$ (1×n). Коэффициенты `coeffs` — вектор $[c_0, c_1, \ldots, c_{n-1}]$. |

**Ограничение:** только SISO ($B$ — столбец n×1).

---

## Примеры

### Размещение полюсов для двойного интегратора

```csharp
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;

// Дискретный двойной интегратор (dt = 0.1 с)
var A = new Matrix(new double[,] { { 1, 0.1 }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.005 }, { 0.1 } });

// Желаемые полюса: оба в 0.7 (вещественные, устойчивые)
// p(λ) = (λ - 0.7)^2 = λ^2 - 1.4λ + 0.49
// Коэффициенты [c0, c1] = [0.49, -1.4]
var desiredCoeffs = new Vector(new[] { 0.49, -1.4 });

Matrix K = PolePlacement.AckermannGain(A, B, desiredCoeffs);

Console.WriteLine($"K = [{K[0, 0]:F4}, {K[0, 1]:F4}]");
```

### Проверка управляемости

```csharp
Matrix W = PolePlacement.ControllabilityMatrix(A, B);
Console.WriteLine($"det(W) = {W.Determinant:F6}");
// Если det ≠ 0 — система управляема
```

### Симуляция замкнутой системы

```csharp
var model = new DiscreteLtiModel(A, B, new Matrix(new double[,] { { 1, 0 } }));
var x = new Vector(new[] { 5.0, 0.0 });  // начальное состояние

for (int k = 0; k < 50; k++)
{
    // u = -K x
    double u = -(K[0, 0] * x[0] + K[0, 1] * x[1]);
    var uVec = new Vector(new[] { u });
    Vector y = model.Step(uVec);
    x = model.State;
    Console.WriteLine($"k={k:D2}  x1={x[0]:F4}  x2={x[1]:F4}  u={u:F4}");
}
```

---

## Замечания

- При плохо обусловленной $\mathcal{W}$ (почти неуправляемая система) `GetInvertMatrix()` выбросит исключение.
- Для MIMO используйте `DiscreteLqr` — он не требует SISO и даёт оптимальное усиление.
- Коэффициенты полинома задаются **от младшей степени к старшей**: $[c_0, c_1, \ldots, c_{n-1}]$.
