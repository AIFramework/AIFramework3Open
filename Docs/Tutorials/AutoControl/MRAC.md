# Адаптивное управление с эталонной моделью (MRAC)

**Пространство имён:** `AI.ControlSystems.Adaptive`  
**Классы:** `ModelReferenceAdaptiveController` (выход первого порядка), `StateModelReferenceAdaptiveController` (по состоянию)

---

## Постановка задачи

Параметры объекта неизвестны или меняются, но известно, как система **должна** себя вести: это
поведение задаёт **эталонная модель**. Регулятор подстраивает свои коэффициенты на ходу так, чтобы
ошибка $e = y - y_m$ между объектом и эталоном стремилась к нулю. Устойчивость доказывается
функцией Ляпунова, а не предполагается: закон адаптации выводится из требования $\dot V \le 0$.

---

## Теория

### Объект первого порядка

Объект $\dot y = a\,y + b\,u$ с неизвестными $a$, $b$; известен только знак $b$. Эталон:

$$\dot y_m = -a_m y_m + a_m r, \qquad a_m > 0.$$

Управление содержит **два** коэффициента — при задании и при выходе:

$$u = \theta_r\,r + \theta_y\,y.$$

Одного коэффициента мало: при $u = \theta r$ полюс замкнутой системы остаётся равным $a$, и
неустойчивый объект таким регулятором не стабилизировать. Идеальные значения, при которых объект
совпадает с эталоном:

$$\theta_r^* = \frac{a_m}{b}, \qquad \theta_y^* = -\frac{a + a_m}{b}.$$

Для ошибки $e = y - y_m$ получаем $\dot e = -a_m e + b(\tilde\theta_r r + \tilde\theta_y y)$, где
$\tilde\theta = \theta - \theta^*$. Функция Ляпунова
$V = \tfrac12 e^2 + \tfrac{|b|}{2\gamma}(\tilde\theta_r^2 + \tilde\theta_y^2)$ убывает,
$\dot V = -a_m e^2 \le 0$, если

$$\dot\theta_r = -\gamma\,\mathrm{sgn}(b)\,e\,r, \qquad \dot\theta_y = -\gamma\,\mathrm{sgn}(b)\,e\,y.$$

Отсюда $e \to 0$ (лемма Барбалата). Коэффициенты приходят к идеальным, только если задание
**возбуждает** систему: ступень оставляет их на прямой решений, меандр — сводит к точке.

Эталон интегрируется точно, при постоянном на шаге $r$:
$y_m \leftarrow r + (y_m - r)\,e^{-a_m \Delta t}$.

### Объект по состоянию

Объект $\dot x = A x + B\Lambda u$: матрица $A$ и диагональная $\Lambda > 0$ неизвестны, $B$
известна. Эталон $\dot x_m = A_m x_m + B_m r$ с устойчивой $A_m$. Управление
$u = K_x x + K_r r$; условия согласования: $A + B\Lambda K_x^* = A_m$, $B\Lambda K_r^* = B_m$.

Закон адаптации (Лавретский, Вайз):

$$\dot K_x = -\Gamma_x B^\top P e\,x^\top, \qquad \dot K_r = -\Gamma_r B^\top P e\,r^\top,$$

где $P$ — решение уравнения Ляпунова $A_m^\top P + P A_m = -Q$. Оно решается один раз в
конструкторе через `LyapunovEquation.SolveContinuous`. Необязательная утечка $\sigma$ добавляет
$-\sigma K$ к обоим законам: коэффициенты перестают дрейфовать под шумом, но слежение становится
неточным.

---

## API

### `ModelReferenceAdaptiveController`

| Член | Описание |
|------|----------|
| `AdaptationGain` | $\gamma > 0$ (по умолчанию 0.1). |
| `ReferencePole` | $a_m > 0$ (по умолчанию 1). |
| `Theta` | $\theta_r$ — коэффициент при задании. |
| `FeedbackGain` | $\theta_y$ — коэффициент при выходе (по умолчанию 0). |
| `PlantGainSign` | Знак $b$: `1` или `-1`. |
| `ReferenceOutput` | $y_m$. |
| `TrackingError` | $e = y - y_m$ после последнего шага. |
| `Compute(r, y, dt)` | Шаг эталона и адаптации; возвращает $u$. |
| `Reset(y0)` | Сброс эталона. |

### `StateModelReferenceAdaptiveController`

| Член | Описание |
|------|----------|
| `StateModelReferenceAdaptiveController(Am, Bm, B, Q = I)` | $A_m$ должна быть устойчивой. |
| `StateAdaptationGain`, `ReferenceAdaptationGain` | $\Gamma_x$, $\Gamma_r$ (скаляры). |
| `Leakage` | $\sigma \ge 0$. |
| `LyapunovSolution` | $P$. |
| `StateGain`, `ReferenceGain` | $K_x$ (m×n), $K_r$ (m×q). |
| `ReferenceState`, `TrackingError` | $x_m$ и $e = x - x_m$. |
| `Compute(x, r, dt)` | Шаг; возвращает $u$ (длина m). |
| `Reset(xm0, Kx0, Kr0)` | Сброс эталона и коэффициентов. |

