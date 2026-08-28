# Прямоугольный волновод TE10

Питающий тракт любой из трёх антенн. Определяет рабочую полосу,
диссипативные потери и — что важнее всего — максимальное поле в системе.

## Постановка задачи

Дан волновод со стенками $a \times b$ и материал. Требуется найти
критические частоты, рабочую полосу, волновое сопротивление, погонное
затухание и предельную передаваемую мощность по условию электрического
пробоя.

## Теория

**Критические частоты.** Основная мода TE10 распространяется выше

$$f_c = \frac{c}{2a}$$

Ближайшая высшая мода — TE20 либо TE01, смотря что ниже:

$$f_{next} = \min\left(\frac{c}{a},\ \frac{c}{2b}\right)$$

У стандартного отношения $a/b = 2$ обе совпадают и равны $2 f_c$.
Рабочая полоса берётся от $1.25 f_c$ до $0.95 f_{next}$: у самой отсечки
затухание и дисперсия растут неограниченно.

**Дисперсия.** Длина волны в волноводе и волновое сопротивление:

$$\lambda_g = \frac{\lambda}{\sqrt{1 - (f_c/f)^2}}, \qquad
Z_{TE10} = \frac{\eta_0}{\sqrt{1 - (f_c/f)^2}}$$

Обратите внимание на **деление**: умножение на корень дало бы соотношение
для TM-волн. У критической частоты сопротивление уходит в бесконечность,
с ростом частоты стремится к 377 Ом.

**Затухание в стенках.** Через поверхностное сопротивление
$R_s = 1/(\sigma\delta)$, где $\delta = \sqrt{1/(\pi f \mu_0 \sigma)}$:

$$\alpha_c = \frac{R_s}{b\,\eta_0\sqrt{1-(f_c/f)^2}}
\left[1 + \frac{2b}{a}\left(\frac{f_c}{f}\right)^2\right] \quad [\text{Нп/м}]$$

**Максимальное поле.** Из связи передаваемой мощности с амплитудой поля
в центре широкой стенки:

$$P = \frac{a b E_0^2}{4 Z_{TE10}} \quad\Longrightarrow\quad
E_0 = \sqrt{\frac{4 P Z_{TE10}}{a b}}$$

Это и есть самая напряжённая точка всего тракта: сечение здесь минимально.
В раскрыве рупора поле на два порядка меньше.

## Сложность

Замкнутые формулы, $O(1)$. Перебор стандартного ряда — $O(n)$ по 15
типоразмерам.

## API

| Член | Назначение |
|------|------------|
| `RectangularWaveguide.CutoffTE10Hz` | Критическая частота основной моды |
| `RectangularWaveguide.CutoffNextModeHz` | Критическая частота высшей моды |
| `RectangularWaveguide.BandLowHz` / `BandHighHz` | Рекомендованная полоса |
| `RectangularWaveguide.WaveImpedanceTE10(f)` | Волновое сопротивление |
| `RectangularWaveguide.GuideWavelength(f)` | Длина волны в волноводе |
| `RectangularWaveguide.AttenuationDbPerM(f, sigma)` | Погонное затухание |
| `RectangularWaveguide.PeakElectricField(P, f)` | Поле в горловине |
| `RectangularWaveguide.GetStandards()` | Ряд EIA от WR-975 до WR-28 |
| `RectangularWaveguide.SelectForFrequency(f)` | Подбор типоразмера под частоту |

## Код

```csharp
using AI.Microwave.Models;
using AI.Microwave.Physics;

var wg = RectangularWaveguide.SelectForFrequency(2.45e9);
var copper = MaterialProperties.GetStandardMaterials()[0];

Console.WriteLine($"{wg.Standard}: {wg.WidthMm} x {wg.HeightMm} мм");
Console.WriteLine($"fc = {wg.CutoffTE10Hz / 1e9:F3} ГГц, " +
                  $"полоса {wg.BandLowHz / 1e9:F2}...{wg.BandHighHz / 1e9:F2} ГГц");
Console.WriteLine($"Z = {wg.WaveImpedanceTE10(2.45e9):F0} Ом");
Console.WriteLine($"затухание {wg.AttenuationDbPerM(2.45e9, copper.Conductivity) * 1000:F1} дБ/км");
Console.WriteLine($"поле при 900 Вт: {wg.PeakElectricField(900, 2.45e9) / 1000:F1} кВ/м");
```

## Ограничения

- Учитывается только основная мода TE10; выше высшей критической частоты
  выдаётся предупреждение, но многомодовый режим не моделируется.
- Затухание считается для гладких стенок: шероховатость поверхности,
  сравнимая со скин-слоем (единицы микрон), заметно его увеличивает.
- Потери во фланцах, изгибах и переходах не учитываются.
- Диэлектрическое заполнение не поддерживается — волновод воздушный.

## См. также

- [Электрическая прочность](breakdown.md)
- [Пирамидальный рупор](horn.md)
