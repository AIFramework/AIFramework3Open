# Двумерное преобразование Фурье (2D FFT)

## Основы

2D FFT разлагает изображение на частотные компоненты: от плавных градиентов (низкие частоты) до резких краёв (высокие частоты).

Для изображения $f(x, y)$ размером $M \times N$:

$$
F(u, v) = \sum_{x=0}^{M-1}\sum_{y=0}^{N-1} f(x,y) \cdot e^{-j2\pi\left(\frac{ux}{M} + \frac{vy}{N}\right)}
$$

## Разделимость

2D FFT эффективно вычисляется как последовательность 1D FFT:
1. Применить 1D FFT к каждой **строке** изображения
2. Применить 1D FFT к каждому **столбцу** результата

$$
F(u, v) = \text{FFT}_{\text{cols}}\left(\text{FFT}_{\text{rows}}(f)\right)
$$

Сложность: $O(MN\log(MN))$ вместо $O(M^2 N^2)$ при прямом вычислении.

## Амплитудный спектр

$$
|F(u,v)| = \sqrt{\text{Re}^2(F) + \text{Im}^2(F)}
$$

С логарифмическим масштабированием для визуализации:

$$
S(u,v) = \ln(1 + |F(u,v)|)
$$

## Фазовый спектр

$$
\phi(u,v) = \arctan\frac{\text{Im}(F)}{\text{Re}(F)}
$$

Фаза определяет пространственное расположение деталей.

## FFTShift

Перемещает нулевую частоту из угла в центр изображения:

$$
F_{\text{shifted}}(u,v) = F\left((u + M/2) \bmod M, \; (v + N/2) \bmod N\right)
$$

## Частотные фильтры

### Идеальный низкочастотный (Low-Pass)

$$
H(u,v) = \begin{cases} 1 & \sqrt{u^2 + v^2} \leq D_0 \\ 0 & \text{иначе} \end{cases}
$$

Пропускает только низкие частоты → размывает изображение.

### Идеальный высокочастотный (High-Pass)

$$
H(u,v) = 1 - H_{\text{LP}}(u,v)
$$

Пропускает только высокие частоты → выделяет края.

### Гауссов фильтр

$$
H(u,v) = e^{-\frac{u^2 + v^2}{2\sigma^2}}
$$

Плавное затухание без артефактов Гиббса.

### Полосовой (Band-Pass)

$$
H(u,v) = \begin{cases} 1 & D_L \leq \sqrt{u^2 + v^2} \leq D_H \\ 0 & \text{иначе} \end{cases}
$$

## Цветные изображения

Для RGB-изображений 2D FFT применяется **поканально**:

$$
F_R, F_G, F_B = \text{FFT2D}(R), \text{FFT2D}(G), \text{FFT2D}(B)
$$

Фильтрация и обратное преобразование выполняются независимо для каждого канала, результаты собираются обратно в цветное изображение.

## API

```csharp
using AI.ComputerVision.FrequencyDomain;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using SkiaSharp;

// ── Серое изображение ────────────────────
Matrix gray = ImageMatrixConverter.BmpToMatr(bitmap);

// Прямое 2D FFT
ComplexMatrix spectrum = FFT2D.Forward(gray);

// Амплитудный спектр
Matrix magnitude = FFT2D.MagnitudeSpectrum(spectrum, logScale: true);
Matrix shifted   = FFT2D.FFTShift(magnitude);

// Обратное 2D FFT
Matrix restored = FFT2D.Inverse(spectrum);

// ── Фильтрация ──────────────────────────
var filtered = FFT2D.LowPassFilter(spectrum, cutoffRadius: 30);
var hpFiltered = FFT2D.HighPassFilter(spectrum, cutoffRadius: 20);
var gaussLP = FFT2D.GaussianFilter(spectrum, sigma: 15, lowPass: true);
var bandPass = FFT2D.BandPassFilter(spectrum, rLow: 10, rHigh: 50);

Matrix result = FFT2D.Inverse(filtered);

// ── Цветные изображения ─────────────────
var (specR, specG, specB) = FFT2D.ForwardColor(bitmap);
SKBitmap colorResult = FFT2D.InverseColor(specR, specG, specB,
    bitmap.Width, bitmap.Height);

// Фильтрация цветного одной строкой
SKBitmap lpColor = FFT2D.FilterColor(bitmap,
    s => FFT2D.GaussianFilter(s, sigma: 20, lowPass: true));

// Средний спектр RGB
Matrix avgMag = FFT2D.ColorMagnitudeSpectrum(bitmap);
```
