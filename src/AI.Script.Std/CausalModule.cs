using AI.DataStructs.Algebraic;
using AI.Econometrics;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>causal</c>: оценка причинного эффекта по наблюдательным данным.
/// </summary>
/// <remarks>
/// Каждая функция — отдельный способ заменить эксперимент, которого не было, и у каждого своё
/// допущение: параллельные тренды у разности разностей, отсутствие манипуляции порогом у
/// разрыва регрессии, наблюдаемость всех смешивающих факторов у сопоставления. Проверки этих
/// допущений возвращаются рядом с эффектом — <c>pre_trend_p</c>, <c>density_p</c>,
/// <c>balance</c>, — потому что эффект без них выглядит одинаково убедительно и когда верен,
/// и когда нет.
/// <para>
/// Воздействие кодируется нулями и единицами. Бутстреп и случайный лес берут зерно из
/// <c>options.seed</c>, и повторный прогон даёт тот же ответ.
/// </para>
/// </remarks>
[ScriptModule("causal", "Причинный эффект: DiD, RDD, сопоставление", Version = "0.1", Group = "анализ")]
public static class CausalModule
{
    /// <summary>
    /// Разность разностей со ступенчатым началом воздействия.
    /// </summary>
    /// <remarks>
    /// Возвращаются две оценки: устойчивая к разному времени начала (<c>att</c>) и классическая
    /// двусторонняя (<c>att_twfe</c>). При одинаковом для всех эффекте они совпадают. Когда же
    /// эффект меняется со временем, а объекты начинают в разные периоды, вторая смешивает
    /// эффекты с отрицательными весами, и расхождение двух — <c>estimator_gap</c> — прямо
    /// показывает, насколько.
    /// </remarks>
    [ScriptFn("did", "Разность разностей: эффект воздействия со ступенчатым началом",
        Example = "let effect = causal.did(panel, unit: \"region\", period: \"year\", outcome: \"sales\", first_treated: \"start\")")]
    public static ScriptRecord Did(
        IScriptContext context,
        [ScriptParam("таблица: строка — объект в периоде")] ScriptTable data,
        [ScriptParam("колонка объекта: число или строка")] string unit,
        [ScriptParam("колонка номера периода")] string period,
        [ScriptParam("колонка отклика")] string outcome,
        [ScriptParam("колонка периода начала воздействия; 0 — не получал")] string first_treated,
        [ScriptParam("повторов бутстрепа для ошибок")] int bootstrap = 200)
    {
        const string function = "causal.did";

        ScriptData.Require(bootstrap >= 10, $"{function}: повторов бутстрепа нужно хотя бы десять");

        int[] units = ScriptData.Codes(data, unit, function);
        int[] periods = ScriptData.Integers(data, period, function);
        int[] starts = ScriptData.Integers(data, first_treated, function);
        Vector values = ScriptData.Column(data, outcome, function);

        var observations = new DidObservation[data.RowCount];

        for (int i = 0; i < observations.Length; i++)
            observations[i] = new DidObservation(units[i], periods[i], values[i], starts[i]);

        DidResult result = DifferenceInDifferences.Estimate(observations, bootstrap, context.Random.Next());

        return ScriptData.Record(result,
            ("att", result.RobustAtt),
            ("std_error", result.RobustStandardError),
            ("p", result.PValue),
            ("att_twfe", result.TwoWayFixedEffects),
            ("std_error_twfe", result.TwoWayStandardError),
            ("estimator_gap", result.EstimatorGap),
            ("pre_trend_p", result.PreTrendPValue),
            ("event_study", ScriptData.Table(result.EventStudy,
                ("period", e => e.RelativePeriod),
                ("estimate", e => e.Estimate),
                ("std_error", e => e.StandardError),
                ("ci_low", e => e.ConfidenceLow),
                ("ci_high", e => e.ConfidenceHigh),
                ("observations", e => e.Observations))),
            ("cohorts", result.Cohorts),
            ("treated", result.Treated),
            ("never_treated", result.NeverTreated),
            ("observations", result.Observations));
    }

