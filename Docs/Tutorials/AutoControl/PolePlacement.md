# Размещение полюсов (формула Аккермана)

**Пространство имён:** `AI.ControlSystems.Linear`  
**Классы:** `PolePlacement`, `SystemAnalysis`

---

## Постановка задачи

Для управляемой системы $x_{k+1} = Ax_k + Bu_k$ с одним входом ($m = 1$) найти строку усилений
$K$, при которой замкнутая система $x_{k+1} = (A - BK)x_k$ имеет заданные собственные значения
(полюса) $\lambda_1, \ldots, \lambda_n$. Полюса определяют скорость затухания и колебательность
переходного процесса.

---

## Теория

### Желаемый полином

$$p(\lambda) = \prod_{i=1}^{n}(\lambda - \lambda_i) = \lambda^n + c_{n-1}\lambda^{n-1} + \cdots + c_0.$$

Коэффициенты вещественны, если комплексные полюса идут сопряжёнными парами.
`PolePlacement.PolynomialFromPoles` раскрывает произведение и отвергает непарный комплексный полюс.

### Управляемость

Пара $(A, B)$ управляема, если матрица управляемости имеет полный ранг:

$$\mathcal{W} = \begin{bmatrix} B & AB & A^2B & \cdots & A^{n-1}B \end{bmatrix}.$$

Ранг считается по сингулярным числам (`SystemAnalysis.Rank`), а не по определителю: определитель
$\mathcal{W}$ зависит от масштаба. У хорошо обусловленной системы с шагом 0.1 он бывает порядка
$10^{-6}$ и ничего не говорит о вырожденности.

### Формула Аккермана

$$K = e_n^\top \mathcal{W}^{-1} \varphi(A), \qquad \varphi(A) = A^n + c_{n-1}A^{n-1} + \cdots + c_0 I,$$

где $e_n = [0, \ldots, 0, 1]^\top$. Закон управления $u = -Kx$.

### Наблюдатель по двойственности

Полюса наблюдателя Люенбергера $A - LC$ размещаются той же формулой для пары $(A^\top, C^\top)$:
$L = \bigl(\text{Ackermann}(A^\top, C^\top)\bigr)^\top$.

### Выбор полюсов

- Устойчивость дискретной системы: все $|\lambda_i| < 1$.
- Чем ближе полюса к нулю, тем быстрее затухание и тем больше управление.
- Комплексные полюса $re^{\pm j\theta}$ дают колебания.
- Полюса наблюдателя обычно ставят в 2–5 раз быстрее полюсов регулятора.

---

## API

### `PolePlacement`

| Метод | Описание |
|-------|----------|
| `AckermannGain(A, B, poles)` | $K$ (1×n) по самим полюсам (`IEnumerable<Complex>`). |
| `AckermannGain(A, B, coeffs)` | $K$ по коэффициентам $[c_0, \ldots, c_{n-1}]$. |
| `PolynomialFromPoles(poles)` | Коэффициенты $[c_0, \ldots, c_{n-1}]$. |
| `ControllabilityMatrix(A, B)` | $\mathcal{W}$ для SISO. |

### `SystemAnalysis`

| Метод | Описание |
|-------|----------|
| `Controllability(A, B)`, `Observability(A, C)` | Матрицы управляемости и наблюдаемости (MIMO). |
| `Rank(M, tolerance)` | Численный ранг по сингулярным числам. |
| `IsControllable`, `IsObservable` | Проверки полного ранга. |
| `Poles(A)` | Собственные значения (`Complex[]`). |
| `SpectralRadius`, `IsStableDiscrete`, `IsStableContinuous` | Устойчивость. |

**Ограничение:** `AckermannGain` — только SISO ($B$ — столбец n×1).

---

## Примеры

### Регулятор для двойного интегратора

