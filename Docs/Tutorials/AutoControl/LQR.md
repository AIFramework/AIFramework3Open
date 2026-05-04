# LQR и LQG

**Пространство имён:** `AI.ControlSystems.Optimal`  
**Классы:** `DiscreteLqr`, `LqgRegulator`

---

## Теория

### LQR — линейный квадратичный регулятор

Задача: минимизировать бесконечный квадратичный критерий:

$$J = \sum_{k=0}^{\infty} \bigl(x[k]^\top Q\,x[k] + u[k]^\top R\,u[k]\bigr)$$

при ограничении $x[k+1] = Ax[k] + Bu[k]$.

Оптимальный закон управления — линейная обратная связь по состоянию:

$$u[k] = -Kx[k]$$

Матрица $K$ вычисляется через решение **дискретного уравнения Риккати** (DARE):

$$P = A^\top P A - A^\top P B(B^\top P B + R)^{-1} B^\top P A + Q$$

$$K = (B^\top P B + R)^{-1} B^\top P A$$

Итерации продолжаются до сходимости $\|P_{new} - P_{old}\|_F < \varepsilon$.

### Выбор матриц Q и R

- **Q** — матрица штрафов за отклонение состояния. Диагональный элемент $Q_{ii}$ задаёт относительную «важность» координаты $x_i$.
- **R** — матрица штрафов за управление. Увеличение $R$ приводит к более экономному, но более медленному управлению.
- **Правило Брайсона** (рекомендуемое начальное приближение): $Q_{ii} = 1/x_{i,\max}^2$, $R_{jj} = 1/u_{j,\max}^2$, где $x_{i,\max}$ и $u_{j,\max}$ — допустимые отклонения координат.

### LQG — линейный квадратичный гауссов регулятор

LQG = LQR + фильтр Калмана. По **принципу разделения** задачи оптимального управления и оценки состояния решаются независимо:

1. Синтезируем $K$ методом LQR.
2. Строим фильтр Калмана для оценки $\hat x$ по зашумлённым измерениям.
3. Применяем: $u = -K\hat x$.

На каждом шаге `LqgRegulator.Step(uPrev, y)`:
1. Предсказание КФ: $\bar x = A\hat x + Bu_{prev}$.
2. Коррекция КФ: $\hat x^+ = \bar x + K_{KF}(y - C\bar x)$.
3. Управление: $u = -K\hat x^+$.

---

## API

### `DiscreteLqr`

| Метод | Описание |
|-------|----------|
| `Solve(A, B, Q, R)` | Возвращает матрицу усилений $K$ (m×n). |
| `Solve(A, B, Q, R, tolerance, maxIterations)` | С явными параметрами сходимости. |

### `LqgRegulator`

| Член | Описание |
|------|----------|
| `LqgRegulator(kalmanFilter, K)` | Создаёт регулятор из готового КФ и матрицы $K$. |
| `Step(uPrev, y)` | Один шаг: возвращает управление $u = -K\hat x^+$. |
| `StateFeedbackGain` | Матрица $K$. |
| `Filter` | Доступ к внутреннему `KalmanFilter`. |

---

## Примеры

### LQR для двойного интегратора

```csharp
using AI.ControlSystems.Linear;
using AI.ControlSystems.Optimal;
using AI.DataStructs.Algebraic;

double dt = 0.01;
var A = new Matrix(new double[,] { { 1, dt }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.5 * dt * dt }, { dt } });

// Штрафы: позиция важна, скорость менее важна, управление умеренное
var Q = new Matrix(new double[,] { { 10, 0 }, { 0, 1 } });
var R = new Matrix(new double[,] { { 0.1 } });

Matrix K = DiscreteLqr.Solve(A, B, Q, R);
Console.WriteLine($"K = [{K[0, 0]:F4}, {K[0, 1]:F4}]");

// Симуляция
var C = new Matrix(new double[,] { { 1, 0 } });
var model = new DiscreteLtiModel(A, B, C);
var x = new Vector(new[] { 5.0, 0.0 });

for (int k = 0; k < 200; k++)
{
    Vector xState = model.State;
    double u = -(K[0, 0] * xState[0] + K[0, 1] * xState[1]);
    model.Step(new Vector(new[] { u }));

    if (k % 20 == 0)
        Console.WriteLine($"k={k:D3}  x1={model.State[0]:F4}  x2={model.State[1]:F4}  u={u:F4}");
}
```

### LQG — управление по зашумлённым измерениям

```csharp
using AI.ControlSystems.Observers;
using AI.ControlSystems.Optimal;
using AI.DataStructs.Algebraic;

double dt = 0.01;
var A = new Matrix(new double[,] { { 1, dt }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.5 * dt * dt }, { dt } });
var C = new Matrix(new double[,] { { 1, 0 } });
var D = new Matrix(new double[,] { { 0 } });

// LQR
var Qlqr = new Matrix(new double[,] { { 10, 0 }, { 0, 1 } });
var Rlqr = new Matrix(new double[,] { { 0.1 } });
Matrix K = DiscreteLqr.Solve(A, B, Qlqr, Rlqr);

// Калман
var Qkf = new Matrix(new double[,] { { 1e-4, 0 }, { 0, 1e-4 } });
var Rkf = new Matrix(new double[,] { { 0.01 } });
var kf = new KalmanFilter(A, B, C, D, Qkf, Rkf);

// LQG
var lqg = new LqgRegulator(kf, K);

// Симуляция объекта
var plant = new DiscreteLtiModel(A, B, C);
var uPrev = new Vector(new[] { 0.0 });
var rng = new Random(0);

for (int k = 0; k < 300; k++)
{
    Vector y = plant.Step(uPrev);
    // Добавляем шум измерения
    var yNoisy = new Vector(new[] { y[0] + (rng.NextDouble() - 0.5) * 0.1 });

    Vector u = lqg.Step(uPrev, yNoisy);
    uPrev = u;

    if (k % 30 == 0)
        Console.WriteLine($"k={k:D3}  x1={plant.State[0]:F4}  x̂1={lqg.Filter.State[0]:F4}  u={u[0]:F4}");
}
```

---

## Замечания

- `DiscreteLqr.Solve` требует, чтобы $Q \ge 0$ (положительно полуопределённая) и $R > 0$ (положительно определённая).
- Если система неуправляема или $Q$ не «наблюдает» все нестабильные моды, итерации могут не сойтись.
- В `LqgRegulator` матрица $K$ должна иметь размер m×n (число управлений × порядок системы).
- LQG оптимален только при гауссовом шуме; при негауссовом шуме рассмотрите робастные методы.
