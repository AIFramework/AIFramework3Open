# Многолучевое распространение радиоволн

Сигнал приходит к приёмнику не одним лучом, а многими: напрямую, отражённым от земли и стен,
огибающим крыши. Лучи приходят с разными задержками, фазами и доплеровскими сдвигами, и их сумма —
многолучевой канал — определяет, насколько широкую полосу можно передать без эквалайзера и как
глубоко сигнал замирает при движении.

## Постановка задачи

Дана геометрия трассы — передатчик, приёмник, отражающие поверхности и их материалы — либо её
статистическое описание. Требуется найти пути распространения и по ним комплексную импульсную и
частотную характеристики канала, разброс задержек, полосу и время когерентности, K-фактор, а затем
пропустить через канал сигнал.

## Теория

**Канал как сумма путей.** Путь k — задержка τₖ, комплексная амплитуда aₖ и доплеровский сдвиг f_Dk:

$$h(t, \tau) = \sum_k a_k\, e^{j2\pi f_{Dk} t}\, \delta(\tau - \tau_k), \qquad H(f) = \sum_k a_k\, e^{-j2\pi f \tau_k}$$

У прямого луча в свободном пространстве $a = \frac{\lambda}{4\pi d} e^{-j2\pi d/\lambda}$.

**Разброс задержек и полоса когерентности.** Разброс $\sigma_\tau$ — второй центральный момент задержек
по мощности путей. Полоса когерентности — разнос частот, на котором корреляция
$R(\Delta f) = \sum P_k e^{-j2\pi \Delta f \tau_k} / \sum P_k$ опускается до 0,5; правило $B_c \approx 1/(5\sigma_\tau)$
даёт тот же порядок. Сигнал шире $B_c$ замирает частотно-избирательно.

**Отражение.** Коэффициенты Френеля при угле скольжения ψ и комплексной проницаемости
$\varepsilon = \varepsilon' - j\sigma/(2\pi f \varepsilon_0)$:

$$\Gamma_\perp = \frac{\sin\psi - \sqrt{\varepsilon - \cos^2\psi}}{\sin\psi + \sqrt{\varepsilon - \cos^2\psi}}, \qquad
\Gamma_\parallel = \frac{\varepsilon \sin\psi - \sqrt{\varepsilon - \cos^2\psi}}{\varepsilon \sin\psi + \sqrt{\varepsilon - \cos^2\psi}}$$

**Двухлучевая модель.** Прямой и отражённый от земли лучи. За точкой перелома $d_c = 4h_1h_2/\lambda$
они почти в противофазе, и потери растут как $40\lg d - 20\lg(h_1h_2)$ — на 40 дБ за декаду.

**Дифракция на крае.** Параметр $\nu = h\sqrt{2(d_1+d_2)/(\lambda d_1 d_2)}$; поле за краем относительно
свободного пространства $F(\nu) = \frac{1+j}{2}\int_\nu^\infty e^{-j\pi t^2/2}\,dt$. При $\nu = 0$ ослабление 6,02 дБ.

**Замирания во времени.** Приёмник, движущийся со скоростью v среди рассеивателей, видит
комплексный гауссов процесс с автокорреляцией $J_0(2\pi f_D \tau)$, $f_D = v/\lambda$. Огибающая
пересекает уровень ρ (в долях среднеквадратичной) вверх $\sqrt{2\pi} f_D \rho e^{-\rho^2}$ раз в секунду,
а средний провал длится $(e^{\rho^2}-1)/(\rho f_D\sqrt{2\pi})$.

**Модели TR 38.901.** Профили TDL-A…E — нормированные задержки и мощности отводов с разбросом
задержек, равным единице; реальные задержки получаются умножением на нужный разброс. У TDL-D и TDL-E
первый отвод — прямой луч с K = 13,3 и 22 дБ и доплеровским сдвигом 0,7·f_D.

## Сложность

- Трассировка методом изображений: $R^N$ последовательностей отражателей для порядка N и проверка
  заслонения каждого отрезка — $O(R^{N+1} N)$.
- Частотная характеристика: $O(K)$ на частоту по числу путей K.
- Прохождение сигнала: $O(M K)$ на M отсчётов с окном дробной задержки из 32 отсчётов.
- Процесс замираний: $O(S)$ на отсчёт по числу синусоид S.

## API

