# Распределения Вейбулла и Пуассона

## Распределение Вейбулла

$X \sim \mathrm{Weibull}(k, \lambda)$ — обобщение экспоненциального:

$$
p(x) = \frac{k}{\lambda}\left(\frac{x}{\lambda}\right)^{k-1} \exp\left(-\left(\frac{x}{\lambda}\right)^k\right), \qquad x > 0.
$$

### Характеристики

$$
\mathbb{E}[X] = \lambda\,\Gamma\!\left(1 + \frac{1}{k}\right), \qquad \mathrm{Var}(X) = \lambda^2\left[\Gamma\!\left(1+\frac{2}{k}\right) - \Gamma^2\!\left(1+\frac{1}{k}\right)\right].
$$

### Частные случаи

- $k = 1$: экспоненциальное $\mathrm{Exp}(1/\lambda)$;
- $k = 2$: распределение Рэлея;
- $k \to \infty$: вырождается в дельту при $x = \lambda$.

### Генерация

Через инверсию CDF: $X = \lambda\,(-\ln U)^{1/k}$, $U \sim U(0,1)$.

### Применения

Анализ надёжности (время до отказа), моделирование ветровых нагрузок, анализ выживаемости.

## Распределение Пуассона

$N \sim \mathrm{Poisson}(\lambda)$ — дискретное распределение числа событий:

$$
P(N = k) = \frac{\lambda^k e^{-\lambda}}{k!}, \qquad k = 0, 1, 2, \ldots
$$

### Характеристики

$$
\mathbb{E}[N] = \lambda, \qquad \mathrm{Var}(N) = \lambda, \qquad \gamma_1 = 1/\sqrt{\lambda}.
$$

### Генерация

- При $\lambda < 30$: алгоритм Кнута — последовательное умножение $U(0,1)$ до порога $e^{-\lambda}$;
- При $\lambda \ge 30$: нормальное приближение $N \approx \mathrm{round}(\lambda + \sqrt{\lambda}\cdot Z)$.

### Применения

Подсчёт событий в единицу времени (звонки, фотоны, ошибки), моделирование очередей, задачи страхования.

## Код

```csharp
var rng = new Random(42);
double w = RandomEngine.NextWeibull(rng, shape: 1.5, scale: 2.0);
int p = RandomEngine.NextPoisson(rng, lambda: 5.0);
```
