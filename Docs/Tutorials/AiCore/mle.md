# Метод максимального правдоподобия

**Maximum Likelihood Estimation (MLE)** — базовый метод оценки параметров распределения. Принцип: выбирать параметры $\theta$, при которых наблюдаемая выборка $x_1, \dots, x_n$ наиболее вероятна.

## Функция правдоподобия

$$
L(\theta) \;=\; \prod_{i=1}^{n} p(x_i \mid \theta)
$$

Обычно максимизируют **логарифм**, чтобы избежать численного нуля:

$$
\ell(\theta) \;=\; \log L(\theta) \;=\; \sum_{i=1}^{n} \log p(x_i \mid \theta).
$$

## Оценки для $\mathcal{N}(\mu, \sigma^2)$

Из условий $\partial\ell/\partial\mu = 0$, $\partial\ell/\partial\sigma = 0$:

$$
\hat\mu_{\mathrm{MLE}} = \frac{1}{n}\sum_{i=1}^{n} x_i, \qquad
\hat\sigma^2_{\mathrm{MLE}} = \frac{1}{n}\sum_{i=1}^{n} (x_i - \hat\mu)^2.
$$

Оценка $\hat\sigma^2_{\mathrm{MLE}}$ **смещена**: корректировка на $n/(n-1)$ даёт несмещённую. В `AI.Statistics` через `Welford(span, unbiased: false)` используется именно MLE-вариант.

## Свойства MLE

- **Состоятельность**: $\hat\theta_n \to \theta$ по вероятности;
- **Асимптотическая нормальность**: $\sqrt n (\hat\theta - \theta) \to \mathcal{N}(0, I^{-1})$, где $I$ — информация Фишера;
- **Эффективность**: достигает нижней границы Крамера–Рао.

## Код

```csharp
using AI.Statistics.Distributions;

double[] data = /* ваша выборка */;
var fit = NonCorrelatedGaussian.FitMaximumLikelihood(data);
double mu  = fit[NonCorrelatedGaussian.KeyMean];
double sig = fit[NonCorrelatedGaussian.KeyStd];

// Для ND-выборки векторов:
Dictionary<string, Vector> fitND = NonCorrelatedGaussian.FitMaximumLikelihood(samples);
```

Стандартные ошибки оценок: $\mathrm{SE}(\hat\mu) \approx \sigma/\sqrt n$, $\mathrm{SE}(\hat\sigma) \approx \sigma/\sqrt{2n}$.
