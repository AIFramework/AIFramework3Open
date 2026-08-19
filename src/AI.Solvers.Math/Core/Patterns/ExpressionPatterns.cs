using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Patterns;

/// <summary>
/// Распознаватели повторяющихся форм AST.
/// <para>
/// Каждый из них раньше жил в трёх-четырёх копиях с расходящимися правилами:
/// гауссиан разбирался в двух местах Фурье, в его же рекурсивной ветке и в
/// движке интегрирования; произведение sin·cos — в подстановках, в методе по
/// частям и в таблице Лапласа; линейный аргумент ax+b — в движке интегрирования
/// и отдельной усечённой версией в Фурье. Копии расходились: часть учитывала
/// коэффициент при переменной, часть молча считала его единицей.
/// </para>
/// </summary>
internal static class ExpressionPatterns
{
    /// <summary>
    /// Распознаёт exp(-a·x²) с a &gt; 0 и возвращает коэффициент затухания a.
    /// </summary>
    public static bool TryMatchGaussian(Exp exponential, string variable, out double decay)
    {
        decay = 0;

        if (exponential.Argument is Multiply mult &&
            mult.Left is Constant coefficient && coefficient.Value < 0 &&
            mult.Right is Power power &&
            power.Base is Variable v && v.Name == variable &&
            power.Exponent is Constant exponent && System.Math.Abs(exponent.Value - 2) < 1e-10)
        {
            decay = -coefficient.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Распознаёт произведение sin(u)·cos(u) с совпадающим аргументом (в любом порядке).
    /// </summary>
    public static bool TryMatchSinCosProduct(Expression expr, out Expression argument)
    {
        argument = expr;

        if (expr is not Multiply mult) return false;

        Sin? sin = mult.Left as Sin ?? mult.Right as Sin;
        Cos? cos = mult.Left as Cos ?? mult.Right as Cos;
        if (sin is null || cos is null) return false;
        if (sin.Argument.ToString() != cos.Argument.ToString()) return false;

        argument = sin.Argument;
        return true;
    }

    /// <summary>
    /// Распознаёт линейную форму a·variable + b. Возвращает false, если выражение
    /// не линейно или a = 0 — на a обычно делят (подстановка u = ax+b).
    /// </summary>
    public static bool TryMatchLinear(Expression expr, string variable, out double a, out double b)
    {
        a = 0; b = 0;

        if (expr is Variable v && v.Name == variable) { a = 1; return true; }

        if (expr is Multiply mult)
        {
            if (mult.Left is Constant cl && mult.Right is Variable v2 && v2.Name == variable)
            { a = cl.Value; return System.Math.Abs(a) > 1e-12; }

            if (mult.Right is Constant cr && mult.Left is Variable v3 && v3.Name == variable)
            { a = cr.Value; return System.Math.Abs(a) > 1e-12; }
        }

        if (expr is Add add)
        {
            bool left  = TryMatchLinear(add.Left,  variable, out double a1, out double b1);
            bool right = TryMatchLinear(add.Right, variable, out double a2, out double b2);
            if (left && right)
            {
                a = a1 + a2; b = b1 + b2;
                // Линейность с нулевым коэффициентом — фактически константа,
                // непригодна для табличных интегралов с множителем 1/a.
                return System.Math.Abs(a) > 1e-12;
            }
        }

        if (expr is Constant c) { a = 0; b = c.Value; return true; }

        return false;
    }

    /// <summary>
    /// Коэффициент при переменной в линейном аргументе; 0, если формы не той.
    /// Удобно там, где неудача — не ошибка, а просто «шаблон не подошёл».
    /// </summary>
    public static double LinearCoefficient(Expression expr, string variable) =>
        TryMatchLinear(expr, variable, out double a, out _) ? a : 0;
}