```csharp
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;

var A = new Matrix(new double[,] { { 1, 0.1 }, { 0, 1 } });
var B = new Matrix(new double[,] { { 0.005 }, { 0.1 } });

Console.WriteLine($"управляема: {SystemAnalysis.IsControllable(A, B)}");

// Коэффициентами: (λ − 0.7)² = λ² − 1.4λ + 0.49 → [c0, c1] = [0.49, −1.4]
Matrix K = PolePlacement.AckermannGain(A, B, new Vector(new[] { 0.49, -1.4 }));
Console.WriteLine($"K = [{K[0, 0]:F4}, {K[0, 1]:F4}]");

// Самими полюсами: сопряжённая пара 0.8 ± 0.2i
var poles = new System.Numerics.Complex[] { new(0.8, 0.2), new(0.8, -0.2) };
Matrix K2 = PolePlacement.AckermannGain(A, B, poles);

foreach (var pole in SystemAnalysis.Poles(A - B * K2))
    Console.WriteLine($"полюс {pole.Real:F4} {pole.Imaginary:+0.0000;-0.0000}i");
```

### Симуляция замкнутой системы

```csharp
var C = new Matrix(new double[,] { { 1, 0 } });
var D = new Matrix(new double[,] { { 0 } });
var model = new DiscreteLtiModel(A, B, C, D, new Vector(new[] { 5.0, 0.0 }));

for (int k = 0; k < 50; k++)
{
    Vector x = model.State;
    double u = -(K[0, 0] * x[0] + K[0, 1] * x[1]);
    model.Step(new Vector(new[] { u }));

    if (k % 10 == 0)
        Console.WriteLine($"k={k:D2}  x1={x[0]:F4}  x2={x[1]:F4}  u={u:F4}");
}
```

### Наблюдатель по двойственности

```csharp
// Полюса наблюдателя 0.3 и 0.4 — быстрее полюсов регулятора
Matrix L = PolePlacement.AckermannGain(A.Transpose(), C.Transpose(), new System.Numerics.Complex[] { 0.3, 0.4 }).Transpose();
Console.WriteLine($"L = [{L[0, 0]:F4}; {L[1, 0]:F4}]");
```

---

## Сложность

| Операция | Стоимость |
|----------|-----------|
| Матрица управляемости | $O(n^3)$ |
| Проверка ранга (SVD, Якоби) | $O(n^3)$ на развёртку |
| $\varphi(A)$ | $O(n^4)$ — $n$ матричных произведений |
| Обращение $\mathcal{W}$ (LU с выбором главного элемента) | $O(n^3)$ |

---

## Замечания

- Неуправляемая пара даёт `InvalidOperationException` ещё до обращения $\mathcal{W}$.
- Формула Аккермана численно плохо обусловлена: число обусловленности $\mathcal{W}$ растёт
  с порядком. Для $n > 10$ и для MIMO используйте `DiscreteLqr` — он размещает полюса косвенно,
  через веса, и устойчив численно.
- Коэффициенты полинома задаются **от младшей степени к старшей**: $[c_0, c_1, \ldots, c_{n-1}]$.
- Прежняя версия обращала $\mathcal{W}$ через `Matrix.GetInvertMatrix`, а тот считал определитель без
  перестановки строк и сравнивал его с абсолютным порогом $10^{-10}$, поэтому управляемую систему
  третьего порядка с шагом 0.1 объявлял вырожденной. Теперь обращение в ядре идёт через LU с выбором
  главного элемента, а вырожденность определяется относительным порогом $n\varepsilon\|A\|_\infty$.

---

## Проверка

Тесты `ControlSystemsLinearTests` и `ControlSystemsRegressionTests`:

- для системы третьего порядка с комплексной парой полюсов характеристический многочлен $A - BK$,
  посчитанный методом Фаддеева — Леверье без собственных чисел, совпадает с желаемым до 10 знаков;
- собственные числа $A - BK$ совпадают с заданными до $10^{-8}$;
- неуправляемая пара и непарный комплексный полюс отвергаются;
- ошибка наблюдателя, построенного по двойственности, следует $e_{k+1} = (A - LC)e_k$ до 10 знаков.
