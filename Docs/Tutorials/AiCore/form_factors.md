# Форм-факторы сигнала

Безразмерные характеристики формы сигнала, широко используемые в **вибродиагностике**, **диагностике подшипников** и **контроле качества питающего напряжения**.

## Определения

| Фактор | Формула | Что показывает |
|---|---|---|
| **Пик-фактор** (crest) | $K_c = \dfrac{\max \lvert x \rvert}{\mathrm{RMS}}$ | Острота пиков |
| **Форм-фактор** (shape) | $K_s = \dfrac{\mathrm{RMS}}{\mathrm{mean}\lvert x \rvert}$ | Заострённость формы |
| **Импульс-фактор** | $K_i = \dfrac{\max \lvert x \rvert}{\mathrm{mean}\lvert x \rvert}$ | Импульсивность сигнала |

Напомним:

$$
\mathrm{RMS} = \sqrt{\tfrac{1}{N}\sum x_i^2}, \qquad \mathrm{mean}\lvert x \rvert = \tfrac{1}{N}\sum \lvert x_i \rvert.
$$

## Контрольные значения

| Сигнал | $K_c$ | $K_s$ | $K_i$ |
|---|---|---|---|
| Синус $\sin t$ | $\sqrt 2 \approx 1.414$ | $\pi/(2\sqrt 2) \approx 1.111$ | $\pi/2 \approx 1.571$ |
| Прямоугольник $\pm 1$ | $1$ | $1$ | $1$ |
| Треугольник | $\sqrt 3 \approx 1.732$ | $2/\sqrt 3 \approx 1.155$ | $2$ |
| Импульс (разреженный) | большой | большой | очень большой |

## Применения

- **Диагностика подшипников**: рост $K_c$ в спектре ускорений — признак развивающейся раковины/трещины;
- **Электроэнергетика**: $K_s \approx 1.11$ для чисто синусоидальной сети; отклонения указывают на искажения формы;
- **Система связи**: $K_i$ сигнала CDMA/OFDM нужен для выбора запаса усилителя (backoff).

## Код

```csharp
using AI.Statistics;

double crest  = FormStatistics.CrestFactor(signal);
double shape  = FormStatistics.ShapeFactor(signal);
double imp    = FormStatistics.ImpulseFactor(signal);
double rms    = Statistic.RMS(signal);
```
