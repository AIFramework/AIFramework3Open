using AI.DataStructs.Algebraic;
using AI.Econometrics;
using AI.Script.Binding;
using AI.Script.Runtime;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>ts</c>: эконометрика временных рядов.
/// </summary>
/// <remarks>
/// Прогноз ряда живёт в <c>econ.forecast</c>, а здесь — то, без чего прогнозу и регрессии на
/// рядах нельзя верить: стационарность, волатильность, взаимное влияние рядов и их общий
/// тренд. Регрессия одного нестационарного ряда на другой почти всегда «значима», и
/// <c>ts.stationarity</c> — первое, что стоит сделать с рядом до всего остального.
/// <para>
/// Одномерный ряд — вектор; несколько рядов — таблица, где колонка — ряд, строка — момент
/// времени: имена колонок становятся именами переменных в результате.
/// </para>
/// </remarks>
[ScriptModule("ts", "Ряды: стационарность, GARCH, VAR, коинтеграция", Version = "0.1", Group = "прогноз")]
public static partial class TsModule
{
    /// <summary>
    /// Стационарность ряда.
    /// </summary>
    /// <remarks>
    /// Два теста с противоположными нулевыми гипотезами: ADF исходит из единичного корня, KPSS —
    /// из стационарности. Согласие обоих — вывод, расхождение — сигнал, что данных мало или
    /// ряд устроен сложнее; это и есть поле <c>verdict</c>.
    /// </remarks>
    [ScriptFn("stationarity", "Стационарность ряда: тесты ADF и KPSS, порядок интегрирования",
        Example = "let s = ts.stationarity(prices, trend: \"linear\")")]
    public static ScriptRecord Stationarity(
        [ScriptParam("временной ряд")] Vector series,
        [ScriptParam("детерминированная часть: constant, linear, none")] string trend = "constant",
        [ScriptParam("лаги теста; -1 — подобрать")] int lags = -1)
    {
        DeterministicTerms terms = ScriptData.Kind<DeterministicTerms>(trend, "trend", "ts.stationarity",
            ("constant", DeterministicTerms.Constant),
            ("linear", DeterministicTerms.ConstantAndTrend),
            ("none", DeterministicTerms.None));

        StationarityReport report = StationarityTests.Analyze(series, terms, lags);

        return ScriptData.Record(report,
            ("verdict", report.Verdict),
            ("order", report.IntegrationOrder),
            ("adf", UnitRoot(report.AugmentedDickeyFuller)),
            ("kpss", UnitRoot(report.Kpss)),
            ("observations", report.Observations));
    }

    /// <summary>
    /// Модель волатильности.
    /// </summary>
    /// <remarks>
    /// На вход — доходности, а не цены: GARCH описывает, как колеблется приращение, и на ценах
    /// найдёт «волатильность», которая есть просто тренд.
    /// </remarks>
    [ScriptFn("garch", "Волатильность доходностей: GARCH, GJR и EGARCH с прогнозом",
        Example = "let g = ts.garch(returns, kind: \"gjr\", horizon: 10)")]
    public static ScriptRecord VolatilityModel(
        [ScriptParam("доходности, не цены; не меньше 50")] Vector returns,
        [ScriptParam("модель: garch, gjr — асимметрия, egarch — логарифмическая")] string kind = "garch",
        [ScriptParam("горизонт прогноза волатильности")] int horizon = 20)
    {
        const string function = "ts.garch";

        GarchModel model = ScriptData.Kind<GarchModel>(kind, "kind", function,
            ("garch", GarchModel.Garch),
            ("gjr", GarchModel.GjrGarch),
            ("egarch", GarchModel.Egarch));

        ScriptData.Require(horizon >= 1, $"{function}: горизонт — хотя бы один шаг");

        GarchResult result = Garch.Fit(returns, model, horizon);

        return ScriptData.Record(result,
            ("kind", kind),
            ("omega", result.Omega),
            ("alpha", result.Alpha),
            ("beta", result.Beta),
            ("gamma", result.Gamma),
            ("mean", result.Mean),
            ("persistence", result.Persistence),
            ("stationary", result.IsStationary),
            ("half_life", result.HalfLife),
            ("long_run_volatility", result.LongRunVolatility),
            ("volatility", result.ConditionalVolatility),
            ("forecast", result.Forecast),
            ("std_residuals", result.StandardizedResiduals),
            ("arch_stat", result.ArchStatistic),
            ("arch_p", result.ArchPValue),
            ("aic", result.Aic),
            ("bic", result.Bic),
            ("log_likelihood", result.LogLikelihood),
            ("observations", result.Observations));
    }

