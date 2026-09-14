using AI.Econometrics;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>regress</c>: регрессия со статистическим выводом.
/// </summary>
/// <remarks>
/// От <c>ml.linreg</c> и <c>ml.multireg</c> отличается вопросом, на который отвечает. Там —
/// «что предсказать», здесь — «насколько можно верить коэффициенту»: стандартные ошибки,
/// p-значения, интервалы и ковариации, устойчивые к нарушенным допущениям. Модель машинного
/// обучения с перевёрнутым знаком коэффициента при высокой точности — рабочая модель;
/// регрессия с таким изъяном — неверный вывод.
/// <para>
/// Вход — таблица и имена колонок, а не матрица: коэффициент без имени переменной ничего не
/// значит, а порядок столбцов матрицы путается. Свободный член в таблице коэффициентов
/// называется <c>const</c>.
/// </para>
/// </remarks>
[ScriptModule("regress", "Регрессия с выводом: p-значения, панели, IV", Version = "0.1")]
public static class RegressModule
{
    [ScriptFn("ols", "МНК с выводом: стандартные ошибки, p-значения, устойчивые ковариации",
        Example = "let m = regress.ols(data, \"wage\", [\"educ\", \"exper\"], variance: \"hc1\")")]
    public static ScriptRecord Ols(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("ковариация: classical, hc0, hc1, hc2, hc3, newey_west, cluster")] string variance = "classical",
        [ScriptParam("колонка кластеров; включает кластерные ошибки")] string cluster = "",
        [ScriptParam("лаги Ньюи — Уэста; 0 — по правилу Бартлетта")] int lags = 0,
        [ScriptParam("колонка весов наблюдений")] string weights = "",
        [ScriptParam("добавить свободный член const")] bool intercept = true)
    {
        const string function = "regress.ols";

        RobustVariance kind = ScriptData.Kind<RobustVariance>(variance, "variance", function,
            ("classical", RobustVariance.Classical),
            ("hc0", RobustVariance.Hc0),
            ("hc1", RobustVariance.Hc1),
            ("hc2", RobustVariance.Hc2),
            ("hc3", RobustVariance.Hc3),
            ("newey_west", RobustVariance.NeweyWest),
            ("cluster", RobustVariance.Clustered));

        // Колонка кластеров сама говорит, какие нужны ошибки; противоречие с явно выбранной
        // ковариацией — почти наверняка недосмотр, и молча выбрать одно из двух было бы хуже.
        if (cluster.Length > 0 && kind == RobustVariance.Classical) kind = RobustVariance.Clustered;

        if (cluster.Length > 0 && kind != RobustVariance.Clustered)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: колонка кластеров задана вместе с variance: \"{variance}\"",
                "кластерные ошибки — это variance: \"cluster\"; уберите одно из двух");
        }

        if (kind == RobustVariance.Clustered && cluster.Length == 0)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: для кластерных ошибок не указана колонка кластеров",
                "добавьте cluster: \"имя колонки\"");
        }

        var options = new RegressionOptions
        {
            Variance = kind,
            Lags = lags,
            Clusters = cluster.Length > 0 ? ScriptData.Codes(data, cluster, function) : null,
            Weights = weights.Length > 0 ? ScriptData.Column(data, weights, function) : null,
            AddIntercept = intercept,
        };

        RegressionResult result = AI.Econometrics.LinearRegression.Fit(
            ScriptData.Columns(data, x, function), ScriptData.Column(data, y, function), x, options);

        return ScriptData.Record(result,
            ("model", result.Model),
            ("coefficients", ScriptData.Coefficients(result.Coefficients)),
            ("r2", result.RSquared),
            ("adj_r2", result.AdjustedRSquared),
            ("f_stat", result.FStatistic),
            ("f_p", result.FPValue),
            ("sigma", result.Sigma),
            ("aic", result.Aic),
            ("bic", result.Bic),
            ("log_likelihood", result.LogLikelihood),
            ("observations", result.Observations),
            ("parameters", result.Parameters),
            ("fitted", result.Fitted),
            ("residuals", result.Residuals));
    }

    /// <summary>
    /// Квантильная регрессия.
    /// </summary>
    /// <remarks>
    /// Нужна там, где среднее скрывает главное: влияние на хвост распределения — на дорогие
    /// заказы, на долгие задержки — бывает другим по знаку, чем на середину.
    /// </remarks>
    [ScriptFn("quantile", "Квантильная регрессия: влияние на медиану или хвост распределения",
        Example = "let m = regress.quantile(data, \"delay\", [\"load\"], q: 0.9)")]
    public static ScriptRecord Quantile(
        IScriptContext context,
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("квантиль в (0, 1); 0.5 — медиана")] double q = 0.5,
        [ScriptParam("повторов бутстрепа для ошибок")] int bootstrap = 300)
    {
        const string function = "regress.quantile";

        ScriptData.Require(q is > 0 and < 1, $"{function}: квантиль лежит строго между 0 и 1");
        ScriptData.Require(bootstrap >= 10, $"{function}: повторов бутстрепа нужно хотя бы десять");

        QuantileRegressionResult result = QuantileRegression.Fit(
            ScriptData.Columns(data, x, function), ScriptData.Column(data, y, function),
            q, x, bootstrap, context.Random.Next());

        return ScriptData.Record(result,
            ("q", result.Quantile),
            ("coefficients", ScriptData.Coefficients(result.Coefficients)),
            ("pseudo_r2", result.PseudoRSquared),
            ("objective", result.Objective),
            ("bootstrap", result.BootstrapSamples),
            ("observations", result.Observations),
            ("residuals", result.Residuals));
    }

    [ScriptFn("binary", "Логит и пробит: вероятность события 0/1 и предельные эффекты",
        Example = "let m = regress.binary(data, \"bought\", [\"price\", \"income\"], kind: \"probit\")")]
    public static ScriptRecord Binary(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка отклика из нулей и единиц")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("связь: \"logit\" либо \"probit\"")] string kind = "logit")
    {
        const string function = "regress.binary";

        LimitedDependentModel model = ScriptData.Kind<LimitedDependentModel>(kind, "kind", function,
            ("logit", LimitedDependentModel.Logit),
            ("probit", LimitedDependentModel.Probit));

        LimitedDependentResult result = LimitedDependent.Fit(
            ScriptData.Columns(data, x, function), ScriptData.Binary(data, y, function), model, x);

        return Limited(result, kind, ("accuracy", result.Accuracy));
    }

    [ScriptFn("count", "Регрессия счётного отклика: Пуассон и отрицательная биномиальная",
        Example = "let m = regress.count(data, \"visits\", [\"age\"], kind: \"negbin\")")]
    public static ScriptRecord Count(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка неотрицательных целых")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("модель: \"poisson\" либо \"negbin\" — при сверхдисперсии")] string kind = "poisson")
    {
        const string function = "regress.count";

        LimitedDependentModel model = ScriptData.Kind<LimitedDependentModel>(kind, "kind", function,
            ("poisson", LimitedDependentModel.Poisson),
            ("negbin", LimitedDependentModel.NegativeBinomial));

        LimitedDependentResult result = LimitedDependent.Fit(
            ScriptData.Columns(data, x, function), ScriptData.Column(data, y, function), model, x);

        return Limited(result, kind, ("dispersion", result.ScaleParameter));
    }

    /// <summary>
    /// Тобит-регрессия.
    /// </summary>
    /// <remarks>
    /// Для отклика, который упирается в порог: расходы, которых у многих нет вовсе, часы работы
    /// у неработающих. Обычный МНК на таких данных занижает эффект, потому что считает нули
    /// настоящими значениями, а не границей.
    /// </remarks>
    [ScriptFn("tobit", "Тобит: отклик, цензурированный снизу порогом",
        Example = "let m = regress.tobit(data, \"spend\", [\"income\"], censor: 0)")]
    public static ScriptRecord Tobit(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("порог цензурирования снизу")] double censor = 0)
    {
        const string function = "regress.tobit";

        LimitedDependentResult result = LimitedDependent.Fit(
            ScriptData.Columns(data, x, function), ScriptData.Column(data, y, function),
            LimitedDependentModel.Tobit, x, censor);

        return Limited(result, "tobit", ("sigma", result.ScaleParameter), ("censored_share", result.CensoredShare));
    }

    /// <summary>
    /// Инструментальные переменные.
    /// </summary>
    /// <remarks>
    /// Рядом с оценкой возвращается обычный МНК по тем же данным и первая стадия: без них не
    /// видно ни того, изменил ли инструмент ответ, ни того, не слаб ли он — а слабый инструмент
    /// даёт оценку хуже МНК, которую исправлял.
    /// </remarks>
    [ScriptFn("iv", "Инструментальные переменные: эффект эндогенного регрессора",
        Example = "let m = regress.iv(data, \"wage\", [\"educ\"], [\"distance\"], exogenous: [\"exper\"])")]
    public static ScriptRecord Iv(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("эндогенные регрессоры")] string[] endogenous,
        [ScriptParam("инструменты")] string[] instruments,
        [ScriptParam("экзогенные регрессоры")] string[]? exogenous = null,
        [ScriptParam("оценка: \"2sls\" либо \"gmm\"")] string kind = "2sls",
        [ScriptParam("устойчивые ошибки для 2sls")] bool robust = true)
    {
        const string function = "regress.iv";

        bool gmm = ScriptData.Kind<bool>(kind, "kind", function, ("2sls", false), ("gmm", true));
        bool hasExogenous = exogenous is { Length: > 0 };

        var endogenousData = ScriptData.Columns(data, endogenous, function);
        var exogenousData = hasExogenous ? ScriptData.Columns(data, exogenous!, function) : null;
        var instrumentData = ScriptData.Columns(data, instruments, function);
        var response = ScriptData.Column(data, y, function);
        string[]? exogenousNames = hasExogenous ? exogenous : null;

        IvResult result = gmm
            ? InstrumentalVariables.GeneralizedMethodOfMoments(
                endogenousData, exogenousData, instrumentData, response, endogenous, exogenousNames)
            : InstrumentalVariables.TwoStage(
                endogenousData, exogenousData, instrumentData, response, endogenous, exogenousNames, robust);

        return ScriptData.Record(result,
            ("model", result.Model),
            ("coefficients", ScriptData.Coefficients(result.Coefficients)),
            ("ols", ScriptData.Coefficients(result.OrdinaryLeastSquares)),
            ("first_stage", ScriptData.Table(result.FirstStages,
                ("name", s => s.Variable),
                ("f_stat", s => s.FStatistic),
                ("p", s => s.PValue),
                ("partial_r2", s => s.PartialRSquared),
                ("weak", s => s.IsWeak))),
            ("weak_instruments", result.HasWeakInstruments),
            ("overid_stat", result.OveridentificationStatistic),
            ("overid_p", result.OveridentificationPValue),
            ("overid_restrictions", result.OveridentifyingRestrictions),
            ("hausman_stat", result.HausmanStatistic),
            ("hausman_p", result.HausmanPValue),
            ("observations", result.Observations),
            ("residuals", result.Residuals));
    }

    [ScriptFn("panel", "Панельная регрессия: фиксированные и случайные эффекты, разности",
        Example = "let m = regress.panel(data, \"invest\", [\"value\"], \"firm\", \"year\", kind: \"random\")")]
    public static ScriptRecord Panel(
        [ScriptParam("таблица данных: строка — объект в периоде")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("колонка объекта: число или строка")] string unit,
        [ScriptParam("колонка номера периода")] string period,
        [ScriptParam("оценка: fixed, random, twoway, pooled, first_diff, between")] string kind = "fixed",
        [ScriptParam("ошибки, кластеризованные по объектам")] bool robust = true)
    {
        const string function = "regress.panel";

        PanelEstimator estimator = ScriptData.Kind<PanelEstimator>(kind, "kind", function,
            ("fixed", PanelEstimator.FixedEffects),
            ("random", PanelEstimator.RandomEffects),
            ("twoway", PanelEstimator.TwoWayFixedEffects),
            ("pooled", PanelEstimator.Pooled),
            ("first_diff", PanelEstimator.FirstDifference),
            ("between", PanelEstimator.BetweenEffects));

        PanelResult result = PanelData.Fit(
            ScriptData.Panel(data, y, x, unit, period, function), estimator, robust);

        return ScriptData.Record(result,
            ("kind", kind),
            ("coefficients", ScriptData.Coefficients(result.Coefficients)),
            ("r2", result.RSquared),
            ("units", result.Units),
            ("periods", result.Periods),
            ("observations", result.Observations),
            ("sigma_unit", result.SigmaUnit),
            ("sigma_error", result.SigmaError),
            ("rho", result.Rho),
            ("theta", result.Theta),
            ("residuals", result.Residuals));
    }

    /// <summary>
    /// Тест Хаусмана: фиксированные эффекты против случайных.
    /// </summary>
    /// <remarks>
    /// Обе модели оцениваются здесь же и с обычными, а не кластерными ошибками: классическая
    /// статистика опирается на то, что случайные эффекты эффективны при нулевой гипотезе, и с
    /// устойчивыми ковариациями разность матриц перестаёт быть положительно определённой.
    /// </remarks>
    [ScriptFn("hausman", "Тест Хаусмана: выбрать фиксированные или случайные эффекты",
        Example = "let h = regress.hausman(data, \"invest\", [\"value\"], \"firm\", \"year\")")]
    public static ScriptRecord Hausman(
        [ScriptParam("таблица данных: строка — объект в периоде")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("колонка объекта: число или строка")] string unit,
        [ScriptParam("колонка номера периода")] string period)
    {
        PanelDataset dataset = ScriptData.Panel(data, y, x, unit, period, "regress.hausman");

        HausmanResult result = PanelData.Hausman(
            PanelData.Fit(dataset, PanelEstimator.FixedEffects, clusterByUnit: false),
            PanelData.Fit(dataset, PanelEstimator.RandomEffects, clusterByUnit: false));

        return ScriptData.Record(result,
            ("stat", result.Statistic),
            ("df", result.DegreesOfFreedom),
            ("p", result.PValue),
            ("prefers_fixed", result.PrefersFixedEffects),
            ("defined", result.IsDefined),
            ("excluded", result.Excluded),
            ("differences", ScriptData.Table(result.Differences,
                ("name", d => d.Variable),
                ("fixed", d => d.Fixed),
                ("random", d => d.Random),
                ("difference", d => d.Difference))));
    }

    /// <summary>
    /// Динамическая панель: оценка Ареллано — Бонда.
    /// </summary>
    /// <remarks>
    /// Когда отклик зависит от собственного прошлого — продажи, занятость, долг, — фиксированные
    /// эффекты смещают коэффициент при лаге вниз, и тем сильнее, чем короче панель. Здесь лаг
    /// инструментируется более глубокими лагами, а тесты Саргана и AR(2) показывают, законно ли это.
    /// </remarks>
    [ScriptFn("dynamic_panel", "Динамическая панель Ареллано — Бонда: отклик зависит от своего прошлого",
        Example = "let m = regress.dynamic_panel(data, \"sales\", [\"price\"], \"store\", \"month\")")]
    public static ScriptRecord Dynamic(
        [ScriptParam("таблица данных: строка — объект в периоде")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("колонка объекта: число или строка")] string unit,
        [ScriptParam("колонка номера периода")] string period,
        [ScriptParam("глубина лагов-инструментов")] int max_lags = 3)
    {
        const string function = "regress.dynamic_panel";

        ScriptData.Require(max_lags >= 1, $"{function}: глубина лагов — хотя бы один");

        DynamicPanelResult result = DynamicPanel.ArellanoBond(
            ScriptData.Panel(data, y, x, unit, period, function), max_lags);

        return ScriptData.Record(result,
            ("coefficients", ScriptData.Coefficients(result.Coefficients)),
            ("persistence", result.Persistence),
            ("pooled_persistence", result.PooledPersistence),
            ("within_persistence", result.WithinPersistence),
            ("in_bounds", result.IsInBounds),
            ("long_run", result.LongRunMultiplier),
            ("sargan_stat", result.SarganStatistic),
            ("sargan_p", result.SarganPValue),
            ("ar2_stat", result.ArellanoBondAr2),
            ("ar2_p", result.Ar2PValue),
            ("instruments", result.Instruments),
            ("units", result.Units),
            ("observations", result.Observations));
    }

    /// <summary>
    /// Проверка допущений линейной регрессии.
    /// </summary>
    /// <remarks>
    /// Коэффициенты МНК считаются всегда, а верить им можно не всегда. Тесты здесь отвечают на
    /// вопрос «какие из выводов <c>regress.ols</c> нельзя принимать как есть» и в колонке
    /// <c>consequence</c> говорят, что с этим делать.
    /// </remarks>
    [ScriptFn("diagnose", "Проверка допущений МНК: гетероскедастичность, автокорреляция, мультиколлинеарность",
        Example = "let d = regress.diagnose(data, \"wage\", [\"educ\", \"exper\"])")]
    public static ScriptRecord Diagnose(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка отклика")] string y,
        [ScriptParam("колонки регрессоров")] string[] x,
        [ScriptParam("строка начала второго режима для теста Чоу; 0 — середина")] int break_at = 0)
    {
        const string function = "regress.diagnose";

        ScriptData.Require(break_at >= 0, $"{function}: строка разрыва не может быть отрицательной");

        DiagnosticReport report = AI.Econometrics.Diagnostics.Run(
            ScriptData.Columns(data, x, function), ScriptData.Column(data, y, function),
            x, break_at > 0 ? break_at : null);

        return ScriptData.Record(report,
            ("tests", ScriptData.Table(report.Tests,
                ("name", t => t.Name),
                ("null_hypothesis", t => t.NullHypothesis),
                ("stat", t => t.Statistic),
                ("p", t => t.PValue),
                ("rejected", t => t.Rejected),
                ("consequence", t => t.Consequence))),
            ("failed", report.Failed.Select(t => t.Name).ToArray()),
            ("vif", ScriptData.Table(report.Collinearity,
                ("name", v => v.Variable),
                ("vif", v => v.Vif),
                ("r2", v => v.RSquared),
                ("severe", v => v.IsSevere))),
            ("durbin_watson", report.DurbinWatson),
            ("observations", report.Observations));
    }

    // --- внутреннее ---

    /// <summary>Общая часть результата моделей с ограниченным откликом.</summary>
    private static ScriptRecord Limited(
        LimitedDependentResult result, string kind, params (string Name, object Value)[] specific)
    {
        (string Name, object Value)[] common =
        [
            ("kind", kind),
            ("coefficients", ScriptData.Coefficients(result.Coefficients)),
            ("marginal_effects", ScriptData.Table(result.MarginalEffects,
                ("name", e => e.Variable),
                ("effect", e => e.Effect))),
            ("log_likelihood", result.LogLikelihood),
            ("pseudo_r2", result.McFaddenRSquared),
            ("aic", result.Aic),
            ("bic", result.Bic),
            ("converged", result.Converged),
            ("observations", result.Observations),
            ("fitted", result.Fitted),
        ];

        return ScriptData.Record(result, [.. common, .. specific]);
    }
}
