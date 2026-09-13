# Пространство состояний: дискретизация и анализ

**Пространство имён:** `AI.ControlSystems.Linear`  
**Классы:** `DiscreteLtiModel`, `Discretization`, `SystemAnalysis`, `LyapunovEquation`

---

## Постановка задачи

Объект описан непрерывной линейной моделью, а регулятор работает с шагом $\Delta t$. Нужно
получить **точную** дискретную модель — и для управления, и для шума процесса, — а также
ответить на базовые вопросы о системе: устойчива ли она, управляема ли, наблюдаема ли, какова
установившаяся ковариация.

---

## Теория

### Модели

Непрерывная: $\dot x = A_c x + B_c u$, $y = C_c x + D_c u$. Дискретная:
$x_{k+1} = A x_k + B u_k$, $y_k = C x_k + D u_k$.

### Экстраполятор нулевого порядка (ZOH)

При постоянном на шаге управлении $u(t) = u_k$ точные матрицы

$$A_d = e^{A_c \Delta t}, \qquad B_d = \int_0^{\Delta t} e^{A_c \tau}\,d\tau\;B_c$$

получаются одной экспонентой расширенной матрицы:

$$\exp\left(\begin{bmatrix} A_c & B_c \\ 0 & 0 \end{bmatrix}\Delta t\right) = \begin{bmatrix} A_d & B_d \\ 0 & I \end{bmatrix}.$$

Обращать $A_c$ не нужно, поэтому формула верна и для вырожденной $A_c$ (интеграторы). Экспонента
считается масштабированием и возведением в квадрат: степень $s$ выбирается по бесконечной норме
так, чтобы $\|M/2^s\|_\infty \le 1/2$, после чего ряд Тейлора сходится без сокращения больших
слагаемых.

Прежняя версия считала интеграл отдельным рядом и масштабировала по наибольшему элементу. На
жёсткой системе $\dot x = -50x + u$ с шагом 1 она теряла точность: регрессионный тест требует
$B_d = (1 - e^{-50})/50$ до 12 знаков.

### Шум процесса (метод Ван Лоана)

Белый шум интенсивности $Q_c$ в непрерывной модели даёт дискретную ковариацию
$Q_d = \int_0^{\Delta t} e^{A_c\tau} Q_c e^{A_c^\top\tau} d\tau$. По Ван Лоану

$$\exp\left(\begin{bmatrix} -A_c & Q_c \\ 0 & A_c^\top \end{bmatrix}\Delta t\right) = \begin{bmatrix} \cdot & F_{12} \\ 0 & F_{22} \end{bmatrix}, \qquad Q_d = F_{22}^\top F_{12}.$$

Для двойного интегратора с шумом ускорения интенсивности $q$ ответ известен:
$Q_d = q\begin{bmatrix} \Delta t^3/3 & \Delta t^2/2 \\ \Delta t^2/2 & \Delta t \end{bmatrix}$.

### Анализ

- **Управляемость и наблюдаемость** — полный ранг матриц $[B, AB, \ldots, A^{n-1}B]$ и
  $[C; CA; \ldots; CA^{n-1}]$, ранг по сингулярным числам.
- **Полюса** — собственные значения $A$: балансировка, приведение к форме Хессенберга и
  QR-итерации Фрэнсиса с двойным сдвигом (`Eigen.General` из `AI.ClassicMath`).
- **Уравнения Ляпунова**: непрерывное $AX + XA^\top + Q = 0$ и дискретное $AXA^\top - X + Q = 0$.
  Решение дискретного — установившаяся ковариация процесса $x_{k+1} = Ax_k + w_k$ с
  $\mathrm{cov}\,w = Q$.

---

## API

### `DiscreteLtiModel`

| Член | Описание |
|------|----------|
| `DiscreteLtiModel(A, B, C)` | Без прямой связи, нулевое начальное состояние. |
| `DiscreteLtiModel(A, B, C, D, x0)` | С прямой связью и начальным состоянием. |
| `Step(u)` | $x \leftarrow Ax + Bu$; возвращает $y$. |
| `OutputFor(u)` | Выход без обновления состояния. |
| `State`, `Reset()` | Состояние и его сброс. |

### `Discretization`

| Метод | Описание |
|-------|----------|
| `ZeroOrderHold(Ac, Bc, dt, out Ad, out Bd)` | ZOH через экспоненту расширенной матрицы. |
| `ZeroOrderHoldModel(Ac, Bc, Cc, dt, x0)` | Сразу `DiscreteLtiModel`. |
| `DiscretizeProcessNoise(Ac, Qc, dt)` | $Q_d$ по Ван Лоану. |

### `SystemAnalysis` и `LyapunovEquation`

