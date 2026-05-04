# Гамма и Бета распределения

## Гамма-распределение

$X \sim \mathrm{Gamma}(\alpha, \theta)$ с параметрами формы $\alpha > 0$ и масштаба $\theta > 0$:

$$
p(x) = \frac{x^{\alpha-1} e^{-x/\theta}}{\theta^\alpha \,\Gamma(\alpha)}, \qquad x > 0.
$$

### Характеристики

$$
\mathbb{E}[X] = \alpha\theta, \qquad \mathrm{Var}(X) = \alpha\theta^2, \qquad \gamma_1 = \frac{2}{\sqrt{\alpha}}.
$$

### Частные случаи

- $\mathrm{Gamma}(1, 1/\lambda) = \mathrm{Exp}(\lambda)$;
- $\mathrm{Gamma}(n/2,\, 2) = \chi^2(n)$.

### Генерация

Алгоритм Marsaglia–Tsang (2000): для $\alpha \ge 1$ генерируется $v = (1 + c \cdot z)^3$, где $z \sim N(0,1)$, и принимается/отклоняется по быстрому squeeze-тесту. Для $\alpha < 1$ применяется приведение Алдера: $X = X_{\alpha+1} \cdot U^{1/\alpha}$.

## Бета-распределение

$Y \sim \mathrm{Beta}(\alpha, \beta)$ на отрезке $(0, 1)$:

$$
p(y) = \frac{y^{\alpha-1}(1-y)^{\beta-1}}{B(\alpha, \beta)}, \qquad B(\alpha, \beta) = \frac{\Gamma(\alpha)\Gamma(\beta)}{\Gamma(\alpha+\beta)}.
$$

### Характеристики

$$
\mathbb{E}[Y] = \frac{\alpha}{\alpha+\beta}, \qquad \mathrm{Var}(Y) = \frac{\alpha\beta}{(\alpha+\beta)^2(\alpha+\beta+1)}.
$$

### Генерация

Через два независимых гамма-сэмпла: если $G_1 \sim \mathrm{Gamma}(\alpha, 1)$ и $G_2 \sim \mathrm{Gamma}(\beta, 1)$, то $Y = G_1 / (G_1 + G_2) \sim \mathrm{Beta}(\alpha, \beta)$.

## Код

```csharp
var rng = new Random(42);
double x = RandomEngine.NextGamma(rng, shape: 2.0, scale: 1.0);
double y = RandomEngine.NextBeta(rng, alpha: 2.0, beta: 5.0);
```
