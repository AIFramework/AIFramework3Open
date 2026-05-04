# Миграция со старого ядра (NNValue / NNW / NNWGraphCPU) на V2 (Tensor / Module / Trainer)

## Зачем

Новое ядро (`AI.ML.NeuralNetworks.V2`) построено по модели PyTorch:
- N-мерный тензор (`Tensor`) с произвольным dtype, broadcasting, view-операциями.
- Динамическая autograd-лента (`Function`/`TapeContext`/`Engine`).
- Фрактальная система модулей (`Module`/`Sequential`/`ModuleList`).
- 30+ слоёв, 12+ losses, 7 оптимизаторов и 11 LR-scheduler-ов.
- DataLoader + Trainer 2.0 с грэд-аккумуляцией, EMA, hook-ами.
- GPU-backend через `AI.NeuralNetworks.Gpu.V2.GpuBackend.Initialize()`.
- ONNX state-dict через `AI.NeuralNetworks.Onnx.V2.OnnxV2`.

Старое ядро остаётся работоспособным до полной миграции зависимых проектов,
но новые фичи добавляются только в V2.

## Соответствие API

| Старое | Новое |
|---|---|
| `NNValue` | `AI.ML.NeuralNetworks.V2.Tensor` |
| `NNW` | `AI.ML.NeuralNetworks.V2.Nn.Module` (наследник, Sequential для линейных стэков) |
| `NNWGraphCPU` / `INNWGraph` | автоматический tape + `Tensor.Backward()` |
| `Trainer` (legacy) | `AI.ML.NeuralNetworks.V2.Train.Trainer` |
| `IOptimizer`/`Adam`/`SGD` (legacy) | `AI.ML.NeuralNetworks.V2.Optim.Adam`/`SGD` |
| `ILoss`/`LossMSE`/`LossCrossEntropy` | `AI.ML.NeuralNetworks.V2.Losses.MSELoss`/`CrossEntropyLoss` |
| `Layers/LinearLayer`/`Conv1D`/`MaxPool1D`/... | `Nn.Linear`/`Nn.Conv2d`/`Nn.MaxPool2d`/... |
| `Activations/ReLU` etc | `Nn.ReLU`/`Nn.Sigmoid`/... (как Module) |
| GPU: `NNWGraphGPU` | `Tensor.To(Device.Cuda(0))` + `GpuBackend.Initialize()` |
| ONNX export через `OnnxExporter` | `OnnxV2.SaveStateDict(model, path)` |

## Шаблон миграции одного проекта

```csharp
// Было:
var net = new NNW();
net.AddNewLayer(new Shape3D(28*28), new LinearLayer(128));
net.AddNewLayer(new ReLU());
net.AddNewLayer(new LinearLayer(10));
var graph = new NNWGraphCPU(true);
var optim = new Adam(net.GetParameters(), 1e-3);

// Стало:
var net = new Sequential(
    new Linear(28*28, 128),
    new ReLU(),
    new Linear(128, 10));
var optim = new Adam(net.Parameters(), lr: 1e-3f);

// Forward / Backward / Step:
var y = net.Forward(x);
var loss = new CrossEntropyLoss().Forward(y, target);
loss.Backward();
optim.Step();
optim.ZeroGrad();
```

## Что **уже** удалено или будет удалено

После полной миграции зависимых проектов (см. чеклист в TODO):
- `AI.ML.NeuralNetworks.Core.NNW`
- `AI.ML.NeuralNetworks.Core.NNWGraphCPU` (+ partial файлы)
- `AI.ML.NeuralNetworks.Core.Activations.*` (старые)
- `AI.ML.NeuralNetworks.Core.Train.Optimizers.*` (старые)
- `AI.ML.NeuralNetworks.Core.Layers.*` (старые)
- `AI.ML.NeuralNetworks.Core.Loss.*` (старые)
- `AI.ML.NeuralNetworks.Gpu.NNWGraphGPU` (+ partial файлы)
- `AI.DataStructs.NNValue` остаётся как общий тензор (используется в некоторых
  сценариях AI.ML.* за пределами NN), но не должен использоваться в новой работе.

## Соответствие zero-overhead инвариантов
- View-операции (`Reshape/Transpose/Permute/Squeeze/Unsqueeze/Expand`) — zero-copy
  и autograd-aware (через `ViewFunction`).
- Broadcasting — реализован через stride=0 + `Broadcasting.ReduceForBroadcast` в backward.
- TapeContext — `AsyncLocal<>`-based, безопасное параллельное обучение разных моделей.