    /// <summary>
    /// Разрыв регрессии.
    /// </summary>
    /// <remarks>
    /// Эффект — скачок отклика на пороге назначения. Рядом возвращаются чувствительность к
    /// ширине окна, плацебо-пороги и тест плотности: если по разные стороны порога наблюдений
    /// подозрительно разное число, порогом манипулировали, и скачок ничего не доказывает.
    /// </remarks>
    [ScriptFn("rdd", "Разрыв регрессии (RDD): скачок отклика на пороге назначения",
        Example = "let effect = causal.rdd(students, \"score\", \"income\", cutoff: 60)")]
    public static ScriptRecord Rdd(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка переменной назначения")] string running,
        [ScriptParam("колонка отклика")] string outcome,
        [ScriptParam("порог назначения")] double cutoff = 0,
        [ScriptParam("ширина окна; 0 — по эмпирическому правилу")] double bandwidth = 0)
    {
        const string function = "causal.rdd";

        ScriptData.Require(bandwidth >= 0, $"{function}: ширина окна не может быть отрицательной");

        Vector assignment = ScriptData.Column(data, running, function);
        Vector values = ScriptData.Column(data, outcome, function);

        var observations = new RddObservation[assignment.Count];

        for (int i = 0; i < observations.Length; i++) observations[i] = new RddObservation(assignment[i], values[i]);

        RddResult result = RegressionDiscontinuity.Estimate(observations, cutoff, bandwidth);

        return ScriptData.Record(result,
            ("effect", result.Effect),
            ("std_error", result.StandardError),
            ("p", result.PValue),
            ("ci_low", result.ConfidenceLow),
            ("ci_high", result.ConfidenceHigh),
            ("cutoff", result.Cutoff),
            ("bandwidth", result.Bandwidth),
            ("left_limit", result.LeftLimit),
            ("right_limit", result.RightLimit),
            ("left_observations", result.LeftObservations),
            ("right_observations", result.RightObservations),
            ("density_stat", result.DensityStatistic),
            ("density_p", result.DensityPValue),
            ("sensitivity", ScriptData.Table(result.Sensitivity,
                ("bandwidth", s => s.Bandwidth),
                ("effect", s => s.Effect),
                ("std_error", s => s.StandardError))),
            ("placebo", ScriptData.Table(result.Placebo,
                ("cutoff", s => s.Cutoff),
                ("effect", s => s.Effect),
                ("std_error", s => s.StandardError))));
    }

    /// <summary>
    /// Сопоставление по склонности к воздействию.
    /// </summary>
    /// <remarks>
    /// Таблица <c>balance</c> — главное в результате, а не приложение к нему: эффект честен,
    /// только если после сопоставления группы стали похожи по каждому признаку. Колонка
    /// <c>balanced</c> ставит порог 0.1 стандартизованной разности, принятый в литературе.
    /// </remarks>
    [ScriptFn("matching", "Сопоставление по склонности: эффект на получивших воздействие",
        Example = "let effect = causal.matching(clients, \"promo\", \"revenue\", [\"age\", \"tenure\"])")]
    public static ScriptRecord Matching(
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка воздействия из нулей и единиц")] string treatment,
        [ScriptParam("колонка отклика")] string outcome,
        [ScriptParam("колонки признаков, влияющих на воздействие")] string[] covariates,
        [ScriptParam("калипер в долях стандартного отклонения склонности")] double caliper = 0.2,
        [ScriptParam("сколько соседей брать в пару")] int neighbors = 1)
    {
        const string function = "causal.matching";

        ScriptData.Require(caliper > 0, $"{function}: калипер должен быть положительным");
        ScriptData.Require(neighbors >= 1, $"{function}: соседей — хотя бы один");

        MatchingResult result = PropensityScoreMatching.Estimate(
            ScriptData.Columns(data, covariates, function),
            ScriptData.Binary(data, treatment, function),
            ScriptData.Column(data, outcome, function),
            covariates, caliper, neighbors);

        return ScriptData.Record(result,
            ("att", result.AverageTreatmentEffectOnTreated),
            ("std_error", result.StandardError),
            ("p", result.PValue),
            ("naive", result.NaiveDifference),
            ("treated", result.Treated),
            ("matched", result.Matched),
            ("controls", result.Controls),
            ("common_support", result.CommonSupport),
            ("caliper", result.Caliper),
            ("balance", ScriptData.Table(result.Balance,
                ("name", b => b.Variable),
                ("treated_mean", b => b.BeforeTreated),
                ("control_before", b => b.BeforeControl),
                ("control_after", b => b.AfterControl),
                ("smd_before", b => b.StandardizedBefore),
                ("smd_after", b => b.StandardizedAfter),
                ("balanced", b => b.IsBalanced))));
    }

