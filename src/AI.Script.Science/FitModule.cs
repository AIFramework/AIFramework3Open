using AI.DataStructs.Algebraic;
using AI.MathUtils.Nonlinear;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Std;

namespace AI.Script.Science;

/// <summary>
/// Пространство <c>fit</c>: подгонка нелинейной модели к измерениям.
/// </summary>
/// <remarks>
/// Модель — функция скрипта <c>(x, p) =&gt; ...</c>, где <c>p</c> — запись параметров по именам:
/// <c>p.a * math.exp(-p.b * x)</c> читается так же, как формула в отчёте. Параметры ищет метод
/// Левенберга — Марквардта из <c>AI.ClassicMath</c>. Линейные по параметрам модели с выводом о
/// значимости — в <c>regress.ols</c>, многочлен одной переменной — <c>ml.polyreg</c>.
/// </remarks>
[ScriptModule("fit", "Подгонка нелинейной модели к данным: параметры, ошибки, R²", Version = "0.1", Group = "анализ")]
public static class FitModule
{
    /// <summary>
    /// Нелинейный метод наименьших квадратов.
    /// </summary>
    /// <remarks>
    /// Начальное приближение обязательно и важно: у нелинейной задачи бывает несколько минимумов,
    /// и метод находит ближайший. Стандартные ошибки — из якобиана в решении,
    /// <c>s²·(JᵀJ)⁻¹</c>: они верны при независимых ошибках измерения одного масштаба. Если данные
    /// не определяют какой-то параметр (якобиан неполного ранга), его ошибка — <c>nan</c>, а
    /// причина — в <c>warnings</c>.
    /// </remarks>
    [ScriptFn("curve", "Подгонка кривой y = f(x, p) методом Левенберга — Марквардта",
        Example = "let r = fit.curve((x, p) => p.a * math.exp(-p.b * x), <0, 1, 2, 3, 4>, <5, 3, 1.8, 1.1, 0.7>, { a: 1, b: 0.1 })")]
    public static ScriptRecord Curve(
        IScriptContext context,
        [ScriptParam("модель: (x, p) => значение, p — запись параметров")] ScriptCallable model,
        [ScriptParam("значения аргумента")] Vector x,
        [ScriptParam("измеренные значения")] Vector y,
        [ScriptParam("начальное приближение: запись «параметр: значение»")] ScriptRecord start,
        [ScriptParam("предел числа итераций")] int max_iterations = 500)
    {
        const string function = "fit.curve";

        ScriptData.Require(start.Count > 0, $"{function}: нет ни одного параметра в начальном приближении");
        ScriptData.Require(x.Count == y.Count, $"{function}: значений x {x.Count}, а y {y.Count}");
        ScriptData.Require(x.Count >= start.Count, $"{function}: точек {x.Count} меньше, чем параметров {start.Count}");
        ScriptData.Require(max_iterations >= 1, $"{function}: итераций — хотя бы одна");

        IReadOnlyList<string> names = start.Keys;
        var initial = new Vector(names.Count);

        for (int j = 0; j < names.Count; j++) initial[j] = start.Values[j].AsNumber($"{function}: начальное значение '{names[j]}'");

        Vector Predict(Vector parameters)
        {
            ScriptValue record = ScriptValue.Record(ScriptData.Numbers(names, parameters));
            var predicted = new Vector(x.Count);

            for (int i = 0; i < x.Count; i++)
            {
                predicted[i] = ScriptCallbacks.Invoke(context, model, ScriptValue.Num(x[i]), record)
                    .AsNumber($"{function}: значение модели");
            }

            return predicted;
        }

        LeastSquaresResult result = LevenbergMarquardt.Solve(
            parameters => Predict(parameters) - y,
            initial,
            options: new LevenbergMarquardtOptions { MaxIterations = max_iterations });

        Vector fitted = Predict(result.Solution);
        int rank = result.JacobianRank();
        int freedom = x.Count - names.Count;
        double rmse = Math.Sqrt(result.Cost / x.Count);
        double r2 = 1 - (result.Cost / TotalSquares(y));
        Vector errors = StandardErrors(result.Jacobian, result.Cost, freedom, rank == names.Count);

        return ScriptData.Plain(
            ("params", ScriptData.Numbers(names, result.Solution)),
            ("errors", ScriptData.Numbers(names, errors)),
            ("rmse", rmse),
            ("r2", r2),
            ("predicted", fitted),
            ("residuals", y - fitted),
            ("converged", result.Converged),
            ("iterations", result.Iterations),
            ("summary", $"Подогнано параметров {names.Count} по {x.Count} точкам: R² = {ScriptFormatter.Number(Math.Round(r2, 4))}, "
                + $"RMSE = {ScriptFormatter.Number(rmse)}; "
                + string.Join(", ", names.Select((name, j) => $"{name} = {ScriptFormatter.Number(result.Solution[j])}")) + "."),
            ("warnings", Warnings(result.Converged, rank, names.Count, freedom, r2)));
    }

    // --- внутреннее ---

    private static double TotalSquares(Vector y)
    {
        double mean = y.Mean();
        double total = 0;

        for (int i = 0; i < y.Count; i++) total += (y[i] - mean) * (y[i] - mean);

        return total;
    }

    /// <summary>Стандартные ошибки параметров: корни диагонали <c>s²·(JᵀJ)⁻¹</c>.</summary>
    private static Vector StandardErrors(Matrix jacobian, double cost, int freedom, bool fullRank)
    {
        var errors = new Vector(jacobian.Width);

        if (!fullRank || freedom <= 0)
        {
            for (int j = 0; j < errors.Count; j++) errors[j] = double.NaN;

            return errors;
        }

        Matrix covariance = (jacobian.Transpose() * jacobian).GetInvertMatrix() * (cost / freedom);

        for (int j = 0; j < errors.Count; j++) errors[j] = Math.Sqrt(Math.Max(covariance[j, j], 0));

        return errors;
    }

    private static IReadOnlyList<string> Warnings(bool converged, int rank, int parameters, int freedom, double r2)
    {
        var warnings = new List<string>();

        if (!converged)
        {
            warnings.Add("Метод не сошёлся за отведённые итерации: параметры — последнее приближение, а не минимум. "
                + "Начните с другого приближения либо увеличьте max_iterations.");
        }

        if (rank < parameters)
        {
            warnings.Add($"Данные определяют только {rank} из {parameters} параметров: часть из них взаимозаменяема "
                + "(например, два множителя при одном слагаемом). Их ошибки — nan; упростите модель.");
        }

        if (freedom == 0)
            warnings.Add("Точек ровно столько, сколько параметров: кривая проходит через все точки, и оценить разброс нельзя.");

        if (r2 < 0.5)
        {
            warnings.Add($"R² = {ScriptFormatter.Number(Math.Round(r2, 3))}: модель объясняет меньше половины разброса. "
                + "Либо форма модели не та, либо метод остановился в побочном минимуме — проверьте начальное приближение.");
        }

        return warnings;
    }
}
