using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Нормировщик признаков с запомненными параметрами.
/// </summary>
/// <remarks>
/// Отдельный объект с состоянием, а не функция: параметры нормировки считаются по обучающей
/// выборке и применяются к тестовой. Посчитать их заново по тесту значит подсмотреть в него —
/// метрика после этого выглядит лучше, чем модель на самом деле работает.
/// </remarks>
public sealed class FeatureScaler
{
    private readonly Vector _center;
    private readonly Vector _scale;

    /// <summary>Вид нормировки: <c>zscore</c> либо <c>minmax</c>.</summary>
    public string Kind { get; }

    /// <summary>Число признаков.</summary>
    public int Features => _center.Count;

    /// <summary>Смещение по каждому признаку.</summary>
    public Vector Center => _center;

    /// <summary>Масштаб по каждому признаку.</summary>
    public Vector Scale => _scale;

    /// <summary>Создаёт нормировщик с готовыми параметрами.</summary>
    public FeatureScaler(string kind, Vector center, Vector scale)
    {
        Kind = kind;
        _center = center;
        _scale = scale;
    }

    /// <summary>Применяет нормировку.</summary>
    public Matrix Apply(Matrix data)
    {
        if (data.Width != _center.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"нормировщик обучен на {_center.Count} признаках, а данных {data.Width}",
                "признаки обучающей и применяемой выборок должны совпадать по составу и порядку");
        }

        var result = new Matrix(data.Height, data.Width);

        for (int i = 0; i < data.Height; i++)
        {
            for (int j = 0; j < data.Width; j++)
                result[i, j] = _scale[j] == 0 ? 0 : (data[i, j] - _center[j]) / _scale[j];
        }

        return result;
    }

    /// <summary>Обращает нормировку.</summary>
    public Matrix Undo(Matrix data)
    {
        var result = new Matrix(data.Height, data.Width);

        for (int i = 0; i < data.Height; i++)
        {
            for (int j = 0; j < data.Width && j < _center.Count; j++)
                result[i, j] = (data[i, j] * _scale[j]) + _center[j];
        }

        return result;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Kind}, признаков: {Features}";
}

/// <summary>
/// Пространство <c>prep</c>: подготовка признаков с запоминанием параметров.
/// </summary>
/// <remarks>
/// Отличие от <c>mat.zscore</c> принципиальное, а не стилистическое: <c>mat.zscore</c>
/// нормирует матрицу по ней самой и годится для разведки, а <c>prep.zscore</c> запоминает
/// параметры обучающей выборки и применяет их к тестовой. Второе — единственный честный
/// способ измерить качество модели.
/// </remarks>
[ScriptModule("prep", "Подготовка признаков: нормировка с запомненными параметрами, аугментация", Version = "0.1")]
public static class PrepModule
{
    /// <summary>Тип-тег дескриптора нормировщика.</summary>
    public const string ScalerHandle = "prep.scaler";

    [ScriptFn("zscore", "Обучает z-нормировку по выборке", Returns = ScalerHandle,
        Example = "let s = prep.zscore(x_train)\nlet z = s.apply(x_test)")]
    public static ScriptHandle ZScore([ScriptParam("обучающая матрица объект × признак")] Matrix data)
    {
        _ = Datasets.RequireNotEmpty(data, "prep.zscore");

        var center = new Vector(data.Width);
        var scale = new Vector(data.Width);

        for (int j = 0; j < data.Width; j++)
        {
            Vector column = ColumnOf(data, j);

            center[j] = column.Mean();
            scale[j] = column.Std();
        }

        var scaler = new FeatureScaler("z-нормировка", center, scale);

        return new ScriptHandle(ScalerHandle, scaler, scaler.ToString());
    }

    [ScriptFn("minmax", "Обучает нормировку на отрезок [0, 1]", Returns = ScalerHandle,
        Example = "let s = prep.minmax(x_train)")]
    public static ScriptHandle MinMax([ScriptParam("обучающая матрица объект × признак")] Matrix data)
    {
        _ = Datasets.RequireNotEmpty(data, "prep.minmax");

        var center = new Vector(data.Width);
        var scale = new Vector(data.Width);

        for (int j = 0; j < data.Width; j++)
        {
            Vector column = ColumnOf(data, j);

            center[j] = column.Min();
            scale[j] = column.Max() - column.Min();
        }

        var scaler = new FeatureScaler("нормировка на [0, 1]", center, scale);

        return new ScriptHandle(ScalerHandle, scaler, scaler.ToString());
    }

