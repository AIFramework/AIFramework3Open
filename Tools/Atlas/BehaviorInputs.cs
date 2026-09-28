using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.Statistics;
using Complex = System.Numerics.Complex;

namespace AiFramework.Tools.Atlas;

/// <summary>Род значения: то, что генератор умеет порождать, а сравнение — сравнивать.</summary>
public enum ValueKind
{
    /// <summary>Не поддерживается: метод не исполняется.</summary>
    None,
    /// <summary>Вещественное число: <c>double</c>, <c>float</c>.</summary>
    Real,
    /// <summary>Целое число: <c>int</c>, <c>long</c>.</summary>
    Integer,
    /// <summary>Логическое значение.</summary>
    Flag,
    /// <summary>Строка.</summary>
    Text,
    /// <summary>Ряд чисел: <c>double[]</c>, <c>float[]</c>, <c>decimal[]</c>, <see cref="Vector"/>, списки и перечисления <c>double</c>.</summary>
    Series,
    /// <summary>Ряд целых от 0 до 9: <c>int[]</c>, <c>long[]</c>, <c>short[]</c>, <c>byte[]</c>, списки <c>int</c>.</summary>
    Counts,
    /// <summary>Таблица чисел: <c>double[,]</c>, <see cref="Matrix"/>.</summary>
    Table,
    /// <summary>Комплексное число.</summary>
    ComplexNumber,
    /// <summary>Ряд комплексных чисел: <c>Complex[]</c>, <see cref="ComplexVector"/>.</summary>
    ComplexSeries,
}

/// <summary>Набор входов одного вызова.</summary>
/// <param name="Values">Значения в общем виде: <c>double</c>, <c>long</c>, <c>bool</c>, <c>string</c>, <c>double[]</c>, <c>long[]</c>, <c>double[,]</c>, <c>Complex</c>, <c>Complex[]</c>.</param>
/// <param name="Size">Общая длина рядов и сторона таблиц: от неё зависят поправки вида n/(n−1).</param>
/// <param name="Edge">Крайний случай: пустой ряд, одно значение, константа, NaN, огромные величины.</param>
/// <param name="Label">Что это за случай, для таблицы расхождений.</param>
public sealed record InputCase(object[] Values, int Size, bool Edge, string Label);

/// <summary>
/// Генератор входов по сигнатуре: сначала крайние случаи, затем случайные с сидом. Один сид —
/// одни и те же входы.
/// </summary>
/// <remarks>
/// Все ряды одного вызова одной длины, все таблицы квадратные той же стороны: так методы от
/// двух векторов или матрицы и вектора получают согласованные входы, а не падают на
/// размерностях. Половина случайных случаев положительна: логарифмы, корни и гамма-функции
/// иначе проверялись бы в основном вне своей области. Целые берутся от 1 до длины ряда:
/// обычно это размер окна, порядок или число элементов.
/// </remarks>
public static class BehaviorInputs
{
    private static readonly string[] Words = ["сигнал", "signal", "фильтр", "filter", "Матрица", "vector", "ёж", "x1", "", "  ", "a-b", "Привет, мир!"];

    private static readonly (int Size, string Label, Func<Random, double> Value)[] EdgeCases =
    [
        (0, "пусто", _ => 0),
        (1, "одно значение", _ => 1),
        (2, "два значения", rng => rng.NextDouble() + 0.5),
        (5, "константа", _ => 3),
        (5, "есть NaN", rng => rng.NextDouble()),
        (5, "огромные величины", rng => 1e8 * (rng.NextDouble() + 0.5)),
        (5, "малые отрицательные", rng => -1e-6 * (rng.NextDouble() + 0.5)),
    ];

    private static readonly int[] Sizes = [3, 4, 5, 7, 10, 16, 31];

    /// <summary>Род значения для типа параметра или результата.</summary>
    public static ValueKind KindOf(Type type)
    {
        if (type == typeof(double) || type == typeof(float)) return ValueKind.Real;
        if (type == typeof(int) || type == typeof(long)) return ValueKind.Integer;
        if (type == typeof(bool)) return ValueKind.Flag;
        if (type == typeof(string)) return ValueKind.Text;
        if (type == typeof(Complex)) return ValueKind.ComplexNumber;
        if (type == typeof(double[,]) || type == typeof(Matrix)) return ValueKind.Table;
        if (type == typeof(Complex[]) || type == typeof(ComplexVector)) return ValueKind.ComplexSeries;
        if (type == typeof(int[]) || type == typeof(long[]) || type == typeof(short[]) || type == typeof(byte[]) || type == typeof(List<int>)
            || type == typeof(IList<int>) || type == typeof(IReadOnlyList<int>) || type == typeof(IEnumerable<int>)) return ValueKind.Counts;
        if (type == typeof(double[]) || type == typeof(float[]) || type == typeof(decimal[]) || type == typeof(Vector) || type == typeof(List<double>)
            || type == typeof(IList<double>) || type == typeof(IReadOnlyList<double>) || type == typeof(IEnumerable<double>)
            || type == typeof(ICollection<double>) || type == typeof(IReadOnlyCollection<double>)) return ValueKind.Series;

        return ValueKind.None;
    }

