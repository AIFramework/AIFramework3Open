# Tensor2Tensor (ONNX)

## Задача

`Tensor2Tensor` — универсальная обёртка для ONNX-моделей, принимающих **трёхмерный тензор** на вход и возвращающих тензор на выход. Типичный пример — нейронные сети обработки изображений.

## Раскладка каналов: LibType

Разные фреймворки используют разный порядок осей:

| LibType | Порядок осей | Пример формы |
|---------|-------------|--------------|
| `Keras` | $[H, W, C]$ | $[224, 224, 3]$ |
| `PyTorch` | $[C, H, W]$ | $[3, 224, 224]$ |
| `InverseCh` | $[D, H, W]$ | аналогично PyTorch |

`Tensor2Tensor` автоматически переупорядочивает оси при передаче в модель и обратно.

## Свойства модели

```csharp
using AI.ONNX;

using var t2t = new Tensor2Tensor("model.onnx",
    libType:    LibType.Keras,
    libTypeOut: LibType.Keras);

// Входная форма (из метаданных модели)
Console.WriteLine($"Вход:  [{t2t.InputH} × {t2t.InputW} × {t2t.InputD}]");
Console.WriteLine($"Выход: [{t2t.OutpH} × {t2t.OutpW} × {t2t.OutpD}]");
Console.WriteLine($"Имя входа:  {t2t.InputName}");
Console.WriteLine($"Имя выхода: {t2t.OutputName}");
```

## Инференс

```csharp
using AI.DataStructs.Algebraic;

// Создание тензора [H, W, C]
var img = new Tensor(224, 224, 3);
// ... заполнить нормализованными пикселями ...

// Прямой проход
Tensor features = t2t.Transform(img);
// features — выходной тензор (например, [7, 7, 512])
```

## Типичный пайплайн обработки изображений

$$
\text{Image} \xrightarrow{\text{Resize}} \text{Tensor}[H,W,C] \xrightarrow{\text{Normalize}} \hat{T} \xrightarrow{\text{ONNX}} \text{Features}
$$

**Нормализация ImageNet:**
$$
\hat{x}_c = \frac{x_c / 255 - \mu_c}{\sigma_c}, \quad \mu = [0.485, 0.456, 0.406], \quad \sigma = [0.229, 0.224, 0.225]
$$
