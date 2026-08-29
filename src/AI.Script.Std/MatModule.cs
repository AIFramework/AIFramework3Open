using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>mat</c>: матрицы и линейная алгебра.
/// </summary>
/// <remarks>
/// Оператор <c>*</c> для двух матриц — матричное умножение (как в математике и в самом
/// фреймворке); поэлементное произведение даёт <see cref="Hadamard"/>. Для векторов <c>*</c>,
/// наоборот, поэлементное: вектор — колонка данных, матрица — линейный оператор.
/// </remarks>
[ScriptModule("mat", "Матрицы: создание, преобразования, разложения, решение систем", Version = "0.1")]
public static class MatModule
{
    [ScriptFn("zeros", "Матрица из нулей", Example = "mat.zeros(3, cols: 4)")]
    public static Matrix Zeros(
        IScriptContext context,
        [ScriptParam("число строк")] int rows,
        [ScriptParam("число столбцов; по умолчанию равно числу строк")] int cols = 0)
    {
        int width = cols <= 0 ? rows : cols;
        Guard(context, rows, width);

        return new Matrix(rows, width);
    }

    [ScriptFn("full", "Матрица из одинаковых значений", Example = "mat.full(2, cols: 3, value: 1)")]
    public static Matrix Full(
        IScriptContext context,
        [ScriptParam("число строк")] int rows,
        [ScriptParam("число столбцов")] int cols = 0,
        [ScriptParam("значение")] double value = 0)
    {
        int width = cols <= 0 ? rows : cols;
        Guard(context, rows, width);

        var matrix = new Matrix(rows, width);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < width; j++) matrix[i, j] = value;
        }

        return matrix;
    }

    [ScriptFn("eye", "Единичная матрица", Example = "mat.eye(3)")]
    public static Matrix Eye(IScriptContext context, [ScriptParam("размер")] int n)
    {
        Guard(context, n, n);
        return Matrix.Identity(n);
    }

    [ScriptFn("of", "Матрица из списка строк-векторов", Example = "mat.of([<1, 2>, <3, 4>])")]
    public static Matrix Of([ScriptParam("список векторов-строк")] ScriptList rows) => FromRows(rows);

    [ScriptFn("from_rows", "Матрица из векторов-строк", Example = "mat.from_rows([<1, 2>, <3, 4>])")]
    public static Matrix FromRows([ScriptParam("список векторов")] ScriptList rows)
    {
        var vectors = ToVectors(rows, "mat.from_rows");
        return Matrix.FromVectorsAsRows(vectors);
    }

    [ScriptFn("from_cols", "Матрица из векторов-столбцов", Example = "mat.from_cols([<1, 2>, <3, 4>])")]
    public static Matrix FromColumns([ScriptParam("список векторов")] ScriptList cols)
    {
        var vectors = ToVectors(cols, "mat.from_cols");
        return Matrix.FromVectorsAsColumns(vectors);
    }

    [ScriptFn("shape", "Размеры матрицы", Example = "mat.shape(m).rows")]
    public static ScriptRecord Shape([ScriptParam("матрица")] Matrix m) => ScriptRecord.From(
    [
        new KeyValuePair<string, ScriptValue>("rows", ScriptValue.Num(m.Height)),
        new KeyValuePair<string, ScriptValue>("cols", ScriptValue.Num(m.Width)),
    ]);

    [ScriptFn("rows", "Число строк", Example = "mat.rows(m)")]
    public static double Rows([ScriptParam("матрица")] Matrix m) => m.Height;

    [ScriptFn("cols", "Число столбцов", Example = "mat.cols(m)")]
    public static double Cols([ScriptParam("матрица")] Matrix m) => m.Width;

    [ScriptFn("row", "Строка матрицы как вектор", Example = "mat.row(m, 0)")]
    public static Vector Row(
        [ScriptParam("матрица")] Matrix m,
        [ScriptParam("номер строки")] int index)
    {
        int row = Normalize(index, m.Height, "строк");
        var result = new Vector(m.Width);

        for (int j = 0; j < m.Width; j++) result[j] = m[row, j];

        return result;
    }

    [ScriptFn("col", "Столбец матрицы как вектор", Example = "mat.col(m, 0)")]
    public static Vector Column(
        [ScriptParam("матрица")] Matrix m,
        [ScriptParam("номер столбца")] int index)
    {
        int column = Normalize(index, m.Width, "столбцов");
        var result = new Vector(m.Height);

        for (int i = 0; i < m.Height; i++) result[i] = m[i, column];

        return result;
    }

    [ScriptFn("transpose", "Транспонирование", Example = "m |> mat.transpose()")]
    public static Matrix Transpose([ScriptParam("матрица")] Matrix m) => m.Transpose();

    [ScriptFn("mul", "Матричное умножение (то же, что оператор *)", Example = "mat.mul(a, b)")]
    public static Matrix Multiply(
        [ScriptParam("левая матрица")] Matrix a,
        [ScriptParam("правая матрица")] Matrix b)
    {
        if (a.Width != b.Height)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"матрицы {a.Height}×{a.Width} и {b.Height}×{b.Width} не перемножаются");
        }

        return a * b;
    }

    [ScriptFn("hadamard", "Поэлементное произведение", Example = "mat.hadamard(a, b)")]
    public static Matrix Hadamard(
        [ScriptParam("первая матрица")] Matrix a,
        [ScriptParam("вторая матрица")] Matrix b)
    {
        RequireSameShape(a, b, "mat.hadamard");
        return a.AdamarProduct(b);
    }

    [ScriptFn("det", "Определитель", Example = "mat.det(m)")]
    public static double Determinant([ScriptParam("квадратная матрица")] Matrix m)
    {
        RequireSquare(m, "mat.det");
        return LU.Determinant(m);
    }

    [ScriptFn("inv", "Обратная матрица", Example = "mat.inv(m)")]
    public static Matrix Invert([ScriptParam("квадратная невырожденная матрица")] Matrix m)
    {
        RequireSquare(m, "mat.inv");
        return m.GetInvertMatrix();
    }

    [ScriptFn("pinv", "Псевдообратная матрица (Мура — Пенроуза)", Example = "mat.pinv(m)")]
    public static Matrix Pinv(
        [ScriptParam("матрица")] Matrix m,
        [ScriptParam("порог отсечения сингулярных чисел")] double tolerance = 1e-10)
        => Pseudoinverse.Compute(m, tolerance);

    [ScriptFn("solve", "Решает систему A·x = b", Example = "mat.solve(a, b)")]
    public static Vector Solve(
        [ScriptParam("матрица системы")] Matrix a,
        [ScriptParam("вектор правых частей")] Vector b)
    {
        RequireSquare(a, "mat.solve");

        if (a.Height != b.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"mat.solve: матрица {a.Height}×{a.Width} и правая часть длиной {b.Count}");
        }

        return LU.Solve(a, b);
    }

    [ScriptFn("lu", "LU-разложение с перестановками", Example = "mat.lu(m).l")]
    public static ScriptRecord Lu([ScriptParam("квадратная матрица")] Matrix m)
    {
        RequireSquare(m, "mat.lu");
        (Matrix l, Matrix u, int[] perm) = LU.Decompose(m);

        var order = new Vector(perm.Length);
        for (int i = 0; i < perm.Length; i++) order[i] = perm[i];

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("l", ScriptValue.Mat(l)),
            new KeyValuePair<string, ScriptValue>("u", ScriptValue.Mat(u)),
            new KeyValuePair<string, ScriptValue>("perm", ScriptValue.Vec(order)),
        ]);
    }

    [ScriptFn("qr", "QR-разложение", Example = "mat.qr(m).q")]
    public static ScriptRecord Qr([ScriptParam("матрица")] Matrix m)
    {
        Matrix q = QR.GetQ(m);
        Matrix r = QR.GetR(m, q);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("q", ScriptValue.Mat(q)),
            new KeyValuePair<string, ScriptValue>("r", ScriptValue.Mat(r)),
        ]);
    }

    [ScriptFn("cholesky", "Разложение Холецкого симметричной положительно определённой матрицы", Example = "mat.cholesky(m)")]
    public static Matrix CholeskyDecompose([ScriptParam("матрица")] Matrix m)
    {
        RequireSquare(m, "mat.cholesky");
        return Cholesky.Decompose(m);
    }

    [ScriptFn("svd", "Сингулярное разложение", Example = "mat.svd(m).sigma")]
    public static ScriptRecord SvdDecompose([ScriptParam("матрица")] Matrix m)
    {
        (Matrix u, Vector sigma, Matrix v) = Svd.DecomposeVector(m);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("u", ScriptValue.Mat(u)),
            new KeyValuePair<string, ScriptValue>("sigma", ScriptValue.Vec(sigma)),
            new KeyValuePair<string, ScriptValue>("v", ScriptValue.Mat(v)),
        ]);
    }

    [ScriptFn("eig", "Собственные числа и векторы симметричной матрицы (метод Якоби)", Example = "mat.eig(m).values")]
    public static ScriptRecord Eig([ScriptParam("симметричная матрица")] Matrix m)
    {
        RequireSquare(m, "mat.eig");
        (Vector values, Matrix vectors) = JacobiEigen.ComputeVector(m);

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("values", ScriptValue.Vec(values)),
            new KeyValuePair<string, ScriptValue>("vectors", ScriptValue.Mat(vectors)),
        ]);
    }

    [ScriptFn("concat_rows", "Склеивает матрицы сверху вниз", Example = "mat.concat_rows(a, b)")]
    public static Matrix ConcatRows(
        [ScriptParam("верхняя матрица")] Matrix a,
        [ScriptParam("нижняя матрица")] Matrix b)
    {
        if (a.Width != b.Width)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"mat.concat_rows: разное число столбцов ({a.Width} и {b.Width})");
        }

        var result = new Matrix(a.Height + b.Height, a.Width);

        for (int i = 0; i < a.Height; i++)
        {
            for (int j = 0; j < a.Width; j++) result[i, j] = a[i, j];
        }

        for (int i = 0; i < b.Height; i++)
        {
            for (int j = 0; j < b.Width; j++) result[a.Height + i, j] = b[i, j];
        }

        return result;
    }

    [ScriptFn("concat_cols", "Склеивает матрицы слева направо", Example = "mat.concat_cols(a, b)")]
    public static Matrix ConcatColumns(
        [ScriptParam("левая матрица")] Matrix a,
        [ScriptParam("правая матрица")] Matrix b)
    {
        if (a.Height != b.Height)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"mat.concat_cols: разное число строк ({a.Height} и {b.Height})");
        }

        var result = new Matrix(a.Height, a.Width + b.Width);

        for (int i = 0; i < a.Height; i++)
        {
            for (int j = 0; j < a.Width; j++) result[i, j] = a[i, j];
            for (int j = 0; j < b.Width; j++) result[i, a.Width + j] = b[i, j];
        }

        return result;
    }

    [ScriptFn("mean", "Среднее по столбцам", Example = "mat.mean(m)")]
    public static Vector Mean([ScriptParam("матрица")] Matrix m) => Reduce(m, values => values.Mean());

    [ScriptFn("std", "Среднеквадратичное отклонение по столбцам", Example = "mat.std(m)")]
    public static Vector Std([ScriptParam("матрица")] Matrix m) => Reduce(m, values => values.Std());

    [ScriptFn("sum", "Сумма по столбцам", Example = "mat.sum(m)")]
    public static Vector Sum([ScriptParam("матрица")] Matrix m) => Reduce(m, values => values.Sum());

    [ScriptFn("min", "Минимум по столбцам", Example = "mat.min(m)")]
    public static Vector Min([ScriptParam("матрица")] Matrix m) => Reduce(m, values => values.Min());

    [ScriptFn("max", "Максимум по столбцам", Example = "mat.max(m)")]
    public static Vector Max([ScriptParam("матрица")] Matrix m) => Reduce(m, values => values.Max());

    /// <summary>
    /// Постолбцовая z-нормировка.
    /// </summary>
    /// <remarks>
    /// По столбцам, а не по всей матрице: столбец — это признак, и нормировать признаки нужно
    /// каждый в своей шкале, иначе крупный по величине признак подавит остальные.
    /// </remarks>
    [ScriptFn("zscore", "Постолбцовая z-нормировка", Example = "m |> mat.zscore()")]
    public static Matrix ZScore([ScriptParam("матрица")] Matrix m)
    {
        var result = new Matrix(m.Height, m.Width);

        for (int j = 0; j < m.Width; j++)
        {
            Vector column = ColumnOf(m, j);
            double mean = column.Mean();
            double std = column.Std();

            for (int i = 0; i < m.Height; i++)
                result[i, j] = std == 0 ? m[i, j] - mean : (m[i, j] - mean) / std;
        }

        return result;
    }

    [ScriptFn("minmax", "Постолбцовая нормировка на отрезок [0, 1]", Example = "m |> mat.minmax()")]
    public static Matrix MinMax([ScriptParam("матрица")] Matrix m)
    {
        var result = new Matrix(m.Height, m.Width);

        for (int j = 0; j < m.Width; j++)
        {
            Vector column = ColumnOf(m, j);
            double min = column.Min();
            double span = column.Max() - min;

            for (int i = 0; i < m.Height; i++)
                result[i, j] = span == 0 ? 0 : (m[i, j] - min) / span;
        }

        return result;
    }

    [ScriptFn("map", "Применяет функцию к каждой ячейке матрицы", Example = "m |> mat.map(x => x * 2)")]
    public static async Task<ScriptValue> Map(
        IScriptContext context,
        [ScriptParam("матрица")] Matrix m,
        [ScriptParam("функция одного аргумента")] ScriptCallable transform)
    {
        ScriptValue callable = ScriptValue.Fn(transform);
        var result = new Matrix(m.Height, m.Width);

        for (int i = 0; i < m.Height; i++)
        {
            context.Cancellation.ThrowIfCancellationRequested();

            for (int j = 0; j < m.Width; j++)
            {
                ScriptValue value = await context
                    .CallAsync(callable, ScriptValue.Num(m[i, j]))
                    .ConfigureAwait(false);

                result[i, j] = value.AsNumber("результат функции mat.map");
            }
        }

        return ScriptValue.Mat(result);
    }

    private static Vector Reduce(Matrix m, Func<Vector, double> reduce)
    {
        var result = new Vector(m.Width);

        for (int j = 0; j < m.Width; j++) result[j] = reduce(ColumnOf(m, j));

        return result;
    }

    private static Vector ColumnOf(Matrix m, int index)
    {
        var column = new Vector(m.Height);

        for (int i = 0; i < m.Height; i++) column[i] = m[i, index];

        return column;
    }

    private static List<Vector> ToVectors(ScriptList items, string what)
    {
        var vectors = new List<Vector>(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            object? converted = Marshaller.ToClr(items[i], typeof(Vector), $"{what}: элемент {i}");
            vectors.Add((Vector)converted!);
        }

        if (vectors.Count == 0) throw new ScriptError(DiagnosticCodes.SizeMismatch, $"{what}: список пуст");

        foreach (Vector vector in vectors)
        {
            if (vector.Count == vectors[0].Count) continue;

            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"{what}: векторы разной длины ({vectors[0].Count} и {vector.Count})");
        }

        return vectors;
    }

    private static void Guard(IScriptContext context, int rows, int cols)
    {
        if (rows < 0 || cols < 0) throw new ScriptError(DiagnosticCodes.BadOperand, "размер матрицы отрицателен");

        context.CountAllocation((long)rows * cols);
    }

    private static void RequireSquare(Matrix m, string what)
    {
        if (m.Height == m.Width) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: нужна квадратная матрица, а здесь {m.Height}×{m.Width}");
    }

    private static void RequireSameShape(Matrix a, Matrix b, string what)
    {
        if (a.Height == b.Height && a.Width == b.Width) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: несовместимые размеры {a.Height}×{a.Width} и {b.Height}×{b.Width}");
    }

    private static int Normalize(int index, int length, string what)
    {
        int result = index < 0 ? index + length : index;

        if (result < 0 || result >= length)
        {
            throw new ScriptError(
                DiagnosticCodes.IndexOutOfRange,
                $"индекс {index} вне границ: {what} всего {length}");
        }

        return result;
    }
}
