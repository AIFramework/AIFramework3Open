# БИХ-фильтры по техническому заданию

Пространство имён `AI.DSP.IIR` проектирует рекурсивный фильтр одним путём: ТЗ → минимальный порядок →
аналоговый прототип (нули, полюсы, усиление) → преобразование частот → билинейное преобразование с
предыскажением → каскад звеньев второго порядка (SOS). Коэффициенты совпадают со `scipy.signal`
(`butter`, `cheby1`, `cheby2`, `ellip`, `bessel` с `output='sos'`) вплоть до порядков 10–20.

## Постановка задачи

Дано ТЗ: вид полосы (ФНЧ, ФВЧ, полосовой, режекторный), частота дискретизации $F_s$, границы полос
пропускания и задерживания, допустимое ослабление $R_p$ в полосе пропускания и требуемое затухание
$A_s$ в полосе задерживания. Нужно найти фильтр наименьшего порядка, выполняющий ТЗ, и применять его к
сигналу потоком или целиком, в том числе с нулевой фазой.

## Теория

**Предыскажение.** Билинейное преобразование $s = 2F\,(z-1)/(z+1)$ сжимает ось частот, поэтому
границы полос переводятся в аналоговые: $\Omega = \tan(\pi f/F_s)$.

**Порядок.** Для Баттерворта
$n = \left\lceil \lg\dfrac{10^{0{,}1A_s}-1}{10^{0{,}1R_p}-1} \Big/ \left(2\lg\dfrac{\Omega_s}{\Omega_p}\right)\right\rceil$;
для Чебышёва — через $\operatorname{arcch}$; для эллиптического — через полные эллиптические интегралы
$K(m)$: $n = \lceil K(k^2)K(1-k_1^2) / (K(1-k^2)K(k_1^2)) \rceil$, $k = \Omega_p/\Omega_s$,
$k_1^2 = (10^{0{,}1R_p}-1)/(10^{0{,}1A_s}-1)$.

**Прототипы** с частотой 1 рад/с: Баттерворт — полюсы на единичной окружности; Чебышёв I —
$p_m = -\operatorname{sh}(\mu + j\theta_m)$; Чебышёв II — обращённые полюсы и нули на мнимой оси;
эллиптический — через функции Якоби $\operatorname{sn}, \operatorname{cn}, \operatorname{dn}$;
Бессель — корни обратного полинома Бесселя.

**Преобразования частот:** $s \to s/\omega_0$ (ФНЧ), $s \to \omega_0/s$ (ФВЧ),
$s \to (s^2+\omega_0^2)/(\Delta\omega\, s)$ (ПФ), $s \to \Delta\omega\, s/(s^2+\omega_0^2)$ (РФ).

**Звенья.** Полюсы, ближайшие к единичной окружности, попадают в последние звенья, каждому полюсу —
ближайший нуль; так каскад устойчивее одной передаточной функции высокого порядка.

## Сложность

Дизайн — $O(n^2)$ по порядку $n$. Фильтрация — 5 умножений и 4 сложения на звено и отсчёт; звеньев
$\lceil n/2 \rceil$ (у ПФ и РФ — $n$).

## API

| Тип | Назначение |
|-----|------------|
| `IirSpecification` | ТЗ: `LowPass`, `HighPass`, `BandPass`, `BandStop`, `FromLinear`; противоречивое ТЗ — понятное исключение |
| `IirOrder.Estimate` | минимальный порядок и частоты среза (как `buttord` … `ellipord`) |
| `IirDesign.Design` | фильтр по ТЗ или по явному порядку и срезу |
| `AnalogPrototype`, `FrequencyTransform` | шаги пути по отдельности |
| `SecondOrderSections` | `Filter`, `FilterZeroPhase`, `FrequencyResponse`, `MagnitudeDb`, `Phase`, `GroupDelay`, `Zeros`, `Poles`, `IsStable`, `Verify`, `ToIirFilter` |
| `SosFilter` | потоковый фильтр в `double` или Q16 (блок `IMultirateBlock`) |
| `Biquad` | звенья RBJ: ФНЧ, ФВЧ, ПФ, режекторный, фазовый, пиковый, полочные |

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.DSP.IIR;
using AI.DSP.Multirate;

// ФНЧ: Fs = 600 Гц, пропускание до 45 Гц (1 дБ), задерживание от 55 Гц (40 дБ)
IirSpecification spec = IirSpecification.LowPass(600, 45, 55, rippleDb: 1, attenuationDb: 40);

foreach (IirFamily family in new[] { IirFamily.Butterworth, IirFamily.ChebyshevI, IirFamily.ChebyshevII, IirFamily.Elliptic })
{
    IirFilterDesign design = IirDesign.Design(family, spec);
    IirSpecificationReport report = design.Verify(spec);
    Console.WriteLine($"{family}: порядок {design.Order}, в полосе {report.PassbandRippleDb:F2} дБ, " +
                      $"затухание {report.StopbandAttenuationDb:F1} дБ");
}

// Явный порядок: полосовой эллиптический 6-го порядка
SecondOrderSections bp = IirDesign.Design(IirFamily.Elliptic, IirBandType.BandPass, 6, 600,
    new Vector(80.0, 160.0), rippleDb: 0.5, attenuationDb: 60).Sections;

var x = new Vector(Enumerable.Range(0, 1000).Select(i => Math.Sin(2 * Math.PI * 120 * i / 600.0)));
Vector y = bp.Filter(x);              // как scipy.signal.sosfilt
Vector y0 = bp.FilterZeroPhase(x);    // как scipy.signal.sosfiltfilt
Vector yq = bp.CreateFilter(Arithmetic.Q16, Q16Options.Recommended).ProcessBlock(x);
```

## Ограничения

- Порядок прототипа — до 256; для фильтра Бесселя порядок по ТЗ подбирается только для ФНЧ и ФВЧ.
- В Q16 коэффициенты квантуются с шагом $2^{-16}$: у узкополосных фильтров высокого порядка полюсы
  смещаются, проверяйте выход относительно `double`.
- Частота среза Чебышёва II — граница полосы задерживания, как в scipy.

## См. также

[Многоскоростная обработка](Multirate.md) · [Цифровые фильтры](Filtering.md)
