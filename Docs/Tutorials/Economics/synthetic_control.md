# Синтетический контроль

Построение искусственного двойника для одного объекта под воздействием:
взвешенная комбинация доноров, повторяющая его поведение до вмешательства, и
плацебо-тест вместо стандартной ошибки.

## Постановка задачи

Дано: временной ряд показателя по одному объекту, который в известный момент
подвергся воздействию, и ряды по нескольким объектам-донорам, которые ему не
подвергались.

Требуется: оценка эффекта воздействия и оценка её достоверности — при том что
объект под воздействием ровно один и классическая статистика неприменима.

Где встречается: эффект закона в одном регионе, последствия входа
конкурента на один рынок, влияние санкций на одну отрасль, результат
реорганизации в одном филиале.

## Теория

**Идея.** Ни один донор по отдельности не похож на объект под воздействием.
Но их взвешенная комбинация может воспроизвести его траекторию до
вмешательства — и тогда её продолжение после вмешательства служит
контрфактическим сценарием «что было бы».

**Оптимизация.** Веса ищутся минимизацией расхождения на периоде до
воздействия при ограничениях выпуклости:

$$
\min_{w}\;\sum_{t=1}^{T_0}\left(y_t - \sum_j w_j\,y_{jt}\right)^2
\qquad\text{при}\qquad
w_j \ge 0,\;\; \sum_j w_j = 1 .
$$

Ограничения принципиальны. Неотрицательность и сумма единица запрещают
экстраполяцию: синтетический объект остаётся внутри выпуклой оболочки
доноров. Это делает метод честным — если объект под воздействием
экстремален по показателю, хорошего двойника просто не найдётся, и метод это
покажет большой ошибкой подгонки.

Задача решается проекционным градиентным спуском с проекцией на симплекс.

**Эффект** — разрыв между фактическим и синтетическим рядом после
вмешательства:

$$
\hat\tau_t \;=\; y_t - \sum_j w_j\,y_{jt},\qquad t > T_0 .
$$

**Проверка достоверности.** Классических стандартных ошибок нет — объект
один. Вместо них применяется **плацебо-тест**: та же процедура запускается
для каждого донора, как если бы воздействию подвергся он. Считается
отношение ошибок:

$$
r_j \;=\; \frac{\mathrm{RMSPE}^{\text{после}}_j}{\mathrm{RMSPE}^{\text{до}}_j},
$$

и p-значение — ранг настоящего объекта в этом распределении. При десяти
донорах минимально достижимое p-значение равно $1/11 \approx 0{,}09$: это
структурное ограничение, а не недостаток данных.

**Качество подгонки до воздействия** — обязательный фильтр. Если RMSPE до
вмешательства велик, синтетический двойник плох, и разрыв после ничего не
доказывает.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(I\,T\,J)$, $\times J$ на плацебо | $J$ доноров, $I$ итераций |
| Память | $O(TJ)$ | матрица доноров |

## API

| Метод | Описание |
|-------|----------|
| `SyntheticControl.Build(treated, donors, donorNames, treatmentPeriod, unitName)` | Веса, разрыв, плацебо |
| `SyntheticControlResult.Weights` | Веса доноров |
| `SyntheticControlResult.Actual` / `Synthetic` / `Gap` | Три ряда для графика |
| `SyntheticControlResult.PreTreatmentRmspe` | Качество подгонки до воздействия |
| `SyntheticControlResult.RmspeRatio` / `PValue` | Плацебо-тест |
| `SyntheticControlResult.AverageEffect` | Средний эффект после вмешательства |

Исходники: `src/AI.Economics/Econometrics/SyntheticControl.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Statistics;

Random rng = RandomEngine.Create(151);
int periods = 32, donorCount = 10, treatmentPeriod = 22;
const double trueEffect = 5.0;

var donors = new Matrix(periods, donorCount);
var treated = new Vector(periods);

var common = new double[periods];
for (int t = 0; t < periods; t++)
    common[t] = 10 + (0.3 * t) + RandomEngine.NextGaussian(rng, 0, 1);

for (int j = 0; j < donorCount; j++)
{
    double loading = 0.7 + (0.6 * rng.NextDouble());
    for (int t = 0; t < periods; t++)
        donors[t, j] = (common[t] * loading) + RandomEngine.NextGaussian(rng, 0, 0.3);
}

for (int t = 0; t < periods; t++)
{
    // Объект воспроизводится комбинацией первых двух доноров
    treated[t] = (0.5 * donors[t, 0]) + (0.5 * donors[t, 1])
        + RandomEngine.NextGaussian(rng, 0, 0.15);

    if (t >= treatmentPeriod) treated[t] += trueEffect;
}

SyntheticControlResult synthetic = SyntheticControl.Build(
    treated, donors, donorNames: null, treatmentPeriod, unitName: "Регион A");

Console.WriteLine($"Истинный эффект {trueEffect:F2}");
Console.WriteLine($"Средний эффект {synthetic.AverageEffect:F4}");
Console.WriteLine($"Ошибка подгонки до вмешательства {synthetic.PreTreatmentRmspe:F4}");
Console.WriteLine($"Ошибка после {synthetic.PostTreatmentRmspe:F4}, " +
                  $"отношение {synthetic.RmspeRatio:F2}");
Console.WriteLine($"p-значение плацебо-теста {synthetic.PValue:F3}");
```

Веса показывают, из кого собран двойник, — это содержательный результат:

```csharp
foreach (DonorWeight weight in synthetic.Weights.Where(w => w.Weight > 1e-3)
    .OrderByDescending(w => w.Weight))
{
    Console.WriteLine($"{weight.Donor}: {weight.Weight:P1}");
}

Console.WriteLine($"Активных доноров {synthetic.ActiveDonors} из {donorCount}");
```

Разрыв по периодам — то, что показывают на слайде:

```csharp
for (int t = treatmentPeriod - 3; t < periods; t++)
{
    string mark = t < treatmentPeriod ? "до" : "после";
    Console.WriteLine($"Период {t + 1} ({mark}): факт {synthetic.Actual[t]:F2}, " +
                      $"синтетика {synthetic.Synthetic[t]:F2}, разрыв {synthetic.Gap[t]:F2}");
}

Console.WriteLine("Плацебо-тест по донорам:");
foreach ((string donor, double ratio) in synthetic.Placebo.OrderByDescending(p => p.Ratio).Take(3))
    Console.WriteLine($"  {donor}: отношение ошибок {ratio:F2}");

Console.WriteLine(synthetic.Interpret().ToLlmText());
```

## Ограничения

- Точность p-значения ограничена числом доноров. С пятью донорами минимальное
  достижимое значение — 1/6, и значимость на уровне 5% недостижима в
  принципе.
- Метод требует длинного периода до воздействия — не менее 10–15 наблюдений.
  На коротком периоде подгонка достигается случайно, и контрфактический
  прогноз ненадёжен.
- Доноры не должны подвергаться ни воздействию, ни его переливам. Соседний
  регион, затронутый той же политикой, портит контроль.
- Хорошая подгонка до вмешательства не гарантирует правильности прогноза
  после: соотношение весов могло измениться по независимым причинам.
- При наличии нескольких объектов под воздействием предпочтительнее
  [разность разностей](causal_did.md) — она даёт стандартные ошибки и
  проверяемые допущения.

## См. также

- [Разность разностей](causal_did.md) — когда объектов под воздействием много
- [Разрывный дизайн](causal_rdd.md) — эффект на пороге правила
- [Векторная авторегрессия](var_model.md) — альтернативный контрфактический прогноз
