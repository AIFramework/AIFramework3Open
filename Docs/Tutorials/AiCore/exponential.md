# Экспоненциальное распределение

$X \sim \mathrm{Exp}(\lambda)$ — непрерывная случайная величина с плотностью

$$
p(x) \;=\; \lambda e^{-\lambda x}, \qquad x \ge 0,
$$

и кумулятивной функцией $F(x) = 1 - e^{-\lambda x}$.

## Характеристики

$$
\mathbb{E}[X] = \frac{1}{\lambda}, \qquad \mathrm{Var}(X) = \frac{1}{\lambda^2}, \qquad \sigma = \frac{1}{\lambda}.
$$

Уникальное свойство — **отсутствие памяти**:

$$
P(X > s + t \mid X > s) \;=\; P(X > t).
$$

## Применения

- Время ожидания события в пуассоновском процессе;
- Время между отказами радиоэлектронной аппаратуры при постоянной интенсивности отказов;
- Модель межкадрового интервала в сети с равномерной нагрузкой.

## Генерация методом обратной функции

Если $U \sim U(0, 1)$, то

$$
X \;=\; -\frac{1}{\lambda}\ln(1 - U) \;\sim\; \mathrm{Exp}(\lambda).
$$

Именно этот алгоритм реализован в `RandomEngine.NextExponential`.

## Код

```csharp
var rng = new Random(42);
double rate = 1.5;
var sample = new Vector(n);
for (int i = 0; i < n; i++)
    sample[i] = RandomEngine.NextExponential(rng, rate);

// Эмпирическое среднее ≈ 1/rate
// Эмпирическое СКО    ≈ 1/rate
```

Асимметрия $\mathrm{Exp}$: $\gamma_1 = 2$ (сильно скошенное вправо).
