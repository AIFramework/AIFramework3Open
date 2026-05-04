# PID-регулятор

**Пространство имён:** `AI.ControlSystems.Pid`  
**Классы:** `PidController`, `VectorPidController`, `ImcPidTuning`, `SlewRateLimiter`

---

## Теория

### Параллельная форма

Дискретный ПИД вычисляет управляющее воздействие по трём составляющим:

$$u[k] = K_p\,e[k] + K_i \sum_{j=0}^{k} e[j]\,\Delta t + K_d\,\frac{e[k]-e[k-1]}{\Delta t} + u_{ff}$$

где $e[k] = r[k] - y[k]$ — ошибка регулирования, $r$ — уставка, $y$ — измерение, $u_{ff}$ — упреждение (feedforward).

### Производная по измерению

При скачке уставки $r$ производная ошибки $\dot e$ мгновенно возрастает, что вызывает «выброс» управления. Альтернатива — дифференцировать только измерение:

$$D = -K_d\,\frac{y[k]-y[k-1]}{\Delta t}$$

Флаг `DerivativeOnMeasurement = true` включает этот режим.

### Фильтр дифференциальной составляющей

Сырая разность $\Delta e / \Delta t$ усиливает шум измерения. Фильтр первого порядка с постоянной времени $\tau$:

$$D_f[k] = D_f[k-1] + \alpha\,(D_{raw}[k] - D_f[k-1]),\quad \alpha = \frac{\Delta t}{\tau + \Delta t}$$

Задаётся свойством `DerivativeFilterTau`.

### Насыщение интегратора (anti-windup)

При ограниченном выходе исполнительного механизма интегральная составляющая может «накапливаться» до больших значений — **windup**. Два способа борьбы:

1. **Clamp** (`IntegralClamp`) — симметричное ограничение $|\int e\,dt| \le I_{max}$.
2. **Tracking** (`UseAntiWindupTracking`) — при насыщении выхода интеграл корректируется обратно:
   $$\int e\,dt \mathrel{+}= T_t\,\frac{u_{sat} - u_{pre}}{K_i}$$
   где $T_t$ = `AntiWindupTrackingGain`.

### IMC-настройка PI

Для объекта первого порядка $G(s) = \frac{K}{\tau s + 1}$ и желаемой постоянной времени замкнутого контура $\lambda$:

$$K_p = \frac{\tau}{K\lambda},\quad K_i = \frac{K_p}{\tau}$$

### Ограничение скорости (slew rate)

Ограничивает приращение выходного сигнала за один шаг:

$$u[k] = u[k-1] + \mathrm{clamp}(u_{desired}[k] - u[k-1],\; \delta_{min},\; \delta_{max})$$

---

## API

### `PidController`

| Член | Тип | Описание |
|------|-----|----------|
| `Kp`, `Ki`, `Kd` | `double` | Коэффициенты П, И, Д. |
| `DerivativeOnMeasurement` | `bool` | Производная по измерению (не по ошибке). |
| `DerivativeFilterTau` | `double?` | Постоянная времени фильтра Д-составляющей. |
| `IntegralClamp` | `double?` | Симметричное ограничение интеграла. |
| `UseAntiWindupTracking` | `bool` | Anti-windup tracking. |
| `AntiWindupTrackingGain` | `double` | Коэффициент tracking (по умолчанию 1). |
| `OutputMin`, `OutputMax` | `double?` | Ограничение выхода. |
| `Feedforward` | `double` | Постоянная добавка к выходу. |
| `IntegralOfError` | `double` | Текущее $\int e\,dt$. |
| `Compute(r, y, dt)` | `double` | Один шаг регулятора. |
| `Reset()` | `void` | Сброс интеграла и истории. |

### `VectorPidController`

| Член | Описание |
|------|----------|
| `VectorPidController(n)` | Создаёт `n` независимых SISO PID. |
| `this[i]` | Доступ к `PidController` канала `i`. |
| `Compute(setpoint, measured, dt)` | Возвращает `Vector` управлений. |
| `Reset()` | Сброс всех каналов. |

### `ImcPidTuning`

```csharp
ImcPidTuning.FirstOrderPi(K, tau, lambda, out double kp, out double ki);
```

### `SlewRateLimiter`

| Член | Описание |
|------|----------|
| `SlewRateLimiter(dMin, dMax)` | Конструктор с границами приращения. |
| `Limit(desired)` | Ограничивает желаемое значение. |
| `Reset(initial)` | Устанавливает начальное значение. |
| `LastOutput` | Последнее применённое значение. |

---

## Примеры

### Базовый PID

```csharp
using AI.ControlSystems.Pid;

var pid = new PidController(kp: 2.0, ki: 0.5, kd: 0.1);

double dt = 0.01;
double setpoint = 10.0;
double measured = 0.0;

for (int k = 0; k < 1000; k++)
{
    double u = pid.Compute(setpoint, measured, dt);
    // применить u к объекту, получить новое measured
}
```

### PID с фильтром D и anti-windup

```csharp
var pid = new PidController(2.0, 0.5, 0.1)
{
    DerivativeOnMeasurement = true,
    DerivativeFilterTau     = 0.05,   // фильтр с τ = 0.05 с
    OutputMin               = -100,
    OutputMax               =  100,
    UseAntiWindupTracking   = true,
    AntiWindupTrackingGain  = 0.5
};
```

### Настройка через IMC

```csharp
// Объект: K=2, τ=3 с; желаемая постоянная замкнутого контура λ=1 с
ImcPidTuning.FirstOrderPi(processGain: 2.0, timeConstant: 3.0,
    imcFilterTimeConstant: 1.0, out double kp, out double ki);

var pid = new PidController(kp, ki, 0);
```

### Многоканальный PID

```csharp
using AI.DataStructs.Algebraic;

var vpid = new VectorPidController(3);
vpid[0].Kp = 1.0; vpid[0].Ki = 0.1;
vpid[1].Kp = 1.5; vpid[1].Ki = 0.2;
vpid[2].Kp = 0.8; vpid[2].Ki = 0.05;

Vector sp = new Vector(new[] { 1.0, 2.0, 3.0 });
Vector y  = new Vector(new[] { 0.0, 0.0, 0.0 });

Vector u = vpid.Compute(sp, y, dt: 0.01);
```

### Ограничение скорости

```csharp
var slew = new SlewRateLimiter(minDeltaPerStep: -5, maxDeltaPerStep: 5);
slew.Reset(0);

double desired = 100;
double limited = slew.Limit(desired); // не более +5 от предыдущего
```

---

## Типичные ошибки

| Проблема | Причина | Решение |
|----------|---------|---------|
| Выброс при скачке уставки | Производная по ошибке | `DerivativeOnMeasurement = true` |
| Шум на выходе | Нет фильтра D | Задать `DerivativeFilterTau` |
| Интегральное насыщение | Нет anti-windup | `IntegralClamp` или `UseAntiWindupTracking` |
| Резкие скачки управления | Нет slew rate | `SlewRateLimiter` |