    /// <summary>
    /// Векторная авторегрессия.
    /// </summary>
    /// <remarks>
    /// Импульсные отклики отдаются длинной таблицей <c>shock, response, step, value</c>, а не
    /// трёхмерным массивом: её можно отфильтровать по паре рядов и сразу нарисовать, а массив
    /// пришлось бы сначала разбирать, помня, какой индекс что значит.
    /// </remarks>
    [ScriptFn("var", "Векторная авторегрессия: причинность по Грейнджеру, импульсные отклики",
        Example = "let v = ts.var(macro, order: 2, horizon: 12)")]
    public static ScriptRecord Var(
        [ScriptParam("таблица: колонка — ряд, строка — момент времени")] ScriptTable data,
        [ScriptParam("порядок; 0 — подобрать по AIC до шести")] int order = 0,
        [ScriptParam("горизонт импульсных откликов и разложения дисперсии")] int horizon = 10,
        [ScriptParam("колонки-ряды; по умолчанию все")] string[]? columns = null)
    {
        const string function = "ts.var";

        ScriptData.Require(order >= 0, $"{function}: порядок не может быть отрицательным");
        ScriptData.Require(horizon >= 1, $"{function}: горизонт — хотя бы один шаг");

        string[] names = ScriptData.SeriesNames(data, columns);
        Matrix series = ScriptData.Columns(data, names, function);

        VarResult result = order == 0
            ? VectorAutoregression.SelectOrder(series, names: names)
            : VectorAutoregression.Fit(series, order, names);

        return ScriptData.Record(result,
            ("variables", names),
            ("order", result.Order),
            ("stable", result.IsStable),
            ("spectral_radius", result.SpectralRadius),
            ("granger", ScriptData.Table(result.Granger,
                ("from", g => g.From),
                ("to", g => g.To),
                ("f_stat", g => g.FStatistic),
                ("p", g => g.PValue),
                ("causes", g => g.Causes))),
            ("irf", ImpulseResponses(result, names, horizon)),
            ("fevd", Decomposition(result, names, horizon)),
            ("coefficients", result.Coefficients),
            ("residual_cov", result.ResidualCovariance),
            ("aic", result.Aic),
            ("bic", result.Bic),
            ("log_likelihood", result.LogLikelihood),
            ("observations", result.Observations));
    }

    /// <summary>
    /// Тест Йохансена на коинтеграцию.
    /// </summary>
    /// <remarks>
    /// Коинтегрированные ряды блуждают, но не расходятся: цена и себестоимость, курс и паритет.
    /// Регрессия таких рядов в разностях теряет именно то, что их связывает, — поэтому ранг
    /// коинтеграции нужно знать до выбора модели.
    /// </remarks>
    [ScriptFn("johansen", "Коинтеграция по Йохансену: сколько общих трендов у рядов",
        Example = "let j = ts.johansen(prices, lags: 2)")]
    public static ScriptRecord Johansen(
        [ScriptParam("таблица: колонка — ряд, до шести рядов")] ScriptTable data,
        [ScriptParam("лаги в разностях")] int lags = 1,
        [ScriptParam("колонки-ряды; по умолчанию все")] string[]? columns = null)
    {
        const string function = "ts.johansen";

        ScriptData.Require(lags >= 1, $"{function}: лагов — хотя бы один");

        string[] names = ScriptData.SeriesNames(data, columns);
        JohansenResult result = Cointegration.Johansen(ScriptData.Columns(data, names, function), lags, names);

        return ScriptData.Record(result,
            ("variables", names),
            ("rank", result.Rank),
            ("tests", ScriptData.Table(result.Rows,
                ("rank", r => r.Rank),
                ("eigenvalue", r => r.Eigenvalue),
                ("trace_stat", r => r.TraceStatistic),
                ("trace_critical", r => r.TraceCritical),
                ("max_eigen_stat", r => r.MaxEigenStatistic),
                ("max_eigen_critical", r => r.MaxEigenCritical))),
            ("vectors", result.CointegratingVectors),
            ("lags", result.Lags),
            ("observations", result.Observations));
    }

    [ScriptFn("vecm", "Модель коррекции ошибок: возврат коинтегрированных рядов к равновесию",
        Example = "let m = ts.vecm(prices, rank: 1)")]
    public static ScriptRecord Vecm(
        [ScriptParam("таблица: колонка — ряд, до шести рядов")] ScriptTable data,
        [ScriptParam("ранг коинтеграции; 0 — по тесту Йохансена")] int rank = 0,
        [ScriptParam("лаги в разностях")] int lags = 1,
        [ScriptParam("колонки-ряды; по умолчанию все")] string[]? columns = null)
    {
        const string function = "ts.vecm";

        ScriptData.Require(rank >= 0, $"{function}: ранг не может быть отрицательным");
        ScriptData.Require(lags >= 1, $"{function}: лагов — хотя бы один");

        string[] names = ScriptData.SeriesNames(data, columns);
        VecmResult result = Cointegration.ErrorCorrection(
            ScriptData.Columns(data, names, function), rank, lags, names);

        return ScriptData.Record(result,
            ("variables", names),
            ("rank", result.Rank),
            ("adjustment", result.Adjustment),
            ("cointegrating", result.Cointegrating),
            ("adjustment_coefficients", ScriptData.Coefficients(result.AdjustmentCoefficients)),
            ("equilibrium_error", result.EquilibriumError),
            ("half_life", result.HalfLife),
            ("lags", result.Lags),
            ("observations", result.Observations));
    }

