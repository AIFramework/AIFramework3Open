# Рекурсивные МНК (RLS)

**Пространство имён:** `AI.ControlSystems.Identification`  
**Класс:** `RecursiveLeastSquares`

---

## Теория

### Задача идентификации

Требуется оценить параметры $\theta \in \mathbb{R}^n$ линейной модели:

$$y[k] = \varphi[k]^\top \theta + \varepsilon[k]$$

где $\varphi[k]$ — вектор регрессоров (известные величины), $y[k]$ — скалярный выход, $\varepsilon$ — шум.

Пример для ARX-модели порядка 2: $y[k] = a_1 y[k-1] + a_2 y[k-2] + b_1 u[k-1] + b_2 u[k-2]$, тогда:

$$\varphi[k] = [y[k-1],\, y[k-2],\, u[k-1],\, u[k-2]]^\top, \quad \theta = [a_1, a_2, b_1, b_2]^\top$$

### Рекурсивный алгоритм

Вместо пакетного МНК $\hat\theta = (\Phi^\top\Phi)^{-1}\Phi^\top Y$ используется рекурсия:

$$\hat\theta[k] = \hat\theta[k-1] + K[k]\,\bigl(y[k] - \varphi[k]^\top\hat\theta[k-1]\bigr)$$

$$K[k] = \frac{P[k-1]\varphi[k]}{\lambda + \varphi[k]^\top P[k-1]\varphi[k]}$$

$$P[k] = \frac{1}{\lambda}\bigl(P[k-1] - K[k]\varphi[k]^\top P[k-1]\bigr)$$

Матрица $P$ — ковариация ошибки оценки (обратная к информационной матрице).

### Коэффициент забывания

$\lambda \in (0, 1]$ — **forgetting factor**:
- $\lambda = 1$: все данные равновесны (стационарный объект).
- $\lambda < 1$: старые данные «забываются», алгоритм отслеживает нестационарные параметры.
- Типичные значения: $\lambda \in [0.95, 0.99]$.

### Инициализация

- $\hat\theta[0]$: начальная оценка (нули или грубое приближение).
- $P[0] = \alpha I$: большое $\alpha$ (например, $10^4$) означает большую начальную неопределённость.

---

## API

| Член | Описание |
|------|----------|
| `RecursiveLeastSquares(theta0, P0)` | Инициализация начальными $\theta$ и $P$. |
| `Update(phi, y)` | Один шаг обновления по паре $(\varphi, y)$. |
| `Theta` | Текущая оценка параметров. |
| `ForgettingFactor` | Коэффициент забывания $\lambda$ (по умолчанию 1). |

---

## Примеры

### Идентификация ARX-модели первого порядка

```csharp
using AI.ControlSystems.Identification;
using AI.DataStructs.Algebraic;

// Реальный объект: y[k] = 0.8*y[k-1] + 0.5*u[k-1] + шум
// Идентифицируем theta = [a1, b1]

int n = 2;
var theta0 = new Vector(new[] { 0.0, 0.0 });
var P0 = new Matrix(n, n);
P0[0, 0] = 1e4;
P0[1, 1] = 1e4;

var rls = new RecursiveLeastSquares(theta0, P0)
{
    ForgettingFactor = 1.0
};

double yPrev = 0, uPrev = 0;
var rng = new Random(42);

for (int k = 0; k < 500; k++)
{
    double u = Math.Sin(0.1 * k);
    double y = 0.8 * yPrev + 0.5 * uPrev + (rng.NextDouble() - 0.5) * 0.02;

    var phi = new Vector(new[] { yPrev, uPrev });
    rls.Update(phi, y);

    if (k % 50 == 0)
        Console.WriteLine($"k={k:D3}  a1={rls.Theta[0]:F4}  b1={rls.Theta[1]:F4}");

    yPrev = y;
    uPrev = u;
}
// Ожидаемый результат: Theta ≈ [0.8, 0.5]
```

### С коэффициентом забывания (нестационарный объект)

```csharp
var rls = new RecursiveLeastSquares(theta0, P0)
{
    ForgettingFactor = 0.97  // отслеживаем медленные изменения параметров
};

// В k=300 объект меняет параметры: a1 меняется с 0.8 на 0.6
for (int k = 0; k < 600; k++)
{
    double a1 = k < 300 ? 0.8 : 0.6;
    double u = Math.Sin(0.1 * k);
    double y = a1 * yPrev + 0.5 * uPrev;

    rls.Update(new Vector(new[] { yPrev, uPrev }), y);
    yPrev = y; uPrev = u;
}
```

### Использование для адаптивного управления

```csharp
// Шаг 1: идентифицируем b1 (усиление объекта)
// Шаг 2: используем оценку для настройки регулятора
double estimatedGain = rls.Theta[1];
double pidKp = 1.0 / estimatedGain;  // простая инверсия усиления
```

---

## Замечания

- RLS требует **персистентного возбуждения** (PE): входной сигнал должен быть достаточно богатым по частотному составу для надёжной идентификации всех параметров.
- При $\lambda < 1$ и отсутствии изменений параметров ковариация $P$ может «взрываться» (covariance windup) — добавьте ограничение на $P$ или периодически сбрасывайте.
- Для многомерного выхода ($y \in \mathbb{R}^p$) запустите $p$ независимых экземпляров RLS.
- Начальная матрица $P_0 = \alpha I$ с большим $\alpha$ обеспечивает быструю начальную сходимость.
