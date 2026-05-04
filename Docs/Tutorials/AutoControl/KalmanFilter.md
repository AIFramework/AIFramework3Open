# Фильтр Калмана и расширенный фильтр Калмана

**Пространство имён:** `AI.ControlSystems.Observers`  
**Классы:** `KalmanFilter`, `ExtendedKalmanFilter`

---

## Теория

### Линейный фильтр Калмана (KF)

Фильтр Калмана — оптимальный линейный наблюдатель при гауссовом шуме. Модель:

$$x[k+1] = Ax[k] + Bu[k] + w[k], \quad w \sim \mathcal{N}(0, Q)$$
$$y[k] = Cx[k] + Du[k] + v[k], \quad v \sim \mathcal{N}(0, R)$$

Алгоритм состоит из двух шагов на каждом такте:

**Предсказание:**

$$\bar x[k] = A\hat x[k-1] + Bu[k-1]$$
$$\bar P[k] = A P[k-1] A^\top + Q$$

**Коррекция:**

$$\nu = y[k] - C\bar x[k] - Du[k]$$
$$S = C\bar P C^\top + R$$
$$K = \bar P C^\top S^{-1}$$
$$\hat x[k] = \bar x[k] + K\nu$$
$$P[k] = (I - KC)\bar P(I - KC)^\top + KRK^\top$$

Матрица $K$ — **коэффициент Калмана** (усиление коррекции).

### Расширенный фильтр Калмана (EKF)

Для нелинейных систем $x[k+1] = f(x[k], u[k])$, $y[k] = h(x[k])$ линеаризуем в текущей точке:

$$F = \frac{\partial f}{\partial x}\bigg|_{\hat x}, \quad H = \frac{\partial h}{\partial x}\bigg|_{\hat x}$$

Шаги аналогичны KF, но вместо $A$, $C$ используются якобианы $F$, $H$. Нелинейное предсказание $x^+ = f(\hat x, u)$ и $\hat y = h(x^+)$ вычисляются **пользователем** и передаются в `Predict` / `Update`.

### Выбор матриц Q и R

- **Q** (ковариация шума процесса): увеличение Q повышает доверие к измерениям, ускоряет реакцию оценки, однако увеличивает дисперсию оценки.
- **R** (ковариация шума измерения): увеличение R повышает доверие к модели, сглаживает оценку, однако замедляет реакцию на изменения.
- Начальная ковариация **P₀**: характеризует неопределённость начального состояния; при неизвестном состоянии задают $P_0 = \alpha I$ с большим $\alpha$.

---

## API

### `KalmanFilter`

| Член | Описание |
|------|----------|
| `KalmanFilter(A, B, C, D, Q, R)` | Нулевое начальное состояние, P₀ = 0.01·I. |
| `KalmanFilter(A, B, C, D, Q, R, x0, P0)` | Полная форма. |
| `Predict(u)` | Шаг предсказания по управлению `u`. |
| `Update(y, u)` | Шаг коррекции по измерению `y` (то же `u`, что в `Predict`). |
| `Reset(x0, P0)` | Сброс состояния и ковариации. |
| `State` | Текущая оценка $\hat x$. |
| `Covariance` | Текущая ковариация ошибки $P$. |
| `Q`, `R` | Матрицы шумов (можно менять на лету). |

### `ExtendedKalmanFilter`

| Член | Описание |
|------|----------|
| `ExtendedKalmanFilter(x0, P0)` | Инициализация начальным состоянием и ковариацией. |
| `Predict(xNext, F, Q)` | Предсказание: `xNext` = $f(\hat x, u)$, `F` = якобиан $\partial f/\partial x$. |
| `Update(y, yPred, H, R)` | Коррекция: `yPred` = $h(x^+)$, `H` = якобиан $\partial h/\partial x$. |
| `Reset(x0, P0)` | Сброс. |
| `State`, `Covariance` | Оценка и ковариация. |

---

## Примеры

### KF для оценки скорости по позиции

```csharp
using AI.ControlSystems.Observers;
using AI.DataStructs.Algebraic;

double dt = 0.01;

// Модель: x = [позиция, скорость], u = ускорение
var A = new Matrix(new double[,] { { 1, dt }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.5 * dt * dt }, { dt } });
var C = new Matrix(new double[,] { { 1, 0 } });  // измеряем только позицию
var D = new Matrix(new double[,] { { 0 } });

// Шум процесса и измерения
var Q = new Matrix(new double[,] { { 1e-4, 0 }, { 0, 1e-4 } });
var R = new Matrix(new double[,] { { 0.01 } });

var kf = new KalmanFilter(A, B, C, D, Q, R);

var u = new Vector(new[] { 0.0 });  // нет внешнего управления
var rng = new Random(42);

for (int k = 0; k < 200; k++)
{
    // Реальная позиция с шумом
    double truePos = 0.5 * k * k * dt * dt;
    double measuredPos = truePos + rng.NextDouble() * 0.1 - 0.05;
    var y = new Vector(new[] { measuredPos });

    kf.Predict(u);
    kf.Update(y, u);

    Console.WriteLine($"k={k:D3}  true={truePos:F3}  est={kf.State[0]:F3}  vel={kf.State[1]:F3}");
}
```

### EKF для нелинейного маятника

```csharp
using AI.ControlSystems.Observers;
using AI.DataStructs.Algebraic;

// Нелинейный маятник: x = [угол, угловая скорость]
// f(x, u): x1' = x1 + x2*dt, x2' = x2 - (g/L)*sin(x1)*dt
double g = 9.81, L = 1.0, dt = 0.01;

var x0 = new Vector(new[] { 0.3, 0.0 });
var P0 = new Matrix(new double[,] { { 0.1, 0 }, { 0, 0.1 } });
var Q  = new Matrix(new double[,] { { 1e-5, 0 }, { 0, 1e-4 } });
var R  = new Matrix(new double[,] { { 0.01 } });

var ekf = new ExtendedKalmanFilter(x0, P0);

for (int k = 0; k < 300; k++)
{
    double x1 = ekf.State[0], x2 = ekf.State[1];

    // Нелинейное предсказание
    double x1n = x1 + x2 * dt;
    double x2n = x2 - (g / L) * Math.Sin(x1) * dt;
    var xNext = new Vector(new[] { x1n, x2n });

    // Якобиан F = df/dx
    var F = new Matrix(new double[,]
    {
        { 1, dt },
        { -(g / L) * Math.Cos(x1) * dt, 1 }
    });

    ekf.Predict(xNext, F, Q);

    // Измерение: угол с шумом
    double measAngle = ekf.State[0] + (new Random(k).NextDouble() - 0.5) * 0.05;
    var y     = new Vector(new[] { measAngle });
    var yPred = new Vector(new[] { ekf.State[0] });

    // Якобиан H = dh/dx = [1, 0]
    var H = new Matrix(new double[,] { { 1, 0 } });

    ekf.Update(y, yPred, H, R);

    Console.WriteLine($"k={k:D3}  angle={ekf.State[0]:F4}  omega={ekf.State[1]:F4}");
}
```

---

## Замечания

- В `KalmanFilter.Update` передавайте то же `u`, что было в `Predict` — оно нужно для вычисления $D u$ в выходе.
- EKF требует, чтобы пользователь сам вычислил $f(\hat x, u)$ и якобиан $F$; фильтр не знает о нелинейной модели.
- При сильной нелинейности EKF может расходиться — рассмотрите UKF или частицевый фильтр.
- Ковариация `P` симметризуется после каждого шага для численной стабильности.