    /// <summary>
    /// Структурная модель в пространстве состояний.
    /// </summary>
    /// <remarks>
    /// Разделяет ряд на уровень, наклон и шум — то, что скользящее среднее смешивает. Прогноз
    /// идёт с интервалом, который расширяется с горизонтом, а тест Льюнга — Бокса на инновациях
    /// говорит, осталось ли в остатках что-то неучтённое.
    /// </remarks>
    [ScriptFn("state_space", "Структурная модель: уровень и наклон ряда фильтром Калмана, прогноз",
        Example = "let s = ts.state_space(demand, kind: \"trend\", horizon: 6)")]
    public static ScriptRecord StructuralModel(
        [ScriptParam("временной ряд")] Vector series,
        [ScriptParam("модель: \"level\" — уровень, \"trend\" — уровень и наклон")] string kind = "level",
        [ScriptParam("горизонт прогноза")] int horizon = 12)
    {
        const string function = "ts.state_space";

        StateSpaceModel model = ScriptData.Kind<StateSpaceModel>(kind, "kind", function,
            ("level", StateSpaceModel.LocalLevel),
            ("trend", StateSpaceModel.LocalLinearTrend));

        ScriptData.Require(horizon >= 1, $"{function}: горизонт — хотя бы один шаг");

        StateSpaceResult result = StateSpace.Fit(series, model, horizon);

        return ScriptData.Record(result,
            ("kind", kind),
            ("level", result.Level),
            ("slope", result.Slope),
            ("filtered", result.FilteredLevel),
            ("innovations", result.Innovations),
            ("forecast", result.Forecast),
            ("lower", result.ForecastLower),
            ("upper", result.ForecastUpper),
            ("observation_var", result.ObservationVariance),
            ("level_var", result.LevelVariance),
            ("slope_var", result.SlopeVariance),
            ("signal_to_noise", result.SignalToNoise),
            ("ljung_box_stat", result.LjungBox),
            ("ljung_box_p", result.LjungBoxPValue),
            ("aic", result.Aic),
            ("log_likelihood", result.LogLikelihood),
            ("observations", result.Observations));
    }

    // --- внутреннее ---

    private static ScriptRecord UnitRoot(UnitRootTest test) => ScriptData.Plain(
        ("name", test.Name),
        ("null_hypothesis", test.NullHypothesis),
        ("stat", test.Statistic),
        ("critical_1", test.CriticalOnePercent),
        ("critical_5", test.CriticalFivePercent),
        ("critical_10", test.CriticalTenPercent),
        ("rejected", test.Rejected),
        ("lags", test.Lags));

    /// <summary>Импульсные отклики длинной таблицей: шок, отклик, шаг, значение.</summary>
    private static ScriptTable ImpulseResponses(VarResult result, IReadOnlyList<string> names, int horizon)
    {
        double[][][] responses = VectorAutoregression.ImpulseResponse(result, horizon);
        var rows = new List<(string Shock, string Response, int Step, double Value)>();

        for (int shock = 0; shock < responses.Length; shock++)
        {
            for (int variable = 0; variable < responses[shock].Length; variable++)
            {
                for (int step = 0; step < responses[shock][variable].Length; step++)
                    rows.Add((names[shock], names[variable], step, responses[shock][variable][step]));
            }
        }

        return ScriptData.Table(rows,
            ("shock", r => r.Shock),
            ("response", r => r.Response),
            ("step", r => r.Step),
            ("value", r => r.Value));
    }

    /// <summary>
    /// Разложение дисперсии таблицей: строка — переменная, колонка на каждый источник шока.
    /// </summary>
    private static ScriptTable Decomposition(VarResult result, IReadOnlyList<string> names, int horizon)
    {
        Matrix shares = VectorAutoregression.VarianceDecomposition(result, horizon);
        var columns = new List<ScriptColumn>(names.Count + 1)
        {
            ScriptColumn.From("variable", names.Select(ScriptValue.Str)),
        };

        for (int shock = 0; shock < names.Count; shock++)
        {
            var share = new Vector(names.Count);

            for (int variable = 0; variable < names.Count; variable++) share[variable] = shares[variable, shock];

            columns.Add(ScriptColumn.FromVector(names[shock], share));
        }

        return ScriptTable.Create(columns);
    }
}
