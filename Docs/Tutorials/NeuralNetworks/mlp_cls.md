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

## Примеры

Сеть собирается из `Sequential`, обучается вручную по мини-батчам: готового
класса-обёртки вроде `NeuralClassifier` в V2 нет — цикл обучения открыт.

```csharp
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;

int n = 160, hidden = 16, epochs = 80;
float lr = 0.01f;
var rng = new Random(42);

// Два входных признака, два класса на выходе
var net = new Sequential(
    new Linear(2, hidden, true, rng),
    new ReLU(),
    new Linear(hidden, hidden, true, rng),
    new ReLU(),
    new Linear(hidden, 2, true, rng));

// Данные: плоские массивы float, форма задаётся Shape
var xArr = new float[n * 2];
var yArr = new int[n];
for (int i = 0; i < n; i++)
{
    xArr[i * 2]     = (float)rng.NextDouble() * 2 - 1;
    xArr[i * 2 + 1] = (float)rng.NextDouble() * 2 - 1;
    yArr[i]         = xArr[i * 2] * xArr[i * 2 + 1] > 0 ? 1 : 0;   // «шахматка»
}

var optim = new Adam(net.Parameters(), lr: lr);

for (int epoch = 0; epoch < epochs; epoch++)
{
    optim.ZeroGrad();

    var logits = net.Forward(Tensor.From(xArr, new Shape(n, 2)));
    var loss   = ClassificationLosses.CrossEntropy(logits, Tensor.From(yArr, new Shape(n)));

    loss.Backward();
    optim.Step();
}

// Предсказание: под NoGrad, чтобы не строить граф вычислений
using (TapeContext.NoGrad())
{
    var probe = Tensor.From(new[] { 0.3f, -0.7f }, new Shape(1, 2));
    var ls = net.Forward(probe).AsReadOnlySpan<float>();
    int predicted = ls[0] >= ls[1] ? 0 : 1;
    Console.WriteLine($"Класс: {predicted}");
}
```

## Визуализация границы решений

На графике цветом фона показана **граница принятия решений** — каждый пиксель раскрашен в цвет того класса, который сеть присвоит точке с данными координатами. Благодаря нелинейности ReLU MLP может образовывать сложные кривые границы: например, на датасете «луны» или «шахматка» линейный классификатор не справится, а MLP с 1–2 скрытыми слоями успешно разделяет классы.

