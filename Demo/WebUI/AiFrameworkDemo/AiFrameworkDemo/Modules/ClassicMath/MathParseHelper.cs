using System.Globalization;
using System.Text;
using AI.DataStructs.Algebraic;

namespace AiFrameworkDemo.Modules.ClassicMath;

public static class MathParseHelper
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

    public static Matrix ParseMatrix(string text, int maxDim = 12)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Матрица пуста.");

        // Поддерживаем оба формата:
        //   многострочный:  "1 2\n3 4"
        //   однострочный:   "1 2;3 4"  (TextDefault использует ';' как разделитель строк)
        var lines = text
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        if (lines.Count == 0)
            throw new FormatException("Нет строк матрицы.");

        if (lines.Count > maxDim)
            throw new FormatException($"Не более {maxDim} строк.");

        var rows = new List<double[]>();
        int? width = null;

        foreach (var line in lines)
        {
            var nums = SplitNumbers(line);
            if (nums.Length == 0)
                continue;

            if (width is null)
                width = nums.Length;
            else if (nums.Length != width)
                throw new FormatException("Все строки матрицы должны содержать одинаковое число элементов.");

            if (nums.Length > maxDim)
                throw new FormatException($"Не более {maxDim} столбцов.");

            rows.Add(nums);
        }

        if (rows.Count == 0 || width is null)
            throw new FormatException("Не удалось разобрать матрицу.");

        var m = new Matrix(rows.Count, width.Value);
        for (int i = 0; i < rows.Count; i++)
            for (int j = 0; j < width.Value; j++)
                m[i, j] = rows[i][j];

        return m;
    }

    public static Vector ParseVector(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Вектор пуст.");

        var nums = SplitNumbers(text.ReplaceLineEndings(" "));
        if (nums.Length == 0)
            throw new FormatException("Нет чисел во векторе.");

        var v = new Vector(nums.Length);
        for (int i = 0; i < nums.Length; i++)
            v[i] = nums[i];
        return v;
    }

    public static int[] ParseIntArray(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Список меток пуст.");

        var parts = text.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var a = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, Ci, out a[i]))
                throw new FormatException($"Не целое число: «{parts[i]}».");
        }
        return a;
    }

    public static string FormatMatrix(Matrix m, string? caption = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(caption))
            sb.AppendLine(caption);

        for (int i = 0; i < m.Height; i++)
        {
            for (int j = 0; j < m.Width; j++)
            {
                if (j > 0) sb.Append('\t');
                sb.Append(m[i, j].ToString("G8", Ci));
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    public static string FormatVector(Vector v, string? caption = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(caption))
            sb.AppendLine(caption);
        sb.Append('[');
        for (int i = 0; i < v.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(v[i].ToString("G8", Ci));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static double[] SplitNumbers(string line)
    {
        // ';' НЕ используется — он уже разобран как разделитель строк матрицы
        var tokens = line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
        var list = new List<double>(tokens.Length);
        foreach (var t in tokens)
        {
            if (double.TryParse(t.Trim(), NumberStyles.Float, Ci, out var x))
                list.Add(x);
            else
                throw new FormatException($"Не число: «{t}».");
        }
        return list.ToArray();
    }
}
