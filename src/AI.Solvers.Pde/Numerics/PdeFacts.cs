using AI.DataStructs.Algebraic;
using System.Globalization;

namespace AI.Solvers.Pde.Numerics;

/// <summary>
/// Общие для решений краевых задач факты, которые попадают в интерпретацию.
/// </summary>
/// <remarks>
/// Формулировки о сходимости и погрешности одинаковы для конечных разностей и конечных
/// элементов. Держать их в каждом типе значило бы однажды поправить одну копию и получить
/// разные объяснения одного и того же явления в соседних решателях.
/// </remarks>
internal static class PdeFacts
{
    /// <summary>Предупреждение о несошедшемся решателе системы</summary>
    internal const string NotConverged =
        "Решатель системы не достиг порога по невязке, и значения нельзя считать решением сеточной задачи. "
        + "Частые причины: матрица несимметрична (например, условие Дирихле внесено только строкой, "
        + "без исключения столбца), не положительно определена либо предел итераций мал для её обусловленности.";

    /// <summary>Предупреждение о различии невязки и погрешности дискретизации</summary>
    internal const string ResidualIsNotError =
        "Невязка говорит о том, насколько точно решена сеточная система, а не о том, насколько сетка "
        + "близка к непрерывной задаче. Погрешность дискретизации от неё не зависит и уменьшается только с шагом сетки.";

    /// <summary>Рекомендация проверить сходимость по сетке для схем второго порядка</summary>
    internal const string RefineGrid =
        "Проверить сходимость по сетке: пересчитать с шагом вдвое меньше и сравнить значения в общих узлах. "
        + "Если разница падает примерно вчетверо, сетка достаточна и схема работает со своим вторым порядком.";

    /// <summary>Число в экспоненциальной записи с двумя знаками</summary>
    /// <param name="value">Значение</param>
    internal static string Sci(double value)
        => double.IsNaN(value) ? "не определено" : value.ToString("E2", CultureInfo.InvariantCulture);

    /// <summary>Наименьшее и наибольшее значения; для пустого вектора — нечисла</summary>
    /// <param name="values">Значения</param>
    internal static (double Min, double Max) Range(Vector values)
    {
        if (values is null || values.Count == 0)
            return (double.NaN, double.NaN);

        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;

        for (int i = 0; i < values.Count; i++)
        {
            min = Math.Min(min, values[i]);
            max = Math.Max(max, values[i]);
        }

        return (min, max);
    }

    /// <summary>Наибольшее по модулю значение</summary>
    /// <param name="values">Значения</param>
    internal static double MaxAbs(Vector values)
    {
        double max = 0;

        if (values is null)
            return max;

        for (int i = 0; i < values.Count; i++)
            max = Math.Max(max, Math.Abs(values[i]));

        return max;
    }

    /// <summary>Все ли значения конечны</summary>
    /// <param name="values">Значения</param>
    internal static bool AllFinite(Vector values)
    {
        if (values is null)
            return true;

        for (int i = 0; i < values.Count; i++)
            if (!double.IsFinite(values[i]))
                return false;

        return true;
    }
}
