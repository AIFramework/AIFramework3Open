# Факторные модели: Фама — Френч, Кархарт и главные компоненты

Разложение доходности портфеля на вознаграждение за факторы риска и альфу —
и извлечение скрытых факторов из корреляций методом главных компонент.

## Постановка задачи

Дано: избыточные доходности портфеля и ряды факторных премий (рынок,
размер, стоимость, инерция) либо только матрица доходностей активов.

Требуется: нагрузки портфеля на факторы, альфа с оценкой значимости, доля
объяснённой дисперсии — или, во втором случае, сами факторы, извлечённые из
данных.

Где встречается: оценка качества управляющего, проверка «а не покупает ли
фонд просто бету», построение факторных портфелей, снижение размерности
матрицы рисков.

## Теория

**Однофакторная модель (CAPM)** объясняет доходность рынком:
$r_p - r_f = \alpha + \beta(r_m - r_f) + \varepsilon$. Эмпирически она
объясняет мало: акции малой капитализации и «дешёвые» акции систематически
обгоняют предсказание.

**Трёхфакторная модель Фамы — Френча** добавляет два фактора:

$$
r_p - r_f \;=\; \alpha + \beta_m(r_m - r_f) + \beta_s\,\mathrm{SMB} + \beta_h\,\mathrm{HML} + \varepsilon,
$$

где SMB — премия малых компаний над крупными, HML — премия дешёвых над
дорогими. **Кархарт** добавляет четвёртый фактор инерции WML.

**Смысл упражнения** в переопределении альфы. Управляющий, показывающий 5%
сверх рынка, может просто держать малые дешёвые компании — тогда его альфа
относительно четырёхфакторной модели равна нулю, и платить за это управление
не нужно: тот же результат даёт дешёвый индексный фонд на нужные факторы.

Значимость альфы проверяется t-статистикой. Порог 1,96 при типичной трёхлетней
истории требует альфы порядка 4–5% годовых — вот почему статистически
доказанная альфа так редка.

**Вклад фактора** в доходность — произведение нагрузки на среднюю премию:
$\beta_j\bar f_j$. Сумма вкладов плюс альфа даёт полную доходность и служит
прямым ответом на вопрос «откуда взялся результат».

**Главные компоненты** извлекают факторы, когда наблюдаемых премий нет.
Собственные векторы ковариационной матрицы дают ортогональные направления
максимальной дисперсии:

$$
\Sigma \;=\; V\Lambda V^{\top},
\qquad
\text{доля}_k = \frac{\lambda_k}{\sum_j \lambda_j}.
$$

На рынках акций первая компонента объясняет 40–70% дисперсии и всегда
интерпретируется как рынок; вторая и третья обычно соответствуют отраслевым
или страновым контрастам.

## Сложность

| Ресурс | Оценка | Комментарий |
|--------|--------|-------------|
| Время  | $O(nk^2)$ на регрессию, $O(n^3)$ на разложение | $k$ факторов, $n$ активов |
| Память | $O(nk)$ | матрица факторов |

## API

| Метод | Описание |
|-------|----------|
| `FactorModels.Fit(excessReturns, factors, names, periodsPerYear, portfolio)` | Нагрузки и альфа |
| `FactorModels.PrincipalComponents(returns, factorCount)` | Скрытые факторы из данных |
| `FactorModelResult.Loadings` | Нагрузка, ошибка, t-статистика, вклад |
| `FactorModelResult.Alpha` / `AlphaTStatistic` / `HasAlpha` | Альфа и её значимость |
| `FactorModelResult.ExplainedReturn` | Доходность, объяснённая факторами |

Исходники: `src/AI.Economics/Portfolio/FactorModels.cs`.

## Код