    [ScriptFn("apply", "Применяет запомненную нормировку", Example = "s.apply(x_test)")]
    [ScriptMethod(ScalerHandle)]
    public static Matrix Apply(
        [ScriptParam("нормировщик")] ScriptHandle scaler,
        [ScriptParam("матрица объект × признак")] Matrix data)
        => ((FeatureScaler)scaler.Target).Apply(data);

    [ScriptFn("undo", "Возвращает данные в исходный масштаб", Example = "s.undo(z)")]
    [ScriptMethod(ScalerHandle)]
    public static Matrix Undo(
        [ScriptParam("нормировщик")] ScriptHandle scaler,
        [ScriptParam("нормированная матрица")] Matrix data)
        => ((FeatureScaler)scaler.Target).Undo(data);

    [ScriptFn("params", "Параметры нормировки: смещение и масштаб", Example = "s.params().center")]
    [ScriptMethod(ScalerHandle)]
    public static ScriptRecord Parameters([ScriptParam("нормировщик")] ScriptHandle scaler)
    {
        var model = (FeatureScaler)scaler.Target;

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("kind", ScriptValue.Str(model.Kind)),
            new KeyValuePair<string, ScriptValue>("center", ScriptValue.Vec(model.Center)),
            new KeyValuePair<string, ScriptValue>("scale", ScriptValue.Vec(model.Scale)),
        ]);
    }

    /// <summary>
    /// Дополняет выборку копиями объектов с гауссовым шумом.
    /// </summary>
    /// <remarks>
    /// Шум берётся из ГСЧ прогона: дополненная выборка обязана повторяться от запуска к
    /// запуску, иначе метрики двух прогонов несравнимы.
    /// </remarks>
    [ScriptFn("augment", "Дополняет выборку зашумлёнными копиями", Example = "prep.augment(x, times: 2, sigma: 0.05)")]
    public static ScriptRecord Augment(
        IScriptContext context,
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки либо отклик")] Vector labels,
        [ScriptParam("сколько копий добавить")] int times = 1,
        [ScriptParam("уровень шума в долях СКО признака")] double sigma = 0.05)
    {
        _ = Datasets.RequireNotEmpty(data, "prep.augment");
        Datasets.RequireSameLength(data, labels, "prep.augment");

        if (times < 0) throw new ScriptError(DiagnosticCodes.BadOperand, "prep.augment: число копий отрицательно");

        int total = data.Height * (times + 1);
        context.CountAllocation((long)total * data.Width);

        var deviations = new Vector(data.Width);

        for (int j = 0; j < data.Width; j++) deviations[j] = ColumnOf(data, j).Std() * sigma;

        var result = new Matrix(total, data.Width);
        var targets = new Vector(total);

        for (int copy = 0; copy <= times; copy++)
        {
            for (int i = 0; i < data.Height; i++)
            {
                int row = (copy * data.Height) + i;

                for (int j = 0; j < data.Width; j++)
                    result[row, j] = copy == 0 ? data[i, j] : data[i, j] + Gauss(context, deviations[j]);

                targets[row] = labels[i];
            }
        }

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("x", ScriptValue.Mat(result)),
            new KeyValuePair<string, ScriptValue>("y", ScriptValue.Vec(targets)),
        ]);
    }

    [ScriptFn("polynomial", "Добавляет степени признаков", Example = "prep.polynomial(x, degree: 2)")]
    public static Matrix Polynomial(
        IScriptContext context,
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("максимальная степень")] int degree = 2)
    {
        _ = Datasets.RequireNotEmpty(data, "prep.polynomial");

        if (degree < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "prep.polynomial: степень должна быть не меньше 1");

        context.CountAllocation((long)data.Height * data.Width * degree);

        var result = new Matrix(data.Height, data.Width * degree);

        for (int i = 0; i < data.Height; i++)
        {
            for (int power = 1; power <= degree; power++)
            {
                for (int j = 0; j < data.Width; j++)
                    result[i, ((power - 1) * data.Width) + j] = Math.Pow(data[i, j], power);
            }
        }

        return result;
    }

    private static double Gauss(IScriptContext context, double sigma)
    {
        if (sigma <= 0) return 0;

        double u1 = 1.0 - context.Random.NextDouble();
        double u2 = context.Random.NextDouble();

        return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private static Vector ColumnOf(Matrix data, int index)
    {
        var column = new Vector(data.Height);

        for (int i = 0; i < data.Height; i++) column[i] = data[i, index];

        return column;
    }
}
