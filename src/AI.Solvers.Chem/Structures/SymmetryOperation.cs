using System.Globalization;
using System.Text.RegularExpressions;

namespace AI.Solvers.Chem.Structures;

/// <summary>
/// Операция симметрии в дробных координатах: поворотная часть и трансляция
/// </summary>
/// <remarks>
/// Разбирается запись вида "x, -y, z+1/2" - именно так операции хранятся
/// в CIF-файлах и в международных таблицах.
/// </remarks>
public sealed class SymmetryOperation
{
    private readonly double[,] _rotation = new double[3, 3];
    private readonly double[] _translation = new double[3];

    /// <summary>Исходная запись операции</summary>
    public string Notation { get; }

    /// <summary>Тождественная операция</summary>
    public static SymmetryOperation Identity => Parse("x,y,z");

    private SymmetryOperation(string notation) => Notation = notation;

    /// <summary>Разбирает запись операции симметрии</summary>
    /// <param name="notation">Запись вида "x,-y,z+1/2"</param>
    public static SymmetryOperation Parse(string notation)
    {
        if (!TryParse(notation, out SymmetryOperation operation))
            throw new FormatException($"Не разобрана операция симметрии '{notation}'");

        return operation;
    }

    /// <summary>Безопасный разбор записи операции симметрии</summary>
    /// <param name="notation">Запись операции</param>
    /// <param name="operation">Разобранная операция</param>
    public static bool TryParse(string notation, out SymmetryOperation operation)
    {
        operation = null;

        if (string.IsNullOrWhiteSpace(notation))
            return false;

        string[] parts = notation.Trim().Trim('\'', '"').Split(',');

        if (parts.Length != 3)
            return false;

        var result = new SymmetryOperation(notation.Trim());

        for (int row = 0; row < 3; row++)
        {
            if (!ParseComponent(parts[row], out double x, out double y, out double z, out double shift))
                return false;

            result._rotation[row, 0] = x;
            result._rotation[row, 1] = y;
            result._rotation[row, 2] = z;
            result._translation[row] = shift;
        }

        operation = result;
        return true;
    }

    /// <summary>Применяет операцию к дробным координатам</summary>
    /// <param name="fractional">Дробные координаты</param>
    public Vector3 Apply(Vector3 fractional) => new(
        (_rotation[0, 0] * fractional.X) + (_rotation[0, 1] * fractional.Y) + (_rotation[0, 2] * fractional.Z) + _translation[0],
        (_rotation[1, 0] * fractional.X) + (_rotation[1, 1] * fractional.Y) + (_rotation[1, 2] * fractional.Z) + _translation[1],
        (_rotation[2, 0] * fractional.X) + (_rotation[2, 1] * fractional.Y) + (_rotation[2, 2] * fractional.Z) + _translation[2]);

    /// <summary>Применяет операцию и приводит координаты в основную ячейку</summary>
    /// <param name="fractional">Дробные координаты</param>
    public Vector3 ApplyWrapped(Vector3 fractional)
    {
        Vector3 result = Apply(fractional);

        return new Vector3(Wrap(result.X), Wrap(result.Y), Wrap(result.Z));
    }

    /// <summary>Приводит дробную координату в интервал [0; 1)</summary>
    /// <param name="value">Координата</param>
    public static double Wrap(double value)
    {
        double wrapped = value - Math.Floor(value);

        // Значение вплотную к единице после округления считается нулём
        return wrapped > 1 - 1e-9 ? 0 : wrapped;
    }

    /// <summary>Запись операции</summary>
    public override string ToString() => Notation;

    // Разбор одной компоненты вида "-x+1/2" или "y"
    private static bool ParseComponent(string text, out double x, out double y, out double z, out double shift)
    {
        x = y = z = shift = 0;

        string normalized = text.Replace(" ", string.Empty).ToLowerInvariant();

        if (normalized.Length == 0)
            return false;

        foreach (Match term in Regex.Matches(normalized, @"[+-]?[^+-]+"))
        {
            string value = term.Value;
            double sign = value.StartsWith('-') ? -1 : 1;
            string body = value.TrimStart('+', '-');

            if (body.Length == 0)
                return false;

            if (body.EndsWith('x') || body.EndsWith('y') || body.EndsWith('z'))
            {
                string factorText = body[..^1];
                double factor = 1;

                if (factorText.Length > 0 && !TryParseNumber(factorText, out factor))
                    return false;

                switch (body[^1])
                {
                    case 'x': x += sign * factor; break;
                    case 'y': y += sign * factor; break;
                    default: z += sign * factor; break;
                }
            }
            else
            {
                if (!TryParseNumber(body, out double number))
                    return false;

                shift += sign * number;
            }
        }

        return true;
    }

    private static bool TryParseNumber(string text, out double value)
    {
        value = 0;

        if (text.Contains('/'))
        {
            string[] parts = text.Split('/');

            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator)
                || denominator == 0)
            {
                return false;
            }

            value = numerator / denominator;
            return true;
        }

        if (text == "*")
        {
            value = 1;
            return true;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
