#nullable enable
using System;

namespace AI.Units;

/// <summary>
/// Ошибка несовпадения размерностей: величину с одной размерностью передали туда,
/// где требуется другая, либо сложили несовместимые величины.
/// </summary>
[Serializable]
public class DimensionMismatchException : InvalidOperationException
{
    /// <summary>
    /// Ожидаемая размерность
    /// </summary>
    public Dimension Expected { get; }

    /// <summary>
    /// Фактическая размерность
    /// </summary>
    public Dimension Actual { get; }

    /// <summary>
    /// Создаёт ошибку несовпадения размерностей
    /// </summary>
    /// <param name="expected">Ожидаемая размерность</param>
    /// <param name="actual">Фактическая размерность</param>
    /// <param name="context">Контекст: имя параметра или выполняемая операция</param>
    public DimensionMismatchException(Dimension expected, Dimension actual, string? context = null)
        : base(BuildMessage(expected, actual, context))
    {
        Expected = expected;
        Actual = actual;
    }

    private static string BuildMessage(Dimension expected, Dimension actual, string? context)
    {
        string where = string.IsNullOrEmpty(context) ? string.Empty : $" ({context})";
        return $"Несовпадение размерностей{where}: ожидалась [{expected}], получена [{actual}]";
    }
}