---

## Примеры

### Неустойчивый объект первого порядка

```csharp
using AI.ControlSystems.Adaptive;
using AI.DataStructs.Algebraic;

// Объект ẏ = y + 2u неустойчив; a = 1 и b = 2 регулятору неизвестны, известен знак b
var mrac = new ModelReferenceAdaptiveController
{
    AdaptationGain = 5,
    ReferencePole  = 3,   // эталон с постоянной времени 1/3 с
    Theta          = 0,
    FeedbackGain   = 0,
    PlantGainSign  = 1
};

double y = 0, dt = 0.001;

for (int k = 0; k < 60_000; k++)
{
    double t = k * dt;
    double r = Math.Floor(t / 5) % 2 == 0 ? 1 : -1;   // меандр возбуждает оба коэффициента
    double u = mrac.Compute(r, y, dt);
    y += (y + 2 * u) * dt;

    if (k % 10_000 == 0)
        Console.WriteLine($"t={t,5:F1}  y={y,7:F4}  ym={mrac.ReferenceOutput,7:F4}  θr={mrac.Theta:F3}  θy={mrac.FeedbackGain:F3}");
}
// Идеальные значения: θr* = am/b = 1.5, θy* = −(a + am)/b = −2
```

### Объект второго порядка по состоянию

```csharp
// Истинный объект: A = [0 1; 2 −1] (неустойчив), Λ = 1.5; регулятор знает только B = [0; 1]
var am = new Matrix(new double[,] { { 0, 1 }, { -4, -2.8 } });   // ω = 2, ζ = 0.7
var bm = new Matrix(new double[,] { { 0 }, { 4 } });
var inputMatrix = new Matrix(new double[,] { { 0 }, { 1 } });

var stateMrac = new StateModelReferenceAdaptiveController(am, bm, inputMatrix)
{
    StateAdaptationGain     = 20,
    ReferenceAdaptationGain = 20
};

var x = new Vector(2);
double step = 0.001;

for (int k = 0; k < 120_000; k++)
{
    double t = k * step;
    var r = new Vector(new[] { Math.Floor(t / 5) % 2 == 0 ? 1.0 : -1.0 });
    Vector control = stateMrac.Compute(x, r, step);

    double velocity = x[1];
    double acceleration = 2 * x[0] - x[1] + 1.5 * control[0];
    x = new Vector(new[] { x[0] + velocity * step, x[1] + acceleration * step });
}

// Условия согласования дают Kx* = [−6, −1.8]/Λ = [−4, −1.2], Kr* = 4/Λ ≈ 2.67
Matrix kx = stateMrac.StateGain;
Console.WriteLine($"Kx = [{kx[0, 0]:F2}, {kx[0, 1]:F2}], Kr = {stateMrac.ReferenceGain[0, 0]:F2}");
```

---

## Сложность

| Операция | Стоимость |
|----------|-----------|
| Шаг MRAC первого порядка | $O(1)$ |
| Шаг MRAC по состоянию | $O(n^2 + nm + mq)$ |
| Конструктор MRAC по состоянию | $O(n^6)$ — уравнение Ляпунова через кронекерово произведение, $n \le 30$ |
| Дискретизация эталона при смене `dt` | $O(n^3)$, результат кэшируется |

---

## Замечания

- Знак усиления объекта ($b$ или $\Lambda$) должен быть известен: он задаёт направление адаптации.
- Законы адаптации интегрируются методом Эйлера. Шаг должен быть мал по сравнению с $1/(\gamma\,\|x\|^2)$,
  иначе дискретная адаптация сама становится неустойчивой.
- Проекции коэффициентов на допустимое множество нет, есть только утечка $\sigma$. При ограниченном
  возмущении без утечки коэффициенты могут медленно дрейфовать — классическая проблема робастности MRAC.
- MRAC по выходу для объектов относительной степени выше единицы (нужны фильтры и расширенная ошибка)
  не реализован.
- Прежняя версия имела один коэффициент $u = \theta r$ и обратный знак адаптации. На неустойчивом
  объекте $\dot y = y + 2u$ ошибка слежения достигала $7.6\cdot10^{86}$; регрессионный тест
  `FirstOrderMrac_TracksUnstablePlant` фиксирует исправление.

---

## Проверка

Тесты `ControlSystemsAdvancedTests` и `ControlSystemsRegressionTests`:

- объект $\dot y = 0.5y - 2u$ с отрицательным усилением: $\theta_r \to a_m/b$, $\theta_y \to -(a + a_m)/b$
  с точностью 0.05;
- объект по состоянию из примера выше: ошибка слежения после 110 с меньше 0.02, $K_x$ и $K_r$ совпадают
  с условиями согласования с точностью 0.3;
- неустойчивый объект первого порядка из первого примера: ошибка слежения после 50 с меньше 0.05.
