# Скользящие статистики

**Скользящее среднее** (moving average) — непараметрический низкочастотный фильтр. Для окна ширины $W$:

$$
\bar x_n \;=\; \frac{1}{W}\sum_{k=0}^{W-1} x_{n+k}.
$$

Аналогично — **скользящая дисперсия** и СКО:

$$
\hat\sigma^2_n \;=\; \frac{1}{W - 1}\sum_{k=0}^{W-1} (x_{n+k} - \bar x_n)^2.
$$

## Универсальный интерфейс

`AI.Functions.WindowFuncDouble(vect, F, window)` — применяет к каждому окну **любую скалярную функцию**:

```csharp
Vector avg  = Functions.WindowFuncDouble(signal, v => v.Mean(),   21);
Vector std  = Functions.WindowFuncDouble(signal, v => Math.Sqrt(Statistic.CalcVariance(v)), 21);
Vector mx   = Functions.WindowFuncDouble(signal, v => v.Max(),    21);
Vector rms  = Functions.WindowFuncDouble(signal, Statistic.RMS,    21);
Vector q75  = Functions.WindowFuncDouble(signal, v => Quantile.FastQuantile(v, 0.75), 21);
```

Перегрузка с `stride` позволяет проходить окнами с шагом > 1 (прорежённая оценка).

## Свойства

- Чем шире $W$, тем сильнее сглаживание, но больше задержка и хуже отклик на скачки;
- Длина результата: $N - W + 1$ (смещается относительно входа на $W/2$);
- Эквивалент свёртки с прямоугольным ядром $\mathbf{1}_W / W$.

## Применения

1. Предварительная фильтрация шума перед дифференцированием;
2. Оценка локальной волатильности в финансах;
3. Детектирование изменений режима (change-point) по скользящей дисперсии;
4. Построение огибающей сигнала (скользящий модуль).

## Код

```csharp
using AI;

var rng = new Random(42);
var t     = Vector.Seq(0, 0.02, 10);
var clean = t.Transform(x => Math.Sin(2 * Math.PI * 0.4 * x));
var noisy = new Vector(t.Count);
for (int i = 0; i < t.Count; i++)
    noisy[i] = clean[i] + RandomEngine.NextGaussian(rng) * 0.2;

var smooth = Functions.WindowFuncDouble(noisy, v => v.Mean(), 21);
```
