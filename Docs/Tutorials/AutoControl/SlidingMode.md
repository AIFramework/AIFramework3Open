# Управление в скользящем режиме

**Пространство имён:** `AI.ControlSystems.Nonlinear`  
**Классы:** `SlidingModeController` (скалярный по ошибке), `StateSlidingModeController` (по состоянию), `SuperTwistingController` (второго порядка)

---

## Постановка задачи

Нужно регулирование, **нечувствительное** к ограниченным возмущениям и неточностям модели. Идея
скользящего режима: выбрать поверхность $s(x) = 0$, на которой движение системы желаемое, и
разрывным управлением за конечное время вывести траекторию на неё и удерживать. На поверхности
система перестаёт замечать возмущения, действующие по каналу управления («согласованные»).

---

## Теория

### Скалярный закон по ошибке

Ошибка $e = r - y$, поверхность $s = e + \lambda\dot e$. На поверхности $\dot e = -e/\lambda$:
ошибка затухает с постоянной времени $\lambda$. Закон

$$u = +k\,\mathrm{sat}(s/\Phi)$$

рассчитан на объект с **положительным** усилением: рост $u$ увеличивает $y$. Тогда при $s > 0$
(выход ниже задания) управление положительно и тянет выход вверх. Прежняя версия возвращала
$-k\,\mathrm{sat}(s/\Phi)$ и уводила выход от задания: на $\dot y = -y + u$ выход уходил к $-9.5$
вместо $1$.

Функция насыщения $\mathrm{sat}(z) = \max(-1, \min(1, z))$ с пограничным слоем $\Phi$ убирает
дребезг: внутри слоя закон пропорциональный с коэффициентом $k/\Phi$, снаружи — релейный.
При $\Phi = 0$ получается чистый $\mathrm{sign}$.

### Закон по состоянию

Объект $\dot x = Ax + B(u + d)$, поверхность $s = Sx$ ($S$ размером m×n). Эквивалентное
управление удерживает $\dot s = 0$: $u_{eq} = -(SB)^{-1}SAx$. К нему добавляется разрывная часть:

$$u = -(SB)^{-1}\bigl(SAx + K\,\mathrm{sat}(s/\Phi)\bigr).$$

Тогда $\dot s_i = -K_i\,\mathrm{sat}(s_i/\Phi) + (SBd)_i$, и при $K_i > |(SBd)_i|$ поверхность
достигается за время не больше $|s_i(0)| / (K_i - |(SBd)_i|)$. На поверхности движение описывается
уравнением $Sx = 0$ и от $d$ не зависит.

### Super-twisting

Для скользящей переменной первой относительной степени $\dot s = u + d(t)$ с $|\dot d| \le L$:

$$u = -\alpha\sqrt{|s|}\,\mathrm{sign}(s) + v, \qquad \dot v = -\beta\,\mathrm{sign}(s).$$

Управление **непрерывно** — разрывна только производная интегратора $v$, — а $s$ и $\dot s$
обращаются в ноль за конечное время. Интегратор оценивает возмущение: $v \to -d(t)$.
`ForDisturbanceRate(L)` берёт рекомендацию Левана: $\alpha = 1.5\sqrt L$, $\beta = 1.1L$.

---

## API

### `SlidingModeController`

| Член | Описание |
|------|----------|
| `Lambda` | $\lambda \ge 0$ в $s = e + \lambda\dot e$ (по умолчанию 1). |
| `Gain` | $k > 0$ (по умолчанию 1). |
| `SmoothingBoundary` | $\Phi$; 0 — релейный закон. |
| `Compute(setpoint, measured, dt)` | Шаг; производная ошибки — разность назад. |
| `Reset()` | Сброс истории. |

### `StateSlidingModeController`

| Член | Описание |
|------|----------|
| `StateSlidingModeController(A, B, S)` | Непрерывная модель и поверхность; $SB$ должна быть обратима. |
| `SwitchingGain` | Вектор $K$ (по умолчанию единицы). |
| `BoundaryLayer` | $\Phi$; 0 — релейный закон. |
| `Surface(x)` | $s = Sx$. |
| `Compute(x)` | Управление $u$ (длина m). |

### `SuperTwistingController`

| Член | Описание |
|------|----------|
| `SuperTwistingController(alpha, beta)` | Явные коэффициенты. |
| `ForDisturbanceRate(L)` | Коэффициенты по границе $|\dot d| \le L$. |
| `Compute(s, dt)` | Шаг; возвращает $u$. |
| `Integral` | $v$ — оценка $-d$. |
| `Reset()` | Обнуляет $v$. |

---

## Примеры

### Объект первого порядка

