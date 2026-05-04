# Описательная статистика

**Описательная статистика** — сводка свойств выборки: её *центра*, *разброса* и *формы*. Это первый шаг любого статистического анализа.

## Меры центра

| Оценка | Формула | Смысл |
|---|---|---|
| Среднее $\hat\mu$ | $\dfrac{1}{n}\sum_{i=1}^{n} x_i$ | Центр тяжести выборки |
| Медиана $Q_2$ | средний элемент отсортированной выборки | Центр по частоте |
| Геометрическое | $\left(\prod x_i\right)^{1/n}$ | Для мультипликативных величин |
| RMS | $\sqrt{\tfrac{1}{n}\sum x_i^2}$ | Энергетическое среднее |

## Меры разброса

Несмещённая дисперсия и СКО:

$$
\hat\sigma^2 \;=\; \frac{1}{n-1}\sum_{i=1}^{n} (x_i - \hat\mu)^2, \qquad \hat\sigma = \sqrt{\hat\sigma^2}
$$

Также: размах $R = \max - \min$ и межквартильный размах $\mathrm{IQR} = Q_3 - Q_1$.

## Меры формы

Асимметрия (skewness) и эксцесс (excess kurtosis):

$$
\gamma_1 \;=\; \frac{m_3}{\hat\sigma^3}, \qquad \gamma_2 \;=\; \frac{m_4}{\hat\sigma^4} - 3,
$$

где $m_k = \tfrac{1}{n}\sum (x_i - \hat\mu)^k$ — центральные моменты.

- $\gamma_1 = 0$ — симметрично; $\gamma_1 > 0$ — «длинный правый хвост»;
- $\gamma_2 = 0$ — нормальное; $\gamma_2 > 0$ — более острое и тяжёлохвостое.

## Код

```csharp
var stat = new Statistic(sample);
double mu   = stat.Expected;
double sig  = stat.STD;
double skew = stat.Asymmetry();
double kurt = stat.Excess();

var q  = new Quantile(sample);
double med = q.GetQuantile(0.5);

var hist = stat.Histogramm(40); // нормированная: ∫p(x)dx = 1
```

Все моменты в `AI.Statistics.Statistic` считаются одним проходом по алгоритму Уэлфорда — численно устойчиво даже для больших выборок.
