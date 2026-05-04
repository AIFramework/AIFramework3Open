# Многомерные гетерогенные смеси и Classification EM (ND)

## Обзор

Многомерные гетерогенные смеси — обобщение классических GMM: каждая компонента может иметь свой тип (диагональная ковариация, полная ковариация, различные семейства распределений). Classification EM подгоняет параметры всех компонент одновременно.

## Математическая модель

Плотность смеси из K компонент в пространстве ℝᵈ:

$$p(\mathbf{x}) = \sum_{k=1}^{K} w_k \cdot f_k(\mathbf{x} \mid \theta_k)$$

где каждая $f_k$ может быть:
- `GaussianDistND` — $\mathcal{N}(\boldsymbol{\mu}, \operatorname{diag}(\boldsymbol{\sigma}^2))$
- `GaussianDistFullCov` — $\mathcal{N}(\boldsymbol{\mu}, \Sigma)$ с полной ковариационной матрицей

## Classification EM (Hard-EM) для ND

### E-шаг (параллельный)
$$z_i = \arg\max_k \left[ \ln w_k + \ln f_k(\mathbf{x}_i) \right]$$

### M-шаг (параллельный по компонентам)
$$w_k = \frac{|\{i : z_i = k\}|}{n}$$
$$\hat{\theta}_k = \operatorname{MLE}(\{x_i : z_i = k\})$$

Для полной ковариации:
$$\hat{\Sigma}_k = \frac{1}{n_k - 1}\sum_{i: z_i = k}(\mathbf{x}_i - \hat{\mu}_k)(\mathbf{x}_i - \hat{\mu}_k)^\top$$

## Потокобезопасность

- E-шаг: `Parallel.For` по наблюдениям (lock-free, кроме аккумулятора log-likelihood)
- M-шаг: `Parallel.For` по компонентам (каждая работает с независимым подмножеством)
- Все компоненты иммутабельны — заменяются атомарно

## Пример кода

```csharp
using AI.Statistics.Distributions;
using AI.Statistics.MixtureModeling;
using AI.DataStructs.Algebraic;

// Создание ND-компонент
var comp1 = new GaussianDistND(
    new Vector(new[] { -2.0, 1.0 }),
    new Vector(new[] { 0.8, 0.5 }));

var cov = new double[,] { { 1.0, 0.6 }, { 0.6, 0.8 } };
var comp2 = new GaussianDistFullCov(
    new Vector(new[] { 2.0, -1.0 }), cov);

// Генерация данных
var mixture = new MixtureModel(
    new IDistributionWithoutParams[] { comp1, comp2 },
    new Vector(new[] { 0.6, 0.4 }));

var rng = new Random(42);
var data = new Vector[2000];
for (int i = 0; i < data.Length; i++)
    data[i] = mixture.SampleND(rng);

// Classification EM
var result = ClassificationEM.FitND(data,
    new IDistributionWithoutParams[] { comp1, comp2 });

Console.WriteLine($"Iterations: {result.Iterations}");
Console.WriteLine($"LogL: {result.LogLikelihood:F2}");
```

## Интерфейс IRefittable

Единый контракт для пере-оценки параметров по данным:

```csharp
public interface IRefittable
{
    IDistributionWithoutParams Refit1D(double[] data, int count);
    IDistributionWithoutParams RefitND(Vector[] data, int count);
}
```

Компоненты реализуют поддерживаемый метод, неподдерживаемый бросает `NotSupportedException`.

## Сложность

| Операция | Сложность |
|----------|-----------|
| E-шаг (одна итерация) | O(n·K·d) |
| M-шаг (одна итерация, диаг.) | O(n·d) |
| M-шаг (одна итерация, полная) | O(n·d²) |
| Cholesky | O(d³) |
| Общая (T итераций) | O(T·n·K·d + T·K·d²) |
