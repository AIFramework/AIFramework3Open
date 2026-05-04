# Смесь гауссиан и EM-алгоритм

## Модель смеси

Смесь из $K$ гауссовых компонент:

$$
p(x) = \sum_{k=1}^{K} w_k \,\mathcal{N}(x \mid \mu_k, \sigma_k^2), \qquad \sum_k w_k = 1.
$$

Это универсальный аппроксиматор плотности: при достаточном $K$ любая гладкая плотность приближается со сколь угодно малой ошибкой.

## EM-алгоритм (Expectation–Maximization)

Итеративный метод подгонки параметров $\{w_k, \mu_k, \sigma_k\}$ по методу максимального правдоподобия.

### E-шаг

Вычисление ответственностей (апостериорных вероятностей компонент):

$$
\gamma_{ik} = \frac{w_k \,\mathcal{N}(x_i \mid \mu_k, \sigma_k^2)}{\sum_{j=1}^K w_j \,\mathcal{N}(x_i \mid \mu_j, \sigma_j^2)}.
$$

### M-шаг

Обновление параметров:

$$
N_k = \sum_i \gamma_{ik}, \qquad w_k = \frac{N_k}{N}, \qquad \mu_k = \frac{1}{N_k}\sum_i \gamma_{ik} x_i,
$$

$$
\sigma_k^2 = \frac{1}{N_k}\sum_i \gamma_{ik}(x_i - \mu_k)^2.
$$

### Сходимость

Каждая итерация гарантированно не уменьшает log-likelihood. Критерий останова — относительное изменение $\Delta \log L / |\log L| < \varepsilon$.

## Инициализация

Средние инициализируются через **k-means++** (устойчиво к локальным минимумам), начальные СКО — глобальная дисперсия данных.

## Выбор числа компонент

- **BIC** (Bayesian Information Criterion): $\mathrm{BIC} = -2\log L + p \ln N$;
- **AIC** (Akaike): $\mathrm{AIC} = -2\log L + 2p$;
- Меньше — лучше. $p = (K-1) + 2K$ для 1D-случая.

## Код

```csharp
var data = new double[] { /* выборка */ };
var gmm = EM.Fit(data, numComponents: 3, seed: 42);

// gmm.Weights, gmm.Means, gmm.Stds — восстановленные параметры
// gmm.CulcProb(x) — плотность смеси в точке x
// gmm.Bic(data.Length) — информационный критерий
```
