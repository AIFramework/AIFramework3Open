# Нейронные сети — `AI.NeuralNetworks` / `AI.NeuralNetworks.Gpu`

Сборка **`AI.NeuralNetworks`** (`AI.NeuralNetworks.dll`, **.NET 9.0**) — полноценный тензорный движок **V2** с автоматическим дифференцированием, модульной системой слоёв и циклом обучения. Архитектура повторяет PyTorch: `Tensor` → `autograd` → `nn.Module` → `Optimizer` → `Trainer`. Сборка **`AI.NeuralNetworks.Gpu`** добавляет GPU-ускорение через ILGPU/CUDA без изменения пользовательского кода.

---

## Зависимости

| Проект | Зачем |
|--------|--------|
| **`AI.ML`** | Базовые ML-типы; транзитивно — `AI.ClassicMath` и `AI`. |
| **OpenBlasSharp** | CPU BLAS (SGEMM) для матричных умножений в `CpuBlas`. |
| **ILGPU 1.5.3** | *(только Gpu)* JIT-компиляция и запуск GPU-ядер. |
| **ILGPU.Algorithms** | *(только Gpu)* Встроенные GPU-алгоритмы (XMath и др.). |

---

## Ключевые области

| Область / пространство имён | Назначение |
|------------------------------|------------|
| `V2` (Tensor, Shape, DType, Device) | N-мерный тензор-view поверх `TensorStorage`; immutable форма и страйды; типы элементов (Float32/64, Float16, BFloat16, Int8–64, Bool); абстракция устройства (CPU / CUDA). |
| `V2.Storage` | `TensorStorage` (абстракция), `CpuStorage`, `StorageBackends` — реестр фабрик для plug-in устройств. |
| `V2.Autograd` | `Engine` — обратное распространение по tape; `Function` — узел графа; `TapeContext` — per-thread `AsyncLocal`-лента; `GradCheck` — числовая проверка градиентов; `ViewFunction` — grad через view-операции. |
| `V2.Nn` | `Module` (фрактальный дизайн с `RegisterParameter`/`RegisterModule`), `Linear`, `Conv1d`/`Conv2d`/`ConvTranspose2d`, `RNNCell`/`LSTMCell`/`GRUCell` + многослойные `RNN`/`LSTM`/`GRU`, `Embedding`, `ScaledDotProductAttention`, `MultiHeadAttention`, `TransformerEncoderLayer`/`TransformerEncoder`, `FeedForward`, `Sequential`, `BatchNorm`/`LayerNorm`/`GroupNorm`/`InstanceNorm`/`RMSNorm`, `Dropout`, `MaxPool2d`/`AvgPool2d`, `Activations` (ReLU, GELU, SiLU, Sigmoid, Tanh, …), `Parameter`, `Buffer`, `Init` (Kaiming, Xavier и др.). |
| `V2.Ops` | `TensorOps` (арифметика, MatMul, редукции), `Float32Ops`, `CpuBlas`, `Broadcasting`, `Softmax`, `IndexingOps`, `OpRegistry` — диспатч ядер по `(DeviceType, DType)`, `ElementwiseDispatch`, `IElementwiseOp`. |
| `V2.Losses` | `ClassificationLosses` (CrossEntropy, NLL, BCEWithLogits, KL), `RegressionLosses` (MSE, L1, SmoothL1/Huber), `EmbeddingLosses` (Triplet, CosineEmbedding, MarginRanking, HingeEmbedding), `Reduction`. |
| `V2.Optim` | `Optimizer` → `Adam`, `SGD`, прочие (`OtherOptimizers`); `LRScheduler`-ы (Step, Cosine и др.); `OptimHostMirror` — зеркалирование параметров для GPU. |
| `V2.Train` | `Trainer<TBatch>` — универсальный цикл с gradient accumulation, grad clipping, EMA, hooks; `GradUtils`. |
| `V2.Data` | `IDataset<T>`, `DataLoader<TItem, TBatch>` (многопоточный, через `Channel<T>`), `Sampler`, `Collate`. |

---

## V2 Tensor Engine

Центральный тип — `Tensor`: view поверх линейного `TensorStorage` с `Shape`/`Strides`/`Offset`. Reshape, transpose, slice **не копируют данные** — создают новый view. Если `RequiresGrad = true`, операции записываются в per-thread `TapeContext` (`AsyncLocal`), а `Backward()` запускает `Engine.Run` — топологическая сортировка и in-place аккумуляция градиентов.