```csharp
using AI.ControlSystems.Nonlinear;
using AI.DataStructs.Algebraic;

// Объект ẏ = −y + u; при λ = 0 поверхность s = e
var smc = new SlidingModeController { Lambda = 0, Gain = 10, SmoothingBoundary = 0.05 };
double y = 0, dt = 0.001;

for (int k = 0; k < 3000; k++)
{
    double u = smc.Compute(1.0, y, dt);
    y += (-y + u) * dt;
}

// Внутри слоя закон пропорциональный с коэффициентом k/Φ = 200: y = 200/201 ≈ 0.995
Console.WriteLine($"y = {y:F4}");
```

### Объект второго порядка с возмущением

```csharp
// ÿ = −0.5ẏ + u + 0.3·sin t
var smc2 = new SlidingModeController { Lambda = 3, Gain = 15, SmoothingBoundary = 0.05 };
double position = 0, speed = 0;

for (int k = 0; k < 20_000; k++)
{
    double t = k * dt;
    double u = smc2.Compute(setpoint: 1.0, measured: position, dt: dt);
    speed += (-0.5 * speed + u + 0.3 * Math.Sin(t)) * dt;
    position += speed * dt;
}

Console.WriteLine($"положение {position:F4}");   // ≈ 1: на поверхности ошибка затухает за ~3λ
```

### Двойной интегратор по состоянию

```csharp
// ẍ = u + d, |d| ≤ 0.4; поверхность s = 2x + ẋ, на ней ẋ = −2x
var ac = new Matrix(new double[,] { { 0, 1 }, { 0, 0 } });
var bc = new Matrix(new double[,] { { 0 }, { 1 } });
var surface = new Matrix(new double[,] { { 2, 1 } });

var stateSmc = new StateSlidingModeController(ac, bc, surface)
{
    SwitchingGain = new Vector(new[] { 1.0 }),   // K > max|d|
    BoundaryLayer = 0.005
};

var x = new Vector(new[] { 1.0, 0.0 });
double h = 1e-4;

for (int k = 0; k < 80_000; k++)
{
    double d = 0.4 * Math.Sin(2 * k * h);
    double control = stateSmc.Compute(x)[0];
    x = new Vector(new[] { x[0] + x[1] * h, x[1] + (control + d) * h });
}

// Поверхность достигнута не позже |s₀|/(K − max|d|) = 2/0.6 с, дальше x → 0 независимо от d
Console.WriteLine($"x = {x[0]:E2}, s = {stateSmc.Surface(x)[0]:E2}");
```

### Super-twisting

```csharp
// ṡ = u + d, d = 0.5·sin t, |ḋ| ≤ 0.5
var twisting = SuperTwistingController.ForDisturbanceRate(0.5);
double s = 1;

for (int k = 0; k < 10_000; k++)
    s += (twisting.Compute(s, 1e-3) + 0.5 * Math.Sin(k * 1e-3)) * 1e-3;

// Интегратор восстановил возмущение: −v ≈ 0.5·sin(10) ≈ −0.27
Console.WriteLine($"s = {s:E2}, оценка возмущения {-twisting.Integral:F3}");
```

---

## Сложность

| Регулятор | Шаг | Подготовка |
|-----------|-----|------------|
| `SlidingModeController` | $O(1)$ | — |
| `StateSlidingModeController` | $O(mn + m^2)$ | $(SB)^{-1}$ и $SA$ один раз, $O(m^3 + mn^2)$ |
| `SuperTwistingController` | $O(1)$ | — |

---

## Замечания

- Скалярный закон предполагает положительное усиление объекта. Для отрицательного поменяйте знак
  `Gain` или измеряемой величины.
- При $\lambda > 0$ производная ошибки берётся разностью назад и усиливает шум измерения.
- Закон по состоянию требует модели $A$, $B$ и подавляет только согласованные возмущения — те, что
  входят через $B$. Несогласованные смещают движение на поверхности.
- С пограничным слоем точность $O(\Phi)$, а не ноль: это цена отсутствия дребезга.
- Super-twisting применим к переменной первой относительной степени. При дискретной реализации
  с шагом $\Delta t$ точность по $s$ порядка $\Delta t^2$.

---

## Проверка

Тесты `ControlSystemsAdvancedTests` и `ControlSystemsRegressionTests`:

- закон по состоянию из примера выше достигает поверхности раньше оценки $2/0.6$ с, а конечное
  состояние с возмущением и без него совпадает с точностью 0.005;
- super-twisting: $|s| < 10^{-3}$ после 5 с, скачок управления за шаг меньше 0.05, интегратор равен
  $-d(10)$ с точностью 0.05;
- скалярный закон на объектах первого и второго порядка из примеров приходит к заданию.