    /// <summary>
    /// Синтетический контроль.
    /// </summary>
    /// <remarks>
    /// Для случая, когда воздействие получил один объект — регион, страна, магазин, — и
    /// сравнивать его не с чем. Контрольный объект собирается взвешенной смесью остальных так,
    /// чтобы до вмешательства повторять обработанный; расхождение после — эффект. Значимость
    /// проверяется плацебо: тот же приём на каждом доноре.
    /// </remarks>
    [ScriptFn("synthetic", "Синтетический контроль: эффект для единственного объекта",
        Example = "let effect = causal.synthetic(sales, \"moscow\", 24)")]
    public static ScriptRecord Synthetic(
        [ScriptParam("таблица: колонка — объект, строка — период")] ScriptTable data,
        [ScriptParam("колонка объекта, получившего воздействие")] string treated,
        [ScriptParam("номер строки, с которой действует вмешательство")] int start,
        [ScriptParam("колонки-доноры; по умолчанию все остальные")] string[]? donors = null)
    {
        const string function = "causal.synthetic";

        string[] pool = donors is { Length: > 0 }
            ? donors
            : [.. data.Names().Where(name => name != treated)];

        ScriptData.Require(!pool.Contains(treated), $"{function}: объект '{treated}' не может быть своим донором");
        ScriptData.Require(pool.Length >= 2, $"{function}: доноров нужно хотя бы два");

        SyntheticControlResult result = SyntheticControl.Build(
            ScriptData.Column(data, treated, function),
            ScriptData.Columns(data, pool, function),
            pool, start, treated);

        return ScriptData.Record(result,
            ("effect", result.AverageEffect),
            ("p", result.PValue),
            ("weights", ScriptData.Table(result.Weights,
                ("donor", w => w.Donor),
                ("weight", w => w.Weight))),
            ("active_donors", result.ActiveDonors),
            ("actual", result.Actual),
            ("synthetic", result.Synthetic),
            ("gap", result.Gap),
            ("pre_rmspe", result.PreTreatmentRmspe),
            ("post_rmspe", result.PostTreatmentRmspe),
            ("rmspe_ratio", result.RmspeRatio),
            ("placebo", ScriptData.Table(result.Placebo,
                ("donor", p => p.Donor),
                ("ratio", p => p.Ratio))));
    }

    /// <summary>
    /// Причинный лес.
    /// </summary>
    /// <remarks>
    /// Отвечает не «каков эффект», а «у кого он какой»: эффект на каждом объекте и признаки,
    /// от которых он зависит. Поле <c>calibration</c> — наклон фактического эффекта по
    /// предсказанному; далёкий от единицы, он значит, что различиям между объектами верить нельзя.
    /// </remarks>
    [ScriptFn("forest", "Причинный лес: неоднородный эффект по признакам объекта",
        Example = "let effect = causal.forest(clients, \"promo\", \"revenue\", [\"age\", \"tenure\"], trees: 300)")]
    public static ScriptRecord Forest(
        IScriptContext context,
        [ScriptParam("таблица данных")] ScriptTable data,
        [ScriptParam("колонка воздействия из нулей и единиц")] string treatment,
        [ScriptParam("колонка отклика")] string outcome,
        [ScriptParam("колонки признаков")] string[] features,
        [ScriptParam("число деревьев")] int trees = 200,
        [ScriptParam("наименьший размер листа")] int min_leaf = 10,
        [ScriptParam("наибольшая глубина дерева")] int max_depth = 4)
    {
        const string function = "causal.forest";

        ScriptData.Require(trees >= 1, $"{function}: деревьев — хотя бы одно");
        ScriptData.Require(min_leaf >= 1, $"{function}: лист — хотя бы из одного объекта");
        ScriptData.Require(max_depth >= 1, $"{function}: глубина — хотя бы один уровень");

        Matrix x = ScriptData.Columns(data, features, function);

        context.CountAllocation((long)trees * x.Height);

        CausalForestResult result = CausalForest.Fit(
            x,
            ScriptData.Binary(data, treatment, function),
            ScriptData.Column(data, outcome, function),
            features, trees, min_leaf, max_depth, context.Random.Next());

        return ScriptData.Record(result,
            ("ate", result.AverageEffect),
            ("spread", result.EffectSpread),
            ("heterogeneous", result.HasHeterogeneity),
            ("calibration", result.CalibrationSlope),
            ("effects", result.Effects),
            ("groups", ScriptData.Table(result.Groups,
                ("group", g => g.Group),
                ("predicted", g => g.PredictedEffect),
                ("actual", g => g.ActualEffect),
                ("size", g => g.Size))),
            ("importance", ScriptData.Table(result.Importance,
                ("name", i => i.Variable),
                ("importance", i => i.Importance))),
            ("trees", result.Trees),
            ("observations", result.Observations));
    }
}
