# Сходимость выборочных моментов

Выборочное среднее и выборочное СКО — **состоятельные** оценки своих теоретических аналогов:

$$
\hat\mu_n \xrightarrow{\text{п.н.}} \mu, \qquad \hat\sigma_n \xrightarrow{\text{п.н.}} \sigma \qquad (n \to \infty).
$$

## Скорость сходимости

По центральной предельной теореме:

$$
\sqrt{n}\,(\hat\mu_n - \mu) \;\xrightarrow{d}\; \mathcal{N}(0, \sigma^2),
$$

то есть стандартная ошибка среднего убывает как $\sigma / \sqrt n$. Для оценки $\hat\sigma$ скорость примерно $\sigma / \sqrt{2n}$.

## Что показывает демо

Строятся зависимости $\hat\mu(n)$ и $\hat\sigma(n)$ при нарастающем объёме выборки $n$ из $\mathcal{N}(\mu, \sigma^2)$. Оценки колеблются вокруг истинных значений всё с меньшей амплитудой — это визуализация закона больших чисел.

## Код

```csharp
var big = Statistic.RandNorm(nMax, rng) * sigma + mu;
for (int k = 10; k <= nMax; k += step)
{
    var sub = new Vector(k);
    for (int j = 0; j < k; j++) sub[j] = big[j];
    var st = new Statistic(sub);
    // st.Expected → μ,  st.STD → σ
}
```

Чтобы уменьшить ошибку оценки среднего в 2 раза, нужно **в 4 раза** больше данных — таков ценник нормализующих коэффициентов $1/\sqrt n$.
