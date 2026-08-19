using System.Text.RegularExpressions;

namespace AI.Solvers.Math.Core;

public static class FractalMathStyleParser
{
    public static FractalMathCommand Parse(string input)
    {
        input = input.Trim();

        // Определенный интеграл
        var match = Regex.Match(input, @"^integrate\s+(.+?)\s+from\s+(\S+)\s+to\s+(\S+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var lower = ParseNumericValue(match.Groups[2].Value);
            var upper = ParseNumericValue(match.Groups[3].Value);
            if (lower is null || upper is null)
                return new FractalMathCommand
                {
                    Type = CommandType.Unknown,
                    Expression = $"Не удалось распознать границы интегрирования: " +
                                 $"from='{match.Groups[2].Value}', to='{match.Groups[3].Value}'. " +
                                 "Допустимы: число, pi, -pi, e, infinity, -infinity."
                };
            return new FractalMathCommand
            {
                Type = CommandType.DefiniteIntegral,
                Expression = match.Groups[1].Value.Trim(),
                LowerBound = lower,
                UpperBound = upper,
                Variable = "x"
            };
        }

        // Двойной интеграл
        match = Regex.Match(input, @"^integrate\s+integrate\s+(.+?)\s+d([a-z])\s+d([a-z])$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.DoubleIntegral,
                Expression = match.Groups[1].Value.Trim(),
                Variable = match.Groups[2].Value,
                Variable2 = match.Groups[3].Value
            };
        }

        // Неопределенный интеграл
        match = Regex.Match(input, @"^integrate\s+(.+?)(?:\s+dx|\s+d([a-z]))?$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.IndefiniteIntegral,
                Expression = match.Groups[1].Value.Trim(),
                Variable = match.Groups[2].Success ? match.Groups[2].Value : "x"
            };
        }

        // Первая производная
        match = Regex.Match(input, @"^derivative\s+of\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.FirstDerivative,
                Expression = match.Groups[1].Value.Trim(),
                Variable = "x"
            };
        }

        // Вторая производная
        match = Regex.Match(input, @"^second\s+derivative\s+of\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.SecondDerivative,
                Expression = match.Groups[1].Value.Trim(),
                Variable = "x",
                Order = 2
            };
        }

        // Производная n-го порядка
        match = Regex.Match(input, @"^(\d+)(?:st|nd|rd|th)\s+derivative\s+of\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.NthDerivative,
                Expression = match.Groups[2].Value.Trim(),
                Variable = "x",
                Order = int.Parse(match.Groups[1].Value)
            };
        }

        // [9] Частная производная
        match = Regex.Match(input, @"^partial\s+derivative\s+of\s+(.+?)\s+with\s+respect\s+to\s+([a-z])$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.PartialDerivative,
                Expression = match.Groups[1].Value.Trim(),
                Variable = match.Groups[2].Value
            };
        }

        // Производная сложной функции
        match = Regex.Match(input, @"^derivative\s+of\s+(.+?)\s+with\s+respect\s+to\s+([a-z])$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.FirstDerivative,
                Expression = match.Groups[1].Value.Trim(),
                Variable = match.Groups[2].Value
            };
        }

        // ODE и PDE
        match = Regex.Match(input, @"^solve\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var tail = match.Groups[1].Value.Trim();

            // Система ОДУ: каждая часть — уравнение с производной.
            // Проверять это надо ДО деления на «уравнение + начальные условия»:
            // ленивая группа (.+?) отрезала по первой же запятой, и вторая строка
            // системы («y' = -x») уходила в начальные условия, а не в решатель систем.
            var parts = tail.Split(',').Select(p => p.Trim()).ToList();
            if (parts.Count > 1 && parts.All(p => Regex.IsMatch(p, @"^[a-z]'\s*=", RegexOptions.IgnoreCase)))
            {
                return new FractalMathCommand
                {
                    Type = CommandType.SystemODE,
                    Equations = parts
                };
            }

            int commaIndex = tail.IndexOf(',');
            var equation     = commaIndex >= 0 ? tail[..commaIndex].Trim() : tail;
            var initialConds = commaIndex >= 0 ? tail[(commaIndex + 1)..].Trim() : "";

            // Проверка на PDE (уравнения в частных производных)
            if (Regex.IsMatch(equation, @"u_[a-z]{1,2}") || equation.Contains("u_xx") || equation.Contains("u_yy") ||
                equation.Contains("u_tt") || equation.Contains("u_t"))
            {
                return new FractalMathCommand
                {
                    Type = CommandType.PDE,
                    Expression = equation
                };
            }

            // ODE с начальными условиями
            if (!string.IsNullOrEmpty(initialConds) || Regex.IsMatch(equation, @"[a-z]\(\d+\)\s*="))
            {
                return new FractalMathCommand
                {
                    Type = CommandType.ODEWithInitialConditions,
                    Expression = equation,
                    InitialConditions = ParseInitialConditions(initialConds)
                };
            }

            // Обычное ODE
            if (equation.Contains("'") || Regex.IsMatch(equation, @"[a-z]''"))
            {
                return new FractalMathCommand
                {
                    Type = CommandType.ODE,
                    Expression = equation
                };
            }

            // Обычное уравнение
            return new FractalMathCommand
            {
                Type = CommandType.Solve,
                Expression = equation
            };
        }

        // Предел
        match = Regex.Match(input, @"^limit\s+(.+?)\s+as\s+([a-z])\s*->\s*(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.Limit,
                Expression = match.Groups[1].Value.Trim(),
                Variable = match.Groups[2].Value,
                LimitPoint = match.Groups[3].Value.Trim()
            };
        }

        // Ряд Тейлора
        match = Regex.Match(input, @"^Taylor\s+series\s+of\s+(.+?)\s+at\s+([a-z])\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.TaylorSeries,
                Expression = match.Groups[1].Value.Trim(),
                Variable = match.Groups[2].Value,
                LimitPoint = match.Groups[3].Value.Trim()
            };
        }

        // Таблица преобразований Лапласа
        match = Regex.Match(input, @"^Laplace\s+table$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.LaplaceTable
            };
        }

        //Преобразование Лапласа
        match = Regex.Match(input, @"^Laplace\s+transform\s+of\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.LaplaceTransform,
                Expression = match.Groups[1].Value.Trim(),
                Variable = "t"
            };
        }

        // Преобразование Фурье
        match = Regex.Match(input, @"^Fourier\s+transform\s+of\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return new FractalMathCommand
            {
                Type = CommandType.FourierTransform,
                Expression = match.Groups[1].Value.Trim(),
                Variable = "x"
            };
        }

        return new FractalMathCommand { Type = CommandType.Unknown };
    }

    private static double? ParseNumericValue(string value)
    {
        value = value.ToLower().Trim();

        if (value == "pi")
            return System.Math.PI;
        if (value == "e")
            return System.Math.E;
        if (value == "-pi")
            return -System.Math.PI;
        if (value == "infinity" || value == "inf")
            return double.PositiveInfinity;
        if (value == "-infinity" || value == "-inf")
            return double.NegativeInfinity;

        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double result))
            return result;

        return null;
    }

    private static Dictionary<string, string> ParseInitialConditions(string conditions)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(conditions))
            return result;

        var parts = conditions.Split(',');
        foreach (var part in parts)
        {
            var match = Regex.Match(part.Trim(), @"([a-z])\((.+?)\)\s*=\s*(.+)");
            if (match.Success)
            {
                var key = $"{match.Groups[1].Value}({match.Groups[2].Value})";
                result[key] = match.Groups[3].Value.Trim();
            }
        }

        return result;
    }
}

