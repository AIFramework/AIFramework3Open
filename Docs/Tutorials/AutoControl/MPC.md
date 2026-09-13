# Управление с прогнозирующей моделью (MPC)

**Пространство имён:** `AI.ControlSystems.Optimal`  
**Классы:** `ModelPredictiveController`, `MpcStep`, `LinearQuadraticMpc`

---

## Постановка задачи

Привод ограничен: управление не больше предела, растёт не быстрее заданной скорости, а состояние
(скорость, температура, уровень) не должно выходить за допуск. LQR этих ограничений не видит: он
требует столько управления, сколько велит оптимум, а насыщение привода портит и оптимальность,
и устойчивость. MPC решает на каждом такте задачу оптимизации **с ограничениями** на горизонте
$N$ шагов, подаёт первое управление и повторяет всё на следующем такте с новым измерением.

---

## Теория

### Задача на горизонте

Для $x_{k+1} = Ax_k + Bu_k$ с уставкой $r$ и установившимся управлением $u_r$:

$$\min_{u_0..u_{N-1}} \sum_{k=1}^{N-1}\|x_k - r\|_Q^2 + \|x_N - r\|_{Q_f}^2 + \sum_{k=0}^{N-1}\Bigl(\|u_k - u_r\|_R^2 + \|u_k - u_{k-1}\|_S^2\Bigr)$$

при

$$u_{\min} \le u_k \le u_{\max}, \qquad \Delta u_{\min} \le u_k - u_{k-1} \le \Delta u_{\max}, \qquad x_{\min} \le x_k \le x_{\max}.$$

$u_{-1}$ — управление, поданное на прошлом такте.

### Плотная форма

Прогноз линеен по плану $U = [u_0; \ldots; u_{N-1}]$: $X = \Phi x_0 + \Gamma U$. Подстановка в
стоимость оставляет квадратичную задачу от $U$:

$$\tfrac12 U^\top H U + g^\top U, \qquad H = 2(\Gamma^\top\bar Q\Gamma + \bar R + D^\top\bar S D), \qquad g = 2(\Gamma^\top\bar Q f - \bar R U_r - D^\top \bar S d_0),$$

где $f = \Phi x_0 - r$, а $D$ — матрица разностей для $\Delta u$. Ограничения на состояние
становятся линейными по $U$: $\Gamma U \le x_{\max} - \Phi x_0$. $H$ зависит только от весов и
горизонта и собирается один раз, на такте пересчитывается только $g$ и правые части. Задачу
решает `QpSolver` из `AI.Solvers.Optimization` — метод активного множества.

### Мягкие ограничения на состояние

Возмущение может вывести прогноз туда, где жёсткие ограничения выполнить нельзя, и регулятору
станет нечего подать. Поэтому по умолчанию каждая ограниченная координата получает свою
неотрицательную переменную ослабления $\varepsilon_i$ (одну на весь горизонт) со штрафом
$\rho\varepsilon_i^2 + \rho\varepsilon_i$. Линейный член делает штраф точным: если ограничение
выполнимо, оптимум его выполняет; если нет — нарушает как можно меньше.

### Связь с LQR

Если $Q_f = P$ — решение уравнения Риккати (значение по умолчанию), стоимость после горизонта
учтена точно, и **без активных ограничений** первое управление MPC совпадает с $-K_{LQR}x$ на любом
горизонте. Ограничения делают закон кусочно-линейным: регулятор заранее притормаживает, чтобы не
упереться в предел.

### Тёплый старт и резервный план

План прошлого такта, сдвинутый на шаг, служит начальной точкой решателя: план меняется мало, и
метод активного множества приходит к оптимуму за несколько итераций. Если задача всё же не
решена (несовместные жёсткие ограничения), подаётся следующий шаг прошлого плана, обрезанный по
пределам управления, а `MpcStep.UsedFallback` сообщает об этом.

### `LinearQuadraticMpc`

Без ограничений задача решается обратным проходом Риккати от $S_N = Q_f$; `ComputeFirstGain`
возвращает первое усиление $K_0$. Это аналитический частный случай, полезный для сравнения
горизонтов.

---

## API

### `ModelPredictiveController`