| Член | Назначение |
|------|------------|
| `PropagationPath` | Путь: задержка, амплитуда, доплер, углы прихода и ухода, порядок |
| `MultipathChannel` | Канал из путей: `FrequencyResponse`, `ImpulseResponse`, `Apply`, `RmsDelaySpreadSeconds`, `CoherenceBandwidthHz`, `CoherenceTimeSeconds`, `RicianKFactor`, `Interpret` |
| `RadioMaterial` | Проницаемость и проводимость по ITU-R P.2040: бетон, кирпич, стекло, металл, грунты и другие |
| `FresnelReflection.Coefficients` | Коэффициенты отражения для двух поляризаций |
| `TwoRayGround` | Двухлучевая модель: канал, потери, точка перелома, асимптота |
| `KnifeEdgeDiffraction` | Параметр ν, точное F(ν), приближение ITU-R P.526, дифракционный путь |
| `RayTracingScene`, `PlanarReflector`, `RadioEndpoint` | Трассировка методом изображений с поляризацией, заслонением, ДН и доплером |
| `FadingProcess` | Рэлеевские и райсовские замирания суммой синусоид, автокорреляция, частота пересечений, длительность провалов |
| `TappedDelayLineModel`, `TappedDelayLineChannel` | Профили TDL-A…E из TR 38.901 и их реализации с замираниями |

## Код

```csharp
using System.Numerics;
using AI.Microwave.Propagation;
using Vector3 = AI.Geometry.Primitives.Vector3;

// Двухлучевая модель: за точкой перелома потери растут на 40 дБ за декаду
double breakpoint = TwoRayGround.BreakpointDistanceM(30, 1.5, 2e9);
double loss = TwoRayGround.PathLossDb(10 * breakpoint, 30, 1.5, 2e9, RadioMaterial.MediumDryGround);
Console.WriteLine($"Точка перелома {breakpoint:F0} м, потери на 10·d_c — {loss:F1} дБ");

// Улица: асфальт и две бетонные стены, приёмник едет к передатчику со скоростью 15 м/с
var scene = new RayTracingScene(3.5e9)
    .Add(PlanarReflector.Infinite(new Vector3(0, 0, 0), new Vector3(0, 0, 1), RadioMaterial.MediumDryGround, "асфальт"))
    .Add(new PlanarReflector(new Vector3(-50, -10, 0), new Vector3(300, 0, 0), new Vector3(0, 0, 20), RadioMaterial.Concrete, "дом слева"))
    .Add(new PlanarReflector(new Vector3(-50, 10, 0), new Vector3(300, 0, 0), new Vector3(0, 0, 20), RadioMaterial.Concrete, "дом справа"));

MultipathChannel street = scene.Trace(
    new RadioEndpoint(new Vector3(0, 3, 10)),
    new RadioEndpoint(new Vector3(200, -4, 1.5)) { Velocity = new Vector3(-15, 0, 0) },
    maxReflections: 3);

Console.WriteLine($"Путей {street.Count}, разброс задержек {street.RmsDelaySpreadSeconds * 1e9:F1} нс");
Console.WriteLine($"Полоса когерентности {street.CoherenceBandwidthHz() / 1e6:F2} МГц, доплер до {street.MaxDopplerHz:F0} Гц");
Console.WriteLine(street.Interpret());

// Стохастический канал TR 38.901: TDL-C, разброс 300 нс, 30 км/ч на 3,5 ГГц
double doppler = 30 / 3.6 / (299792458.0 / 3.5e9);
TappedDelayLineChannel tdl = TappedDelayLineModel.TdlC.Realize(300e-9, doppler, new Random(1));

Complex[] signal = Enumerable.Range(0, 1024)
    .Select(n => Complex.FromPolarCoordinates(1, 2 * Math.PI * 0.05 * n))
    .ToArray();

Complex[] received = tdl.Apply(signal, sampleRate: 30.72e6);
Console.WriteLine($"Мгновенное усиление {tdl.Snapshot(0).NarrowbandGainDb:F1} дБ, отсчётов на выходе {received.Length}");
```

## Ограничения

- Трассировка учитывает только зеркальные отражения от плоских поверхностей. Дифракция на рёбрах
  считается отдельно (`KnifeEdgeDiffraction`), прохождения сквозь стены и диффузного рассеяния нет:
  реальный разброс задержек обычно больше лучевого.
- Земля плоская и гладкая, кривизна Земли и поверхностная волна не учтены.
- Материалы ITU-R P.2040 заданы в своих диапазонах частот; грунт ниже 1 ГГц задавайте явно через
  `RadioMaterial.FromConstants`.
- Профили TDL описывают один канал «антенна — антенна» без углов прихода. Кластерная модель с углами,
  MIMO-матрицами и пространственной согласованностью — [канал TR 38.901](tr38901.md); фиксированных
  профилей CDL-A…E нет.
- Мощности отводов TDL нормированы на единицу: потери на трассе и затенение добавляются отдельно,
  например по `PathLoss` и `LinkBudget`.
- Старая `AI.DSP.Multiray` — эскиз без отражений; эта модель её не использует.

## См. также

- [Канал TR 38.901](tr38901.md) — кластеры, MIMO, движение абонента и пространственная согласованность
- [Покрытие сети связи](coverage.md) — карты покрытия, SINR и потери по рельефу ITU-R P.1812
- [Пределы облучения](exposure.md) — диаграммы направленности `AntennaPattern`, используемые в трассировке
- [Распределения Релея и Райса](../AiCore/rayleigh_rice.md) — статистика огибающей при замираниях
