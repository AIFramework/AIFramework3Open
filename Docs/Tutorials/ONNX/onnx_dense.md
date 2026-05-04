# Dense Layer (ONNX)

## Линейный слой (полносвязный)

Полносвязный слой выполняет аффинное преобразование входного вектора:

$$
y = f(Wx + b)
$$

где:
- $x \in \mathbb{R}^N$ — входной вектор,
- $W \in \mathbb{R}^{M \times N}$ — матрица весов,
- $b \in \mathbb{R}^M$ — вектор смещений,
- $f$ — функция активации.

## Функции активации

| Название | Формула | Диапазон |
|----------|---------|----------|
| ReLU | $\max(0, x)$ | $[0, +\infty)$ |
| Sigmoid | $\frac{1}{1+e^{-x}}$ | $(0, 1)$ |
| Tanh | $\frac{e^x - e^{-x}}{e^x + e^{-x}}$ | $(-1, 1)$ |
| Linear | $x$ | $(-\infty, +\infty)$ |

## Инициализация Xavier (Glorot)

Для сохранения дисперсии градиентов при инициализации:

$$
W_{ij} \sim \mathcal{U}\left(-\sqrt{\frac{6}{N+M}}, \sqrt{\frac{6}{N+M}}\right)
$$

## Класс `AI.ONNX.Base.LayersModel.Dense`

```csharp
using AI.ONNX.Base.LayersModel;
using AI.DataStructs.Algebraic;

// Загрузка ONNX-модели с одним полносвязным слоем
using var dense = new Dense("linear_model.onnx", DataType.Float32);

// Входной вектор — форма {1, N} (без батча)
var x = new Vector(new double[] { 1.0, -0.5, 2.3, /* ... */ });
Vector output = dense.ForwardNoBatch(x);

// output — результат прохода через ONNX-граф
```

## Создание совместимой ONNX-модели (PyTorch)

```python
import torch
import torch.onnx

model = torch.nn.Linear(16, 8)
dummy = torch.zeros(1, 16)
torch.onnx.export(model, dummy, "linear_model.onnx",
    input_names=["input"], output_names=["output"],
    opset_version=17)
```