| Член | Описание |
|------|----------|
| `ModelPredictiveController(A, B, Q, R, horizon, Qf = P)` | $Q_f$ по умолчанию — решение DARE. |
| `InputLower`, `InputUpper` | Пределы управления (длина m); `null` — нет. |
| `RateLower`, `RateUpper` | Пределы приращения за такт. |
| `StateLower`, `StateUpper` | Пределы состояния; бесконечность — нет предела по координате. |
| `RateWeight` | $S$ (m×m) — штраф приращения. |
| `SoftStateConstraints` | Мягкие ли ограничения на состояние (по умолчанию да). |
| `ViolationWeight` | $\rho$ (по умолчанию $10^5$). |
| `StateReference`, `InputReference` | $r$ и $u_r$; можно менять между тактами. |
| `Step(x)` | Такт: `MpcStep` с планом и диагностикой. |
| `Compute(x)` | Только управление. |
| `Reset(u)` | Сброс памяти: прошлое управление и тёплый старт. |

### `MpcStep`

| Член | Описание |
|------|----------|
| `Input` | Управление, которое нужно подать сейчас. |
| `PredictedInputs`, `PredictedStates` | План $u_0..u_{N-1}$ и прогноз $x_1..x_N$. |
| `PredictedCost` | Стоимость плана. |
| `Status`, `Iterations` | Исход и итерации QP. |
| `ActiveConstraints` | Число активных ограничений. |
| `StateViolation` | Наибольшее нарушение ограничений состояния в прогнозе. |
| `UsedFallback` | Подан резервный план. |
| `Interpret()` | Объяснение такта (`IInterpretable`). |

### `LinearQuadraticMpc`

| Метод | Описание |
|-------|----------|
| `ComputeFirstGain(A, B, Q, R, Qf, horizon)` | $K_0$ (m×n) задачи без ограничений. |

---

## Примеры

### Двойной интегратор с ограниченным приводом

```csharp
using AI.ControlSystems.Linear;
using AI.ControlSystems.Optimal;
using AI.DataStructs.Algebraic;
using AI.Solvers.Optimization;

double dt = 0.1;
var A = new Matrix(new double[,] { { 1, dt }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.5 * dt * dt }, { dt } });
var Q = new Matrix(new double[,] { { 1, 0 }, { 0, 0.1 } });
var R = new Matrix(new double[,] { { 0.01 } });

Matrix K = DiscreteLqr.Solve(A, B, Q, R);
Console.WriteLine($"LQR из x = [10, 0] требует u = {-(K[0, 0] * 10):F2}, а привод даёт только ±1");

var mpc = new ModelPredictiveController(A, B, Q, R, horizon: 20)
{
    InputLower = new Vector(new[] { -1.0 }),
    InputUpper = new Vector(new[] { 1.0 })
};

var C = new Matrix(new double[,] { { 1, 0 } });
var D = new Matrix(new double[,] { { 0 } });
var plant = new DiscreteLtiModel(A, B, C, D, new Vector(new[] { 10.0, 0.0 }));

for (int k = 0; k < 400; k++)
{
    MpcStep step = mpc.Step(plant.State);
    plant.Step(step.Input);

    if (k % 50 == 0)
        Console.WriteLine($"k={k:D3}  x={plant.State[0],8:F4}  v={plant.State[1],8:F4}  u={step.Input[0],6:F3}  активных {step.ActiveConstraints}");
}
```

### Слежение за уставкой с пределом скорости

```csharp
var tracker = new ModelPredictiveController(A, B, new Matrix(new double[,] { { 1, 0 }, { 0, 0.01 } }), R, 25)
{
    InputLower = new Vector(new[] { -2.0 }),
    InputUpper = new Vector(new[] { 2.0 }),
    StateUpper = new Vector(new[] { double.PositiveInfinity, 1.0 }),   // скорость ≤ 1, мягко
    StateReference = new Vector(new[] { 10.0, 0.0 })
};

var mover = new DiscreteLtiModel(A, B, C, D, new Vector(2));
double peak = 0;

for (int k = 0; k < 300; k++)
{
    mover.Step(tracker.Compute(mover.State));
    peak = Math.Max(peak, mover.State[1]);
}

// Без предела скорость разгона превышает 1.2; с ним — не больше 1, и положение приходит к 10
Console.WriteLine($"положение {mover.State[0]:F3}, наибольшая скорость {peak:F4}");
```