| Метод | Описание |
|-------|----------|
| `SystemAnalysis.IsControllable(A, B)`, `IsObservable(A, C)` | Проверки ранга. |
| `SystemAnalysis.Poles(A)`, `SpectralRadius(A)` | Собственные значения. |
| `SystemAnalysis.IsStableDiscrete(A)`, `IsStableContinuous(A)` | $\rho(A) < 1$ и $\max\mathrm{Re}\,\lambda < 0$. |
| `LyapunovEquation.SolveContinuous(A, Q)` | $AX + XA^\top + Q = 0$. |
| `LyapunovEquation.SolveDiscrete(A, Q)` | $AXA^\top - X + Q = 0$. |

---

## Примеры

### Дискретная модель вручную

```csharp
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;

// Двойной интегратор с шагом 0.01
var A = new Matrix(new double[,] { { 1, 0.01 }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.00005 }, { 0.01 } });
var C = new Matrix(new double[,] { { 1, 0 } });

var model = new DiscreteLtiModel(A, B, C);
var input = new Vector(new[] { 1.0 });

for (int k = 0; k < 100; k++)
{
    Vector y = model.Step(input);
    if (k % 20 == 0)
        Console.WriteLine($"y = {y[0]:F6}");
}
```

### ZOH-дискретизация непрерывной модели

```csharp
var Ac = new Matrix(new double[,] { { 0, 1 }, { 0, 0 } });
var Bc = new Matrix(new double[,] { { 0 }, { 1 } });
var Cc = new Matrix(new double[,] { { 1, 0 } });
double dt = 0.01;

var zohModel = Discretization.ZeroOrderHoldModel(Ac, Bc, Cc, dt, new Vector(2));
Console.WriteLine($"Ad[0,1] = {zohModel.A[0, 1]:F6}, Bd[0,0] = {zohModel.B[0, 0]:E3}");   // dt и dt²/2

Discretization.ZeroOrderHold(Ac, Bc, dt, out Matrix Ad, out Matrix Bd);
```

### Шум процесса

```csharp
// Шум ускорения интенсивности 0.5 → q·[dt³/3, dt²/2; dt²/2, dt]
var Qc = new Matrix(new double[,] { { 0, 0 }, { 0, 0.5 } });
Matrix Qd = Discretization.DiscretizeProcessNoise(Ac, Qc, dt);
Console.WriteLine($"Qd = [{Qd[0, 0]:E3} {Qd[0, 1]:E3}; {Qd[1, 0]:E3} {Qd[1, 1]:E3}]");
```

### Анализ и установившаяся ковариация

```csharp
Console.WriteLine($"управляема: {SystemAnalysis.IsControllable(Ad, Bd)}, наблюдаема: {SystemAnalysis.IsObservable(Ad, Cc)}");
Console.WriteLine($"устойчива: {SystemAnalysis.IsStableDiscrete(Ad)}");   // нет: два полюса в 1

// Устойчивый процесс x⁺ = Fx + w: ковариация, к которой он приходит
var F = new Matrix(new double[,] { { 0.9, 0.1 }, { 0, 0.8 } });
Matrix covariance = LyapunovEquation.SolveDiscrete(F, Qd);
Console.WriteLine($"установившаяся дисперсия x1: {covariance[0, 0]:E3}");
```

---

## Сложность

| Операция | Стоимость |
|----------|-----------|
| `ZeroOrderHold` | $O((n + m)^3 (s + K))$: $s = \lceil\log_2(\|A\Delta t\|_\infty / 0.5)\rceil$ возведений в квадрат, $K \le 60$ членов ряда |
| `DiscretizeProcessNoise` | То же для матрицы $2n \times 2n$ |
| `Poles` | $O(n^3)$ |
| `IsControllable`, `IsObservable` | $O(n^3)$ на развёртку SVD |
| `LyapunovEquation` | $O(n^6)$ — система $n^2 \times n^2$ через кронекерово произведение |

---

## Замечания

- Размерности: $A$ — n×n, $B$ — n×m, $C$ — p×n, $D$ — p×m.
- Уравнения Ляпунова решаются плотной системой порядка $n^2$, поэтому порядок ограничен
  `LyapunovEquation.MaxOrder = 30`. Для больших систем нужен метод Бартелса — Стюарта через форму Шура.
- Экспонента считается рядом Тейлора с масштабированием, без аппроксимации Паде: при огромных нормах
  $\|A\Delta t\|$ число возведений в квадрат растёт логарифмически, но ошибка округления накапливается.
- Дискретизации первого порядка (FOH) и билинейной (Тастин) нет.
- Для нелинейных объектов используйте `ExtendedKalmanFilter` с линеаризацией на каждом шаге.

---

## Проверка

Тесты `ControlSystemsLinearTests` и `ControlSystemsRegressionTests`:

- ZOH сверен с аналитическими ответами для первого порядка, двойного интегратора и осциллятора
  до 13–14 знаков, а для жёсткой системы $a = -50$, $\Delta t = 1$ — до 12 знаков;
- $Q_d$ по Ван Лоану сверена с формулами для скалярной системы и двойного интегратора;
- уравнения Ляпунова сверены со скалярными формулами $x = -q/(2a)$ и $x = q/(1 - a^2)$, невязка на
  системе 3×3 меньше $10^{-11}$.
