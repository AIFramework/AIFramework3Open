# LQR и LQG

**Пространство имён:** `AI.ControlSystems.Optimal`, `AI.ControlSystems.Linear`  
**Классы:** `DiscreteLqr`, `LqrDesign`, `RiccatiEquation`, `LqgRegulator`

---

## Постановка задачи

Для дискретной системы $x_{k+1} = Ax_k + Bu_k$ найти обратную связь, минимизирующую бесконечный
квадратичный критерий

$$J = \sum_{k=0}^{\infty} \bigl(x_k^\top Q\,x_k + u_k^\top R\,u_k\bigr), \qquad Q \succeq 0,\; R \succ 0.$$

Если состояние не измеряется, а доступен зашумлённый выход $y_k = Cx_k + v_k$ при шуме процесса
$w_k$, та же задача в среднем решается LQG-регулятором: LQR по оценке фильтра Калмана.

---

## Теория

### LQR и уравнение Риккати

Оптимум — линейная обратная связь $u = -Kx$ с

$$K = (R + B^\top P B)^{-1} B^\top P A,$$

где $P$ — стабилизирующее решение дискретного уравнения Риккати (DARE):

$$P = A^\top P A - A^\top P B (R + B^\top P B)^{-1} B^\top P A + Q.$$

Минимальная стоимость из $x_0$ — квадратичная форма $J^* = x_0^\top P x_0$.

`RiccatiEquation.SolveDiscrete` решает DARE **методом удвоения**, сохраняющим структуру (SDA):

$$A_{j+1} = A_j W^{-1} A_j,\quad G_{j+1} = G_j + A_j W^{-1} G_j A_j^\top,\quad H_{j+1} = H_j + A_j^\top H_j W^{-1} A_j,$$

где $W = I + G_j H_j$, $G_0 = BR^{-1}B^\top$, $H_0 = Q$. $H_j \to P$ квадратично: каждое удвоение
соответствует $2^j$ шагам обычной итерации Риккати. После сходимости проверяются невязка DARE и
спектральный радиус $A - BK$. Если пара $(A, B)$ не стабилизируема или неустойчивая мода не видна
через $Q$, выбрасывается исключение с причиной, а не возвращается бесполезное усиление.

### Выбор Q и R

- $Q_{ii}$ — вес отклонения координаты $x_i$, $R_{jj}$ — цена управления $u_j$.
- Правило Брайсона как начальное приближение: $Q_{ii} = 1/x_{i,\max}^2$, $R_{jj} = 1/u_{j,\max}^2$.

### LQG и принцип разделения

Установившийся фильтр Калмана — двойственная задача: $P_f$ = DARE($A^\top$, $C^\top$, $W$, $V$),
коэффициент коррекции $L = P_f C^\top (C P_f C^\top + V)^{-1}$, апостериорная ковариация
$\Sigma = (I - LC)P_f$. Регулятор на каждом шаге предсказывает $\bar x = A\hat x + Bu_{prev}$,
корректирует $\hat x = \bar x + L(y - C\bar x)$ и выдаёт $u = -K\hat x$.

По **принципу разделения** полюса замкнутой системы — объединение полюсов регулятора и оценщика:

$$\mathrm{eig}(A - BK) \;\cup\; \mathrm{eig}(A - LCA).$$

Средняя стоимость за шаг в установившемся режиме:

$$\bar J = \mathrm{tr}(P W) + \mathrm{tr}\bigl(K^\top (R + B^\top P B) K\,\Sigma\bigr).$$

Первое слагаемое — цена шума процесса при полностью известном состоянии, второе — цена ошибки
оценки.

---

## API

### `DiscreteLqr`

| Метод | Описание |
|-------|----------|
| `Design(A, B, Q, R)` | `LqrDesign`: усиление $K$, решение $P$ (`CostToGo`), полюса $A - BK$. |
| `Solve(A, B, Q, R)` | Только $K$ (m×n). Параметр `maxIterations` сохранён для совместимости и не используется. |

### `RiccatiEquation`

| Метод | Описание |
|-------|----------|
| `SolveDiscrete(A, B, Q, R, tolerance)` | Стабилизирующее решение DARE. |
| `DiscreteResidual(A, B, Q, R, X)` | Невязка DARE для проверки чужого решения. |
| `DiscreteGain(A, B, R, X)` | $K$ по решению $X$. |

### `LqgRegulator`

| Член | Описание |
|------|----------|
| `Design(A, B, C, Q, R, W, V)` | Синтез: LQR + установившийся фильтр Калмана + ожидаемая стоимость. |
| `LqgRegulator(kalmanFilter, K)` | Сборка из готовых частей; характеристики синтеза остаются пустыми. |
| `Step(uPrev, y)` | Шаг: оценка и управление $u = -K\hat x$. |
| `StateFeedbackGain`, `EstimatorGain` | $K$ и $L$. |
| `CostToGo`, `EstimationCovariance` | $P$ и $\Sigma$. |
| `ExpectedCost` | $\bar J$. |
| `ClosedLoopPoles` | $2n$ полюсов замкнутой системы. |
| `Filter` | Внутренний `KalmanFilter`. |

---

## Примеры

### LQR и стоимость без моделирования

