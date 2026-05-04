# Равномерное и нормальное распределения

## Равномерное $U(0, 1)$

Плотность и характеристики:

$$
p(x) = \begin{cases} 1, & x \in [0, 1] \\ 0, & \text{иначе} \end{cases}, \quad \mathbb{E}[X] = \tfrac{1}{2},\ \mathrm{Var}(X) = \tfrac{1}{12},\ \sigma = \tfrac{1}{\sqrt{12}} \approx 0.289.
$$

Асимметрия $= 0$, эксцесс $= -1.2$ (плоское распределение, короткие хвосты).

## Стандартное нормальное $\mathcal{N}(0, 1)$

$$
p(x) = \frac{1}{\sqrt{2\pi}} e^{-x^2/2}, \quad \mathbb{E}[X] = 0,\ \mathrm{Var}(X) = 1.
$$

Асимметрия $= 0$, эксцесс $= 0$ (эталонное распределение для оценки эксцесса).

## Генераторы в AI.Statistics

| Метод | Что делает |
|---|---|
| `Statistic.UniformDistribution(n)` | Вектор $n$ независимых $U(0,1)$ |
| `Statistic.RandNorm(n)` | Вектор из $\mathcal{N}(0, 1)$ через полярный Box–Muller |
| `Statistic.RandNormP(n, k)` | $\mathcal{N}(0, 1)$ через ЦПТ из $k$ равномерных — медленнее, но иллюстративно |
| `RandomEngine.NextGaussian(rng)` | Одна скалярная $\mathcal{N}(0, 1)$ |

Все генераторы потокобезопасны: используют `ThreadLocal<Random>` в `RandomEngine`.

## Код

```csharp
var rng = new Random(42);
Vector uniform = Statistic.UniformDistribution(n, rng);
Vector normal  = Statistic.RandNorm(n, rng);

var statU = new Statistic(uniform);   // statU.Excess() ≈ -1.2
var statN = new Statistic(normal);    // statN.Excess() ≈  0.0
```
