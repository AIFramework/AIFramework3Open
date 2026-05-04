# MLP-классификатор

**Многослойный перцептрон** (Multi-Layer Perceptron) — это полносвязная нейронная сеть, состоящая из последовательных слоёв ReLU-нейронов и выходного softmax-слоя. В отличие от линейных моделей, MLP способен аппроксимировать произвольные (в том числе нелинейные) разделяющие поверхности.

## Математическая модель

Для сети с $L$ скрытыми слоями выход рассчитывается рекурсивно:

$$
h^{(0)} = x, \qquad
h^{(\ell)} = \sigma\!\big(W^{(\ell)} h^{(\ell-1)} + b^{(\ell)}\big), \quad \ell = 1, \ldots, L
$$

$$
p = \operatorname{softmax}\!\big(W^{(L+1)} h^{(L)} + b^{(L+1)}\big)
$$

где $\sigma(z) = \max(0, z) + 0.1 \cdot \min(0, z)$ — Leaky ReLU, а выход $p \in \mathbb{R}^C$ — вероятности принадлежности к $C$ классам.

## Функция потерь

Используется **кросс-энтропия** с softmax:

$$
\mathcal{L}(\theta) = -\sum_{i=1}^{N} \sum_{c=1}^{C} y_{ic}\log p_{ic}(\theta)
$$

где $y_{ic} = 1$, если объект $i$ принадлежит классу $c$, и $0$ иначе (one-hot кодирование).

## Алгоритм обучения

Веса $\theta = \{W^{(\ell)}, b^{(\ell)}\}$ обновляются стохастическим градиентным спуском с оптимизатором **Adam**:

$$
\theta_{t+1} = \theta_t - \eta \cdot \frac{\hat m_t}{\sqrt{\hat v_t} + \varepsilon}
$$

где $\hat m_t$ и $\hat v_t$ — экспоненциально сглаженные оценки первого и второго моментов градиента.

## Параметры демо

| Параметр | Смысл | Типичные значения |
|----------|-------|-------------------|
| Hidden  | нейронов в скрытом слое | 8 – 32 |
| Layers  | число скрытых слоёв     | 1 – 3 |
| Epochs  | проходов по выборке     | 50 – 200 |
| LR      | learning rate для Adam  | 0.005 – 0.02 |

## Пример использования

```csharp
using AI.ML.NeuralNetworks.Core;
using AI.ML.NeuralNetworks.Core.Layers;
using AI.ML.NeuralNetworks.Core.Activations;
using AI.ML.Classification;

var net = new NNW(seed: 42);
net.AddNewLayer(new Shape3D(2), new FeedForwardLayer(16, new ReLU(0.1)));
net.AddNewLayer(new FeedForwardLayer(16, new ReLU(0.1)));
net.AddNewLayer(new FeedForwardLayer(2,  new LinearUnit()));

var cls = new NeuralClassifier(net)
{
    EpochesToPass = 100,
    LearningRate  = 0.01f
};
cls.Train(features, labels);
int predicted = cls.Classify(new Vector(new[] { 0.3, -0.7 }));
```

## Визуализация границы решений

На графике цветом фона показана **граница принятия решений** — каждый пиксель раскрашен в цвет того класса, который сеть присвоит точке с данными координатами. Благодаря нелинейности ReLU MLP может образовывать сложные кривые границы: например, на датасете «луны» или «шахматка» линейный классификатор не справится, а MLP с 1–2 скрытыми слоями успешно разделяет классы.