### Ограничение скорости изменения управления

```csharp
var smooth = new ModelPredictiveController(A, B, Q, R, 15)
{
    InputLower = new Vector(new[] { -1.0 }),
    InputUpper = new Vector(new[] { 1.0 }),
    RateLower = new Vector(new[] { -0.2 }),
    RateUpper = new Vector(new[] { 0.2 })
};

var smoothPlant = new DiscreteLtiModel(A, B, C, D, new Vector(new[] { 5.0, 0.0 }));
double largestJump = 0, previousInput = 0;

for (int k = 0; k < 200; k++)
{
    double input = smooth.Compute(smoothPlant.State)[0];
    largestJump = Math.Max(largestJump, Math.Abs(input - previousInput));
    previousInput = input;
    smoothPlant.Step(new Vector(new[] { input }));
}

Console.WriteLine($"наибольшее приращение управления {largestJump:F3}");   // ≤ 0.2
```

### Диагностика такта

```csharp
MpcStep last = mpc.Step(plant.State);
Console.WriteLine(last.Status == SolverStatus.Optimal ? "план найден" : "подан резервный план");
Console.WriteLine(last.Interpret());
```

### Горизонт без ограничений

```csharp
Matrix Qf = DiscreteLqr.Design(A, B, Q, R).CostToGo;   // решение DARE P, а не усиление K

foreach (int N in new[] { 1, 5, 10, 20 })
{
    Matrix KN = LinearQuadraticMpc.ComputeFirstGain(A, B, Q, R, Qf, N);
    Console.WriteLine($"N={N:D2}  K=[{KN[0, 0]:F4}, {KN[0, 1]:F4}]");   // при Qf = P — K LQR на любом N
}
```

---

## Сложность

| Этап | Стоимость |
|------|-----------|
| Конструктор: $\Phi$, $\Gamma$, DARE для $Q_f$ | $O(N^2 n^2 m + n^3)$ |
| Первый такт: постоянная часть $H$ | $O(N^3 n^2 m)$, один раз |
| Такт: $g$ и правые части | $O(N^2 n m)$ плюс строки ограничений |
| Итерация QP | $O((Nm + |W|)^3)$ — LU системы ККТ; с тёплым стартом итераций обычно единицы |
| Память | $O(N^2 n m)$ |

---

## Замечания

- Задача плотная: рассчитано на $Nm$ до нескольких сотен переменных.
- Терминального множества нет: при жёстких ограничениях на состояние выполнимость на следующих тактах
  и устойчивость не гарантированы. На практике помогают мягкие ограничения (по умолчанию),
  $Q_f = P$ и достаточно длинный горизонт.
- Модель линейная и стационарная. Интегрального действия нет: при ошибке модели остаётся статическая
  ошибка, её компенсируют через `StateReference` и `InputReference` или оценкой возмущения.
- Резервный план обрезается только по пределам управления, не по скорости.
- Уставка постоянна на горизонте.
- Прежний пример в этом документе передавал в `ComputeFirstGain` вместо $Q_f$ усиление
  `DiscreteLqr.Solve` (1×2 вместо 2×2) и падал на проверке размеров. Решение DARE —
  `DiscreteLqr.Design(...).CostToGo`.

---

## Проверка

Тесты `ControlSystemsAdvancedTests`:

- без ограничений первое управление совпадает с $-K_{LQR}x$ на горизонтах 1, 5 и 20 до 7 знаков,
  а при произвольном $Q_f$ — с рекурсией Риккати `LinearQuadraticMpc` до 8 знаков;
- на горизонте 1 с пределами управление равно обрезанному безусловному оптимуму — одномерная
  выпуклая задача сверена с замкнутым ответом;
- первый пример: 400 тактов, каждый решён оптимально, $|u| \le 1$, объект приходит в ноль;
- второй пример: без предела пиковая скорость больше 1.2, с пределом — не больше $1 + 10^{-3}$,
  положение $10 \pm 0.05$;
- третий пример: приращение управления на каждом такте не больше 0.2;
- несовместное жёсткое ограничение даёт резервный план из прошлого решения, а мягкое — оптимум
  с нарушением ровно 3.9 и полным торможением.