`OpRegistry` связывает `(DeviceType, DType)` → реализацию: на CPU зарегистрированы `Float32Ops` + `CpuBlas` (OpenBLAS SGEMM). GPU-сборка добавляет свои kernel-ы при `GpuBackend.Initialize()` — после этого `TensorOps.Add`, `MatMul` и т.д. автоматически диспатчатся на GPU для тензоров на `Device.Cuda(0)`.

---

## GPU-ускорение — `AI.NeuralNetworks.Gpu`

| Компонент | Назначение |
|-----------|------------|
| `GpuBackend` | Точка входа: `Initialize(deviceIndex)` создаёт контекст, регистрирует `CudaStorage` и ядра в `OpRegistry`. Идемпотентен и потокобезопасен. |
| `GpuContext` | Владеет ILGPU `Context` + `CudaAccelerator` + `CuBlasHandle`. |
| `CudaStorage` | `TensorStorage` на GPU-памяти; `IHostCopyable` для `tensor.To(Device.Cuda(0))`. |
| `GpuMemoryPool` | Переиспользование `MemoryBuffer1D<byte>` по размеру — 2–10× ускорение при фиксированном batch size. |
| `V2Kernels` | ILGPU-ядра: поэлементные (neg, exp, relu, sigmoid, gelu, …), бинарные (add, sub, mul, div, pow) с broadcast, GEMM/BatchedGEMM, редукции, strided-copy, рекуррентные (LSTM/GRU forward). |
| `FusedKernels` | Объединённые ядра: `LinearGeluFwd`, `AddBiasReluFwd`, `AdamWStep` — один kernel вместо цепочки, экономия global memory traffic. |
| `GpuOps` | Реализации V2-операций; для matmul — cuBLAS SGEMM при наличии библиотеки, иначе ILGPU fallback. |
| `CuBlas/CuBlasHandle` | P/Invoke-обёртка; graceful fallback, если `cublas64_*.dll` не найдена. |
| `CudaGraphs/StepShapeMonitor` | Отслеживание стабильности формы шага обучения (заготовка под CUDA Graph capture). |

Перенос модели на GPU:

```csharp
GpuBackend.Initialize();
model.To(Device.Cuda(0));       // параметры и буферы → GPU
var x = Tensor.Randn(32, 784).To(Device.Cuda(0));
var y = model.Forward(x);       // forward на GPU
```

---

## ONNX-интеграция — `AI.NeuralNetworks.Onnx`

Сборка **`AI.NeuralNetworks.Onnx`** (зависит от **`AI.NeuralNetworks`**, **Google.Protobuf**, **Microsoft.ML.OnnxRuntime**) предоставляет `OnnxV2` — bridge между V2-Module и форматом ONNX:

- `SaveStateDict(model, path)` — сохранить веса в ONNX-файл (как инициализаторы).
- `LoadStateDict(model, path, strict)` — загрузить веса из ONNX по именам параметров.

Фокус — checkpoint interop с PyTorch / Hugging Face; полноценный экспорт графа операций — за пределами текущего scope.

---

## Роль в решении

- Демо-модуль **`NeuralNetworksModule`** (`Demo/WebUI/…/NeuralNetworks/`) показывает: MLP-классификатор (V2 Sequential + Adam + CrossEntropy), нейросетевую регрессию 1D/2D, GRU-прогноз временных рядов, автоэнкодер.
- `AI.NeuralNetworks` используется **`AI.NeuralNetworks.Gpu`** (GPU-бэкенд) и **`AI.NeuralNetworks.Onnx`** (экспорт/импорт весов). `InternalsVisibleTo` открывает внутренние типы для этих сборок и тестового проекта **`NNW.V2.Tests`**.
- Модульный дизайн: CPU-only проекты ссылаются только на `AI.NeuralNetworks`; GPU-проекты добавляют `AI.NeuralNetworks.Gpu` и вызывают `GpuBackend.Initialize()`.

---

## Сборка

```bash
dotnet build src/AI.NeuralNetworks/AI.NeuralNetworks.csproj -c Release
dotnet build src/AI.NeuralNetworks.Gpu/AI.NeuralNetworks.Gpu.csproj -c Release
dotnet build src/AI.NeuralNetworks.Onnx/AI.NeuralNetworks.Onnx.csproj -c Release
```
