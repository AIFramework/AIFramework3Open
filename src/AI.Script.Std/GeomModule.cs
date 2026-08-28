using AI.DataStructs.Algebraic;
using AI.Geometry.Curves;
using AI.Geometry.Fitting;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>geom</c>: геометрические преобразования, подгонка, кривые.
/// </summary>
/// <remarks>
/// Точки передаются матрицей «точка × координата» — тем же видом, что и выборка объектов в
/// <c>ml</c>. Один вид данных на всю библиотеку означает, что результат <c>table.to_matrix</c>
/// годится и туда, и туда без переупаковки.
/// </remarks>
[ScriptModule("geom", "Геометрия: аффинные преобразования, подгонка прямых и окружностей, кривые", Version = "0.1")]
public static class GeomModule
{
    [ScriptFn("translate", "Матрица переноса на плоскости", Example = "geom.translate(dx: 2, dy: 3)")]
    public static Matrix Translate(
        [ScriptParam("сдвиг по X")] double dx,
        [ScriptParam("сдвиг по Y")] double dy)
        => Affine2D.Translation(dx, dy).M;

    [ScriptFn("scale", "Матрица масштабирования на плоскости", Example = "geom.scale(sx: 2, sy: 0.5)")]
    public static Matrix Scale(
        [ScriptParam("масштаб по X")] double sx,
        [ScriptParam("масштаб по Y")] double sy)
        => Affine2D.Scale(sx, sy).M;

    [ScriptFn("rotate", "Матрица поворота на плоскости", Example = "geom.rotate(math.radians(30))")]
    public static Matrix Rotate([ScriptParam("угол в радианах")] double angle) => Affine2D.Rotation(angle).M;

