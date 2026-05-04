# Двумерное нормальное распределение

## Плотность

Для $\mathbf{x} = (x_1, x_2)^T$ с вектором средних $\boldsymbol{\mu}$ и ковариационной матрицей $\Sigma$:

$$
p(\mathbf{x}) = \frac{1}{2\pi\sqrt{|\Sigma|}} \exp\!\left(-\frac{1}{2}(\mathbf{x}-\boldsymbol{\mu})^T \Sigma^{-1}(\mathbf{x}-\boldsymbol{\mu})\right).
$$

При параметризации через СКО и корреляцию:

$$
\Sigma = \begin{pmatrix} \sigma_1^2 & \rho\sigma_1\sigma_2 \\ \rho\sigma_1\sigma_2 & \sigma_2^2 \end{pmatrix}, \qquad |\Sigma| = \sigma_1^2\sigma_2^2(1-\rho^2).
$$

## Квадратичная форма

$$
Q = \frac{1}{1-\rho^2}\left[\frac{(x_1-\mu_1)^2}{\sigma_1^2} - \frac{2\rho(x_1-\mu_1)(x_2-\mu_2)}{\sigma_1\sigma_2} + \frac{(x_2-\mu_2)^2}{\sigma_2^2}\right].
$$

Линии уровня $Q = \mathrm{const}$ — эллипсы, ориентация которых определяется $\rho$.

## Влияние корреляции

| $\rho$ | Форма |
|---|---|
| $0$ | Оси эллипса параллельны координатным |
| $> 0$ | Эллипс наклонён к диагонали (x₁ растёт → x₂ растёт) |
| $< 0$ | Эллипс наклонён к антидиагонали |
| $\to \pm 1$ | Эллипс вырождается в отрезок |

## Маргинальные и условные

- Маргинальные: $X_1 \sim N(\mu_1, \sigma_1^2)$, $X_2 \sim N(\mu_2, \sigma_2^2)$;
- Условное: $X_2 | X_1 = x_1 \sim N\!\left(\mu_2 + \rho\frac{\sigma_2}{\sigma_1}(x_1 - \mu_1),\; \sigma_2^2(1-\rho^2)\right)$.

## Код

```csharp
// Плотность в точке (x1, x2) вычисляется через NonCorrelatedGaussian
// для диагонального случая, или напрямую через квадратичную форму.
var gauss = new NonCorrelatedGaussian();
```
