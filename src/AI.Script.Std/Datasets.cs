using AI.DataStructs.Algebraic;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Перевод между матрицей признаков языка и наборами векторов фреймворка.
/// </summary>
/// <remarks>
/// Алгоритмы <c>AI.ML</c> принимают <c>Vector[]</c> — по вектору на объект, а в языке выборка
/// это матрица «объект × признак». Перевод собран в одном месте: иначе каждая привязка
/// повторяла бы его по-своему, и рано или поздно одна из них перепутала бы строки со столбцами.
/// </remarks>
internal static class Datasets
{
    /// <summary>Строки матрицы как отдельные векторы-объекты.</summary>
    public static Vector[] Rows(Matrix data)
    {
        var rows = new Vector[data.Height];

        for (int i = 0; i < data.Height; i++)
        {
            var row = new Vector(data.Width);

            for (int j = 0; j < data.Width; j++) row[j] = data[i, j];

            rows[i] = row;
        }

        return rows;
    }

    /// <summary>Матрица из векторов-строк.</summary>
    public static Matrix FromRows(IReadOnlyList<Vector> rows)
    {
        if (rows.Count == 0) return new Matrix(0, 0);

        var matrix = new Matrix(rows.Count, rows[0].Count);

        for (int i = 0; i < rows.Count; i++)
        {
            for (int j = 0; j < rows[0].Count && j < rows[i].Count; j++) matrix[i, j] = rows[i][j];
        }

        return matrix;
    }

    /// <summary>Метки классов целыми числами; дробная метка — ошибка, а не молчаливое округление.</summary>
    public static int[] Labels(Vector labels, string what)
    {
        var result = new int[labels.Count];

        for (int i = 0; i < labels.Count; i++)
        {
            double value = labels[i];
            double rounded = Math.Round(value);

            if (Math.Abs(value - rounded) > 1e-9)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"{what}: метка класса {ScriptFormatter.Number(value)} не целая",
                    "метки классов — целые числа; для категорий используйте table.encode");
            }

            result[i] = (int)rounded;
        }

        return result;
    }

    /// <summary>Проверяет, что число объектов совпадает с числом меток.</summary>
    public static void RequireSameLength(Matrix data, Vector labels, string what)
    {
        if (data.Height == labels.Count) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: {data.Height} объектов и {labels.Count} меток",
            "число строк матрицы признаков обязано совпадать с длиной вектора меток");
    }

    /// <summary>Проверяет, что выборка не пуста.</summary>
    public static Matrix RequireNotEmpty(Matrix data, string what) =>
        data.Height > 0 && data.Width > 0
            ? data
            : throw new ScriptError(DiagnosticCodes.SizeMismatch, $"{what}: выборка пуста");

    /// <summary>Перемешанный порядок индексов от ГСЧ прогона.</summary>
    public static int[] Shuffled(Random random, int count)
    {
        var order = new int[count];

        for (int i = 0; i < count; i++) order[i] = i;

        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        return order;
    }
}
