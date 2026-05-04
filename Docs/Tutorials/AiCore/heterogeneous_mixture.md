# Смеси разных распределений (гетерогенные)

## Идея

Классическая Gaussian Mixture Model (GMM) — частный случай, где
все компоненты нормальные. В общем случае компоненты могут быть
**произвольными** распределениями:

$$
p(x) = \sum_{k=1}^{K} w_k \cdot p_k(x), \qquad \sum w_k = 1
$$

где каждая p_k может быть Гауссом, Экспонентой, Лапласом, Релеем и т.д.

## Зачем?

- Моделирование мультимодальных данных с различной формой мод
- Учёт асимметрии (Exp, Rayleigh) и тяжёлых хвостов (Laplace)
- Гибкость: каждая компонента описывает свой физический процесс

## Реализация в AIFramework

Класс `MixtureModel` принимает массив `IDistributionWithoutParams[]` — 
подходит любой объект с методом `CulcProb(x)`. Если компонента реализует
`ISamplableDistribution`, смесь может сэмплировать через rejection-free
метод (выбор компоненты по весу + sample из неё).

### Готовые 1D-обёртки

| Класс | Параметры |
|-------|-----------|
| `GaussianDist1D` | μ, σ |
| `ExponentialDist1D` | rate, shift |
| `LaplaceDist1D` | μ, b |
| `RayleighDist1D` | σ |
| `UniformDist1D` | a, b |

## Пример кода

```csharp
using AI.Statistics.Distributions;
using AI.Statistics.MixtureModeling;
using AI.DataStructs.Algebraic;

var components = new IDistributionWithoutParams[]
{
    new GaussianDist1D(-1, 0.5),
    new ExponentialDist1D(2.0, shift: 1),
    new LaplaceDist1D(3, 0.3)
};
var weights = new Vector(new double[] { 0.4, 0.35, 0.25 });

var mixture = new MixtureModel(components, weights);

// PDF в точке
double pdf = mixture.CulcProb(0.5);

// Генерация выборки
var rng = new Random(42);
double sample = mixture.Sample1D(rng);
```

## Визуализация

Демо показывает:
1. Гистограмму сгенерированной выборки
2. Суммарную PDF смеси
3. Взвешенные PDF каждой компоненты отдельно
