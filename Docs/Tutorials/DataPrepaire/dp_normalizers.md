# Нормализация данных (AI.DataPrepaire)

## Зачем нормализовать данные?

Большинство алгоритмов машинного обучения чувствительны к масштабу признаков. Если один признак — рост (160–190 см), а другой — зарплата (50 000–200 000 ₽), то без нормализации второй признак будет доминировать в функции потерь.

## Z-нормализация (стандартизация)

Приводит данные к нулевому среднему и единичной дисперсии:

$$
\hat{x}_j = \frac{x_j - \mu_j}{\sigma_j}
$$

где $\mu_j = \frac{1}{n}\sum_{i=1}^n x_{ij}$, $\sigma_j = \sqrt{\frac{1}{n}\sum_{i=1}^n (x_{ij}-\mu_j)^2}$

**Свойства:**
- После нормализации: $\mathbb{E}[\hat{x}] = 0$, $\mathrm{Var}[\hat{x}] = 1$
- Не ограничивает диапазон (выбросы сохраняются)
- Оптимально для SVM, логрегрессии, нейросетей

## Min-Max нормализация

Масштабирует данные в диапазон $[0, 1]$:

$$
\hat{x}_j = \frac{x_j - \min_j}{\max_j - \min_j}
$$

**Свойства:**
- Гарантированный диапазон $[0,1]$
- Чувствительна к выбросам
- Оптимально для KNN, нейросетей с сигмоидной активацией

## Денормализация

$$
x_j = \hat{x}_j \cdot \sigma_j + \mu_j \quad \text{(Z-норм.)}
$$
$$
x_j = \hat{x}_j \cdot (\max_j - \min_j) + \min_j \quad \text{(Min-Max)}
$$

## API

```csharp
using AI.DataPrepaire.DataNormalizers;
using AI.DataStructs.Algebraic;

// Создание нормализаторов
var zn = new ZNormalizer();
var mm = new MinimaxNomalizer();

// Обучение на тренировочных данных
Vector[] trainData = /* N векторов */;
zn.Train(trainData);
mm.Train(trainData);

// Трансформация
var normalized = zn.Transform(testData).Cast<Vector>().ToArray();

// Денормализация
var restored = zn.Denormalize(normalized).Cast<Vector>().ToArray();

// Свойства
double[] mean = zn.Mean;
double[] std  = zn.Std;
double[] min  = mm.Min;
double[] max  = mm.Max;
```
