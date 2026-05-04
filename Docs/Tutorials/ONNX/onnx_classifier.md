# Softmax-классификатор (ONNX)

## Многоклассовая классификация

Классификатор на выходе применяет функцию **Softmax**, преобразующую произвольные логиты в вектор вероятностей:

$$
P(\text{класс} = k \mid x) = \frac{e^{z_k}}{\sum_{j=1}^{K} e^{z_j}}, \qquad z = Wx + b
$$

Свойства вектора вероятностей:
- $\sum_{k=1}^{K} P_k = 1$
- $P_k \in (0, 1)$

## Предсказание класса

$$
\hat{y} = \arg\max_{k} P_k = \arg\max_k z_k
$$

Логит с максимальным значением = предсказанный класс (softmax монотонна).

## Энтропия предсказания

**Неопределённость** классификатора измеряется энтропией:

$$
H = -\sum_{k=1}^{K} P_k \log_2 P_k
$$

- $H = 0$ — абсолютная уверенность (один класс с вероятностью 1)
- $H = \log_2 K$ — равномерное распределение (максимальная неопределённость)

## Класс `GrayScaleClassifier`

```csharp
using AI.ONNX.Classifiers;
using AI.DataStructs.Algebraic;

// Загрузка классификатора изображений в градациях серого
using var cls = new GrayScaleClassifier("mnist_model.onnx", LibType.PyTorch);

// Входное изображение — Matrix [H × W]
var img = new Matrix(28, 28);
// ... заполнить пикселями 0..1 ...

// Классификация — возвращает вектор вероятностей
Vector probs = cls.Classify(img);
int predictedClass = probs.MaxIdx();
```

## Пример совместимой ONNX-модели (MNIST)

```python
# PyTorch → ONNX
import torch, torch.onnx

model = torchvision.models.AlexNet(num_classes=10)
dummy = torch.zeros(1, 1, 28, 28)  # grayscale
torch.onnx.export(model, dummy, "mnist.onnx",
    input_names=["input"], output_names=["output"])
```