    /// <summary>Входы для параметров данных родов; <paramref name="count"/> включает крайние случаи.</summary>
    public static IReadOnlyList<InputCase> Cases(IReadOnlyList<ValueKind> kinds, int seed, int count)
    {
        var cases = new List<InputCase>(count);

        for (int i = 0; i < count; i++)
        {
            var rng = new Random(unchecked(seed * 7919 + i));
            if (i < EdgeCases.Length)
            {
                (int size, string label, Func<Random, double> value) = EdgeCases[i];
                cases.Add(Case(kinds, rng, size, label, edge: true, _ => value(rng), withNaN: label == "есть NaN"));
            }
            else
            {
                int size = Sizes[rng.Next(Sizes.Length)];
                bool positive = i % 2 == 0;
                Vector normal = Statistic.RandNorm(Math.Max(size * size, 1) * Math.Max(kinds.Count, 1) + 4, rng);
                int next = 0;
                cases.Add(Case(kinds, rng, size, positive ? "случайные положительные" : "случайные", edge: false,
                    _ => positive ? Math.Abs(normal[next++]) + 0.05 : normal[next++], withNaN: false));
            }
        }

        return cases;
    }

    /// <summary>Значение общего вида в тип параметра; каждый вызов получает свою копию.</summary>
    public static object? Convert(object value, Type type) => value switch
    {
        double x when type == typeof(float) => (float)x,
        double x => x,
        long x when type == typeof(int) => (int)x,
        long x => x,
        double[] x when type == typeof(Vector) => new Vector((double[])x.Clone()),
        double[] x when type == typeof(float[]) => x.Select(v => (float)v).ToArray(),
        double[] x when type == typeof(decimal[]) => x.Select(v => double.IsFinite(v) && Math.Abs(v) < 1e20 ? (decimal)v : 0m).ToArray(),
        double[] x when type == typeof(List<double>) || type == typeof(IList<double>) || type == typeof(ICollection<double>) => new List<double>(x),
        double[] x => (double[])x.Clone(),
        long[] x when type == typeof(int[]) => x.Select(v => (int)v).ToArray(),
        long[] x when type == typeof(short[]) => x.Select(v => (short)v).ToArray(),
        long[] x when type == typeof(byte[]) => x.Select(v => (byte)v).ToArray(),
        long[] x when type == typeof(List<int>) || type == typeof(IList<int>) || type == typeof(IReadOnlyList<int>) || type == typeof(IEnumerable<int>) => x.Select(v => (int)v).ToList(),
        long[] x => (long[])x.Clone(),
        double[,] x when type == typeof(Matrix) => new Matrix((double[,])x.Clone()),
        double[,] x => (double[,])x.Clone(),
        Complex[] x when type == typeof(ComplexVector) => new ComplexVector(x),
        Complex[] x => (Complex[])x.Clone(),
        _ => value,
    };

    /// <summary>Короткая запись входа для таблицы расхождений.</summary>
    public static string Describe(InputCase input) =>
        $"{input.Label}, n={input.Size}: " + string.Join("; ", input.Values.Select(Short));

    private static InputCase Case(IReadOnlyList<ValueKind> kinds, Random rng, int size, string label, bool edge, Func<int, double> value, bool withNaN)
    {
        var values = new object[kinds.Count];
        int seriesIndex = 0;

        for (int k = 0; k < kinds.Count; k++)
        {
            values[k] = kinds[k] switch
            {
                ValueKind.Real => size == 0 ? 0.0 : value(k),
                ValueKind.Integer => edge ? (long)size : rng.Next(1, size + 1),
                ValueKind.Flag => rng.Next(2) == 1,
                ValueKind.Text => edge && size == 0 ? "" : string.Join(' ', Enumerable.Range(0, Math.Max(size, 1)).Select(_ => Words[rng.Next(Words.Length)])),
                ValueKind.Series => Series(size, value, withNaN && seriesIndex++ == 0),
                ValueKind.Counts => Enumerable.Range(0, size).Select(_ => (long)rng.Next(0, 10)).ToArray(),
                ValueKind.Table => Table(size, value),
                ValueKind.ComplexNumber => new Complex(value(k), value(k)),
                ValueKind.ComplexSeries => Enumerable.Range(0, size).Select(_ => new Complex(value(k), value(k))).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(kinds), kinds[k], "род не поддерживается"),
            };
        }

        return new InputCase(values, size, edge, label);
    }

    private static double[] Series(int size, Func<int, double> value, bool withNaN)
    {
        double[] series = [.. Enumerable.Range(0, size).Select(value)];
        if (withNaN && size > 2) series[2] = double.NaN;
        return series;
    }

    private static double[,] Table(int size, Func<int, double> value)
    {
        var table = new double[size, size];
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                table[i, j] = value(i * size + j);
        return table;
    }

    private static string Short(object value) => value switch
    {
        double x => x.ToString("G4", System.Globalization.CultureInfo.InvariantCulture),
        double[] x => "[" + string.Join(", ", x.Take(4).Select(v => v.ToString("G3", System.Globalization.CultureInfo.InvariantCulture))) + (x.Length > 4 ? ", …" : "") + "]",
        long[] x => "[" + string.Join(", ", x.Take(6)) + (x.Length > 6 ? ", …" : "") + "]",
        double[,] x => $"матрица {x.GetLength(0)}×{x.GetLength(1)}",
        Complex[] x => $"комплексный ряд из {x.Length}",
        string x => "«" + (x.Length > 30 ? x[..30] + "…" : x) + "»",
        _ => value.ToString() ?? "",
    };
}
