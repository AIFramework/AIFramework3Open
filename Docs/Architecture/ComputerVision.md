# Компьютерное зрение — `AI.ComputerVision`

Сборка **`AI.ComputerVision`** (`AI.ComputerVision.dll`, **.NET 9.0**) предоставляет инструменты обработки изображений: пространственные и частотные фильтры, градиентный анализ (Sobel, HOG), эквализацию гистограмм, работу с цветными и бинарными изображениями. Графический бэкенд — **SkiaSharp**, матричные типы — из ядра **`AI`**.

---

## Зависимости

| Проект | Зачем |
|--------|--------|
| **`AI`** | `Matrix`, `Vector`, `Tensor`, `ComplexVector`, активационные функции, статистика. |
| **`AI.DSP`** | CPU-ядро FFT (`Fft64`), кепстральный анализ (`Cepstrum`). |
| **`AI.DataPrepaire`** | Пайплайн подготовки данных (пре-/постпроцессинг изображений). |
| **`AI.ONNX`** | Извлечение признаков через ONNX-модели (`ImgOnnxExtractor`). |
| **SkiaSharp 3.x** | Декодирование/кодирование изображений, `SKBitmap` <-> `Matrix`/`Tensor`. |

---

## Ключевые области

| Область / пространство имён | Назначение |
|------------------------------|------------|
| `AI.ComputerVision` | `ImageMatrixConverter` (SKBitmap <-> Matrix/Tensor/RGB-каналы), `BinaryImg`, `CompImg` (сравнение изображений через FFT-дескриптор), `FeaturesInBinaryImg`. |
| `AI.ComputerVision.SpatialFilters` | Свёрточные ядра: `CustomFilter` (базовый класс + произвольное ядро), `Smoothing` (среднее 3×3), `GaussianBlurFilter` (гауссово 3×3), `Sharpness` (повышение резкости), `HLine`/`WLine` (линейные детекторы). |
| `AI.ComputerVision.FrequencyDomain` | `FFT2D` — двумерное БПФ (CPU + cuFFT GPU), амплитудный/фазовый спектр, FFTShift, частотные фильтры (НЧ, ВЧ, гауссов, полосовой), поканальная обработка цветных изображений. `FftBackend`, `CuFftHandle`, `CudaFftInfo`. |
| `AI.ComputerVision.ImgTransforms` | `SobelTransform` (оператор Собеля → `SobelData`: модуль, GradX, GradY, фаза), `HOG` (гистограмма направленных градиентов). |
| `AI.ComputerVision.Statistics` | `ImageHistogram` — построение гистограммы яркости, эквализация (CDF + LUT). |
| `AI.ComputerVision.FiltersEachElements` | Поэлементные фильтры (`FilterEE`, `SigmoidalFilter`) через интерфейс `IFilterEE`. |
| `AI.ComputerVision.ImgFeatureExtractions` | `ImgOnnxExtractor` — извлечение признаков изображения ONNX-моделью. |

---

## GPU-ускорение (cuFFT)

Перечисление **`FftBackend`** управляет выбором вычислительного бэкенда для 2D FFT:

| Значение | Бэкенд | Зависимость |
|----------|--------|-------------|
| `Cpu` | `Parallel.For` + `Fft64` (in-place, double) | Всегда доступен |
| `Cuda` | cuFFT Z2Z через P/Invoke | `cufft64_10–12` + `cudart64` |

Переключение прозрачно: методы `FFT2D.Forward(image, backend)` и `FFT2D.Inverse(..., backend)` при `Cuda` пытаются выполнить расчёт на GPU, при неудаче автоматически откатываются на CPU. Класс **`CudaFftInfo`** кэширует результат пробы при первом обращении (`IsAvailable`, `StatusMessage`). Поддерживаются CUDA Toolkit 10.1 – 12.

Для цветных изображений поканальное FFT (`ForwardColor`, `FilterColor`, `InverseColor`) выполняется параллельно по трём каналам через `Parallel.Invoke`, что даёт дополнительный выигрыш при GPU-бэкенде.

---

## Работа с цветом

`ImageMatrixConverter` поддерживает:

- **Grayscale** — `BmpToMatr` (равновесное RGB→серый) или с пользовательским вектором весов.
- **Поканальное разложение** — `BmpToMatrRed`, `BmpToMatrGreen`, `BmpToMatrBlue`.
- **HSV** — H-компонента (`BmpToHMatr`).
- **Тензор** — `BmpToTensor` (H×W×3, порядок R-G-B).

Обратная сборка: `ToBitmap(Matrix)` (серое), `ToBitmap(Tensor)` (цветное), `Visualization(Matrix)` (цветовая карта).

---

## Бинарные изображения

Класс **`BinaryImg`** хранит `bool[,]`-матрицу, создаётся из `Matrix` или `SKBitmap` (пороговая функция). **`FeaturesInBinaryImg`** извлекает частотные признаки контура (FFT по комплексным координатам точек) с опциональной инвариантностью к повороту, масштабу и сдвигу.

---

## Роль в решении

- Пайплайн **«загрузка → фильтрация → признаки → классификация»** строится в связке с `AI.DataPrepaire`, `AI.ML` и `AI.NeuralNetworks`.
- Демо-модуль **`ComputerVisionModule`** в WebUI покрывает все ключевые сценарии: пространственные фильтры, спектр, частотную фильтрацию, градиенты, HOG, эквализацию, бинаризацию.
- `CompImg` позволяет быстро сравнивать изображения через FFT-дескрипторы (единичный вектор из обрезанного амплитудного спектра).

---

## Сборка

```bash
dotnet build src/AI.ComputerVision/AI.ComputerVision.csproj -c Release
```

Требуется `AllowUnsafeBlocks` (включён в `.csproj`). Для GPU-ускорения установите CUDA Toolkit и убедитесь, что `cufft64_*.dll` / `cudart64_*.dll` доступны в `PATH`.
