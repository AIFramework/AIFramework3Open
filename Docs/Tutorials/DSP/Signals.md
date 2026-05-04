# Генерация и модель сигналов

**Пространство имён:** `AI.DSP.DSPCore`  
**Класс:** `Signal` (статический)

---

## 1. Синусоидальный сигнал

\[
x(t) = A \sin(2\pi f t + \varphi)
\]

```csharp
Vector t = Vector.Seq(0, 1.0/fd, duration);
Vector signal = Signal.Sin(t, A: 1.0, f: 100, fi: 0);
```

---

## 2. ЛЧМ-сигнал (chirp, линейная частотная модуляция)

Сигнал, частота которого линейно изменяется от \(f_0\) до \(f_1\) за время \(T\):

\[
x(t) = \sin\!\left(2\pi \left(f_0 t + \frac{f_1 - f_0}{2T} t^2\right)\right)
\]

Скорость изменения частоты (chirp rate): \(\mu = (f_1 - f_0) / T\) [Гц/с].

**Применение:**
- Радиолокация (зондирующий сигнал).
- Гидроакустика (определение расстояния).
- Широкополосная связь.
- Анализ АЧХ устройств (свип-генератор).

```csharp
// Ручная генерация ЛЧМ
double mu = (f1 - f0) / duration;
var lfm = t.Transform(ti => Math.Sin(2 * Math.PI * (f0 * ti + 0.5 * mu * ti * ti)));
```

---

## 3. Затухающие колебания

\[
x(t) = A \cdot e^{-k t} \cdot \sin(2\pi f t + \varphi)
\]

Моделируют отклик колебательных систем, виброакустические сигналы, переходные процессы.

```csharp
Vector damped = Signal.DampedOscillations(t, f: 80, kDamp: -2.0, A: 1.0, fi: 0);
```

> **Примечание:** параметр `kDamp` передаётся как **отрицательное** число (например, −2.0), поскольку внутри реализован `exp(t * kDamp)`.

---

## 4. Амплитудно-модулированный сигнал (АМ)

\[
x(t) = \bigl(1 + m \cdot s(t)\bigr) \cdot \cos(2\pi f_\text{нес} t)
\]

где \(m\) — глубина модуляции (0 ÷ 1), \(s(t)\) — модулирующий сигнал.

```csharp
var modulating = Signal.Sin(t, m, fMod);
var am = (1 + modulating) * Signal.Sin(t, 1, fCarrier);
```

Огибающая АМ-сигнала извлекается преобразованием Гильберта (см. `FastHilbert.Envelope`).

---

## 5. Прямоугольный сигнал

```csharp
Vector rect = Signal.Rect(t, A: 1.0, f: 100);
```

---

## 6. Вектор времени и частот

```csharp
// Вектор времени 0..duration с шагом 1/fd
Vector t = Vector.Seq(0, 1.0 / fd, duration);

// Вектор частот для отображения спектра (0..fd/2)
Vector freqs = Signal.Frequency(N, fd);

// Центрированный вектор частот (-fd/2..fd/2)
Vector freqsCentered = Signal.FrequencyCentr(N, fd);
```
