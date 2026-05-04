# Дискретная модель в пространстве состояний и ZOH-дискретизация

**Пространство имён:** `AI.ControlSystems.Linear`  
**Классы:** `DiscreteLtiModel`, `Discretization`

---

## Теория

### Пространство состояний (непрерывное время)

Линейная стационарная система описывается уравнениями:

$$\dot x = A_c x + B_c u, \quad y = C_c x + D_c u$$

где $x \in \mathbb{R}^n$ — вектор состояния, $u \in \mathbb{R}^m$ — управление, $y \in \mathbb{R}^p$ — выход.

### Дискретная модель

При цифровом управлении с шагом $\Delta t$ переходим к разностным уравнениям:

$$x[k+1] = A\,x[k] + B\,u[k], \quad y[k] = C\,x[k] + D\,u[k]$$

### ZOH-дискретизация (Zero-Order Hold)

При кусочно-постоянном управлении $u(t) = u[k]$ на интервале $[k\Delta t,\,(k+1)\Delta t)$ точные дискретные матрицы:

$$A_d = e^{A_c \Delta t}, \qquad B_d = \left(\int_0^{\Delta t} e^{A_c \tau}\,d\tau\right) B_c$$

Матричная экспонента вычисляется через ряд Тейлора с масштабированием и возведением в степень двойки (алгоритм scaling-and-squaring).

---

## API

### `DiscreteLtiModel`

| Член | Описание |
|------|----------|
| `DiscreteLtiModel(A, B, C)` | Без прямой связи (D = 0), нулевое начальное состояние. |
| `DiscreteLtiModel(A, B, C, D)` | С матрицей прямой связи. |
| `DiscreteLtiModel(A, B, C, D, x0)` | С начальным состоянием. |
| `Step(u)` | Один шаг: $x \leftarrow Ax+Bu$, возвращает $y = Cx+Du$. |
| `OutputFor(u)` | Выход без обновления состояния. |
| `Reset()` | Сброс состояния в ноль. |
| `State` | Текущий вектор состояния $x$. |
| `StateDimension`, `InputDimension`, `OutputDimension` | Размерности $n$, $m$, $p$. |

### `Discretization`

| Метод | Описание |
|-------|----------|
| `ZeroOrderHold(Ac, Bc, dt, out Ad, out Bd)` | Вычисляет $A_d$, $B_d$ по ZOH. |
| `ZeroOrderHoldModel(Ac, Bc, Cc, dt, x0)` | Создаёт `DiscreteLtiModel` сразу. |

---

## Примеры

### Создание дискретной модели вручную

```csharp
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;

// Двойной интегратор: x1' = x2, x2' = u
var A = new Matrix(new double[,] { { 1, 0.01 }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.00005 }, { 0.01 } });
var C = new Matrix(new double[,] { { 1, 0 } });

var model = new DiscreteLtiModel(A, B, C);

var u = new Vector(new[] { 1.0 });
for (int k = 0; k < 100; k++)
{
    Vector y = model.Step(u);
    Console.WriteLine($"y = {y[0]:F4}");
}
```

### ZOH-дискретизация непрерывной модели

```csharp
// Непрерывный двойной интегратор
var Ac = new Matrix(new double[,] { { 0, 1 }, { 0, 0 } });
var Bc = new Matrix(new double[,] { { 0 }, { 1 } });
var Cc = new Matrix(new double[,] { { 1, 0 } });

double dt = 0.01;
var x0 = new Vector(2);  // нулевое начальное состояние

var model = Discretization.ZeroOrderHoldModel(Ac, Bc, Cc, dt, x0);

// Теперь model.A, model.B — точные дискретные матрицы
Console.WriteLine($"Ad[0,1] = {model.A[0, 1]:F6}");  // ≈ 0.01
Console.WriteLine($"Bd[1,0] = {model.B[1, 0]:F6}");  // ≈ 0.01
```

### Только матрицы Ad, Bd

```csharp
Discretization.ZeroOrderHold(Ac, Bc, dt: 0.01, out Matrix Ad, out Matrix Bd);
```

---

## Замечания

- Размерности: `A` — $n \times n$, `B` — $n \times m$, `C` — $p \times n$, `D` — $p \times m$.
- `Step` обновляет `State`; `OutputFor` — только вычисляет выход без изменения состояния.
- Для нелинейных объектов используйте `ExtendedKalmanFilter` с ручной линеаризацией.