    /// <summary>
    /// Применяет аффинное преобразование к набору точек.
    /// </summary>
    /// <remarks>
    /// Точки дополняются однородной координатой внутри: скрипт работает с обычными
    /// двумерными точками, а не с тройками, где третья всегда единица.
    /// </remarks>
    [ScriptFn("apply", "Применяет аффинное преобразование к точкам",
        Example = "geom.apply(points, transform: geom.rotate(0.5))")]
    public static Matrix Apply(
        [ScriptParam("матрица точка × координата")] Matrix points,
        [ScriptParam("матрица преобразования 3×3")] Matrix transform)
    {
        if (points.Width != 2)
            throw new ScriptError(DiagnosticCodes.SizeMismatch, $"geom.apply: ожидались двумерные точки, а координат {points.Width}");

        if (transform.Height != 3 || transform.Width != 3)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"geom.apply: преобразование должно быть 3×3, а оно {transform.Height}×{transform.Width}");
        }

        var affine = new Affine2D(transform);
        var result = new Matrix(points.Height, 2);

        for (int i = 0; i < points.Height; i++)
        {
            Vector moved = affine.Apply(new Vector(points[i, 0], points[i, 1]));

            result[i, 0] = moved[0];
            result[i, 1] = moved[1];
        }

        return result;
    }

    [ScriptFn("compose", "Композиция двух преобразований", Example = "geom.compose(a, b)")]
    public static Matrix Compose(
        [ScriptParam("первое преобразование")] Matrix first,
        [ScriptParam("второе преобразование")] Matrix second)
        => new Affine2D(first).Compose(new Affine2D(second)).M;

    [ScriptFn("fit_line", "Подгонка прямой методом наименьших квадратов",
        Example = "geom.fit_line(points).slope")]
    public static ScriptRecord FitLine([ScriptParam("матрица точка × координата")] Matrix points)
    {
        Vector[] rows = RequirePoints(points, "geom.fit_line", minimum: 2);
        (double slope, double intercept) = LineFit.Ols(rows);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("slope", ScriptValue.Num(slope)),
            new KeyValuePair<string, ScriptValue>("intercept", ScriptValue.Num(intercept)),
        ]);
    }

    /// <summary>
    /// Подгонка прямой, устойчивая к выбросам.
    /// </summary>
    /// <remarks>
    /// RANSAC случаен по природе, поэтому число проб задаётся явно: без него результат
    /// зависел бы от невидимой константы, а прогоны переставали бы совпадать.
    /// </remarks>
    [ScriptFn("fit_line_robust", "Подгонка прямой методом RANSAC: устойчива к выбросам",
        Example = "geom.fit_line_robust(points, threshold: 0.5)")]
    public static ScriptRecord FitLineRobust(
        [ScriptParam("матрица точка × координата")] Matrix points,
        [ScriptParam("допуск отклонения точки от прямой")] double threshold = 0.5,
        [ScriptParam("число проб")] int trials = 200)
    {
        Vector[] rows = RequirePoints(points, "geom.fit_line_robust", minimum: 2);
        (double slope, double intercept, bool[] inliers) = LineFit.Ransac(rows, trials, threshold, new Random(42));

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("slope", ScriptValue.Num(slope)),
            new KeyValuePair<string, ScriptValue>("intercept", ScriptValue.Num(intercept)),
            new KeyValuePair<string, ScriptValue>("inliers", ScriptValue.Vec(Flags(inliers))),
        ]);
    }

    [ScriptFn("fit_circle", "Подгонка окружности по точкам", Example = "geom.fit_circle(points).radius")]
    public static ScriptRecord FitCircle([ScriptParam("матрица точка × координата")] Matrix points)
    {
        Vector[] rows = RequirePoints(points, "geom.fit_circle", minimum: 3);
        Circle circle = CircleFit.AlgebraicFit(rows);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("x", ScriptValue.Num(circle.Center[0])),
            new KeyValuePair<string, ScriptValue>("y", ScriptValue.Num(circle.Center[1])),
            new KeyValuePair<string, ScriptValue>("radius", ScriptValue.Num(circle.Radius)),
        ]);
    }

    [ScriptFn("bezier", "Точки кривой Безье по опорным точкам",
        Example = "geom.bezier(control, points: 100)")]
    public static Matrix Bezier(
        IScriptContext context,
        [ScriptParam("матрица опорных точек")] Matrix control,
        [ScriptParam("сколько точек вернуть")] int points = 50)
    {
        Vector[] rows = RequirePoints(control, "geom.bezier", minimum: 2);

        if (points < 2) throw new ScriptError(DiagnosticCodes.BadOperand, "geom.bezier: нужно хотя бы две точки");

        context.CountAllocation((long)points * control.Width);

        Vector[] sampled = new BezierCurve(rows).Sample(points);

        return Datasets.FromRows(sampled);
    }

    [ScriptFn("distance", "Евклидово расстояние между точками", Example = "geom.distance(<0, 0>, <3, 4>)")]
    public static double Distance(
        [ScriptParam("первая точка")] Vector a,
        [ScriptParam("вторая точка")] Vector b)
    {
        if (a.Count != b.Count)
            throw new ScriptError(DiagnosticCodes.SizeMismatch, $"geom.distance: размерности {a.Count} и {b.Count}");

        double sum = 0;

        for (int i = 0; i < a.Count; i++) sum += (a[i] - b[i]) * (a[i] - b[i]);

        return Math.Sqrt(sum);
    }

    [ScriptFn("centroid", "Центр масс набора точек", Example = "geom.centroid(points)")]
    public static Vector Centroid([ScriptParam("матрица точка × координата")] Matrix points)
    {
        _ = RequirePoints(points, "geom.centroid", minimum: 1);

        var center = new Vector(points.Width);

        for (int j = 0; j < points.Width; j++)
        {
            double sum = 0;

            for (int i = 0; i < points.Height; i++) sum += points[i, j];

            center[j] = sum / points.Height;
        }

        return center;
    }

    private static Vector[] RequirePoints(Matrix points, string what, int minimum)
    {
        if (points.Height < minimum)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"{what}: нужно хотя бы {minimum} точек, а их {points.Height}");
        }

        return Datasets.Rows(points);
    }

    private static Vector Flags(bool[] values)
    {
        var result = new Vector(values.Length);

        for (int i = 0; i < values.Length; i++) result[i] = values[i] ? 1 : 0;

        return result;
    }
}