```csharp
using AI.ControlSystems.Linear;
using AI.ControlSystems.Optimal;
using AI.DataStructs.Algebraic;

double dt = 0.1;
var A = new Matrix(new double[,] { { 1, dt }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.5 * dt * dt }, { dt } });
var Q = new Matrix(new double[,] { { 1, 0 }, { 0, 0.2 } });
var R = new Matrix(new double[,] { { 0.05 } });

LqrDesign design = DiscreteLqr.Design(A, B, Q, R);
Matrix K = design.Gain;
Console.WriteLine($"K = [{K[0, 0]:F4}, {K[0, 1]:F4}]");

foreach (var pole in design.ClosedLoopPoles)
    Console.WriteLine($"полюс {pole.Real:F4} {pole.Imaginary:+0.0000;-0.0000}i");

// J* = x₀ᵀ P x₀ — без единого шага моделирования
var x0 = new Vector(new[] { 3.0, -1.0 });
double predicted = 0;
for (int i = 0; i < 2; i++)
    for (int j = 0; j < 2; j++)
        predicted += x0[i] * design.CostToGo[i, j] * x0[j];

var C = new Matrix(new double[,] { { 1, 0 } });
var D = new Matrix(new double[,] { { 0 } });
var model = new DiscreteLtiModel(A, B, C, D, x0);
double simulated = 0;

for (int k = 0; k < 5000; k++)
{
    Vector x = model.State;
    double u = -(K[0, 0] * x[0] + K[0, 1] * x[1]);
    simulated += Q[0, 0] * x[0] * x[0] + Q[1, 1] * x[1] * x[1] + R[0, 0] * u * u;
    model.Step(new Vector(new[] { u }));
}

Console.WriteLine($"J* = {predicted:F6}, по моделированию {simulated:F6}");
```

### Проверка решения Риккати

```csharp
Matrix P = RiccatiEquation.SolveDiscrete(A, B, Q, R);
Matrix residual = RiccatiEquation.DiscreteResidual(A, B, Q, R, P);
Console.WriteLine($"невязка DARE: {residual.Data.Max(Math.Abs):E1}");
```

### LQG по зашумлённому положению

```csharp
// Измеряется только положение, σ = 0.1; шум процесса по обеим координатам
var Qg = new Matrix(new double[,] { { 1, 0 }, { 0, 0.1 } });
var Rg = new Matrix(new double[,] { { 0.1 } });
var W = new Matrix(new double[,] { { 1e-4, 0 }, { 0, 1e-3 } });
var V = new Matrix(new double[,] { { 0.01 } });

LqgRegulator lqg = LqgRegulator.Design(A, B, C, Qg, Rg, W, V);
Console.WriteLine($"ожидаемая стоимость за шаг {lqg.ExpectedCost:F5}, полюсов {lqg.ClosedLoopPoles.Length}");

var rng = new Random(8);
double Gauss() => Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());

var state = new Vector(2);
var uPrev = new Vector(new[] { 0.0 });
double total = 0;
int samples = 0;

for (int k = 0; k < 100_000; k++)
{
    var y = new Vector(new[] { state[0] + 0.1 * Gauss() });
    Vector u = lqg.Step(uPrev, y);

    if (k >= 1000)
    {
        total += Qg[0, 0] * state[0] * state[0] + Qg[1, 1] * state[1] * state[1] + Rg[0, 0] * u[0] * u[0];
        samples++;
    }

    state = new Vector(new[]
    {
        A[0, 0] * state[0] + A[0, 1] * state[1] + B[0, 0] * u[0] + 0.01 * Gauss(),
        A[1, 0] * state[0] + A[1, 1] * state[1] + B[1, 0] * u[0] + Math.Sqrt(1e-3) * Gauss()
    });
    uPrev = u;
}

Console.WriteLine($"по моделированию {total / samples:F5}");   // в пределах 5 % от ExpectedCost
```

---

## Сложность

| Операция | Стоимость |
|----------|-----------|
| `RiccatiEquation.SolveDiscrete` | $O(n^3)$ на удвоение; удвоений обычно 5–30 при пределе 100 |
| `DiscreteLqr.Design` | DARE + собственные значения $A - BK$, $O(n^3)$ |
| `LqgRegulator.Design` | два DARE и собственные значения, $O(n^3)$ |
| `LqgRegulator.Step` | $O(n^2 + nm + np + p^3)$ |

---

## Замечания

- Только дискретное время. Непрерывную модель сначала дискретизируйте (`Discretization.ZeroOrderHold`),
  а шум процесса — по Ван Лоану (`Discretization.DiscretizeProcessNoise`).
- $R$ должна быть положительно определённой, иначе `ArgumentException`. Нестабилизируемая пара или
  неустойчивая мода, не видимая через $Q$, дают `InvalidOperationException` с причиной. Прежняя
  версия в таком случае молча возвращала усиление, не стабилизирующее систему.
- LQG не гарантирует запасов устойчивости (Дойл, 1978): оптимальность в среднем не означает
  робастности к ошибкам модели.
- `ExpectedCost` верна для белых гауссовых независимых шумов и фильтра в установившемся режиме.

---

## Проверка

Тесты `ControlSystemsLinearTests` и `ControlSystemsAdvancedTests`:

- скалярное DARE сверено с замкнутой формулой;
- на 10 случайных системах 4×4 с двумя входами решение совпадает с 20 000 итераций Риккати до 6 знаков;
- в первом примере $x_0^\top P x_0$ совпадает со стоимостью, накопленной за 5000 шагов, до 8 знаков;
- полюса LQG совпадают с собственными числами явно собранной замкнутой системы 4×4 до $10^{-8}$;
- во втором примере стоимость по 99 000 шагам укладывается в 5 % от `ExpectedCost`.