```csharp
using AI.DataStructs.Algebraic;
using AI.Economics.Portfolio;
using AI.Statistics;

Random src = RandomEngine.Create(71);
var factors = new Matrix(180, 3);
var excess = new Vector(180);

for (int t = 0; t < factors.Height; t++)
{
    double marketPremium = RandomEngine.NextGaussian(src, 0.007, 0.040);
    double sizePremium = RandomEngine.NextGaussian(src, 0.002, 0.020);
    double valuePremium = RandomEngine.NextGaussian(src, 0.003, 0.024);

    factors[t, 0] = marketPremium;
    factors[t, 1] = sizePremium;
    factors[t, 2] = valuePremium;

    // Истинные нагрузки 1,1 / 0,5 / -0,3 и альфа 0,15% в месяц
    excess[t] = 0.0015 + (1.1 * marketPremium) + (0.5 * sizePremium) - (0.3 * valuePremium)
        + RandomEngine.NextGaussian(src, 0, 0.012);
}

FactorModelResult model = FactorModels.Fit(
    excess, factors, ["Рынок", "Размер", "Стоимость"], periodsPerYear: 12, portfolio: "Фонд роста");

foreach (FactorLoading loading in model.Loadings)
{
    Console.WriteLine($"{loading.Factor}: нагрузка {loading.Loading:F3} " +
                      $"(t = {loading.TStatistic:F2}), вклад {loading.Contribution:P2}");
}

Console.WriteLine($"Альфа {model.Alpha:P2} годовых, t = {model.AlphaTStatistic:F2}");
Console.WriteLine(model.HasAlpha ? "Альфа значима" : "Альфа статистически неотличима от нуля");
Console.WriteLine($"Факторы объясняют {model.RSquared:P1} дисперсии");
```

Разложение доходности — главный вывод для инвестиционного комитета:

```csharp
Console.WriteLine($"Полная доходность {model.TotalReturn:P2}");
Console.WriteLine($"Из них объяснено факторами {model.ExplainedReturn:P2}");
Console.WriteLine($"Необъяснённый остаток {model.TotalReturn - model.ExplainedReturn:P2}");
```

Когда наблюдаемых факторных премий нет, их извлекают из самих данных:

```csharp
var universe = new Matrix(240, 6);
for (int t = 0; t < universe.Height; t++)
{
    double common = RandomEngine.NextGaussian(src);
    double sector = RandomEngine.NextGaussian(src);

    for (int j = 0; j < universe.Width; j++)
    {
        double exposure = j < 3 ? 0.6 : -0.4;
        universe[t, j] = (0.7 * common) + (exposure * sector)
            + (0.5 * RandomEngine.NextGaussian(src));
    }
}

(Matrix components, Vector variance, Matrix loadings) =
    FactorModels.PrincipalComponents(universe, factorCount: 3);

for (int k = 0; k < variance.Count; k++)
    Console.WriteLine($"Компонента {k + 1}: объясняет {variance[k]:P1} дисперсии");

Console.WriteLine($"Нагрузка первого актива на первую компоненту {loadings[0, 0]:F3}");
Console.WriteLine($"Извлечено наблюдений факторов: {components.Height}");
Console.WriteLine(model.Interpret().ToLlmText());
```

## Ограничения

- Факторные премии для российского рынка не публикуются в готовом виде — их
  приходится конструировать самостоятельно, и результат чувствителен к
  правилам построения портфелей-факторов.
- Нагрузки нестабильны во времени. Фонд, менявший стиль, покажет усреднённые
  нагрузки, не описывающие ни один из периодов; считайте на скользящем окне.
- Значимая альфа на трёхлетнем окне встречается случайно примерно у каждого
  двадцатого фонда просто в силу множественного тестирования. Поправка на
  число проверенных фондов обязательна.
- Главные компоненты статистически ортогональны, но экономически
  неинтерпретируемы за пределами первой. Не приписывайте им смысл без
  проверки нагрузок.
- Число факторов при разложении выбирается произвольно. Правило «доля
  объяснённой дисперсии выше 80%» ведёт к переобучению на коротких историях.

## См. также

- [Метрики эффективности портфеля](portfolio_metrics.md) — альфа и бета в простой форме
- [Атрибуция Бринсона](attribution.md) — разложение по сегментам вместо факторов
- [Модель Блэка — Литтермана](black_litterman.md) — превращение факторных взглядов в портфель
