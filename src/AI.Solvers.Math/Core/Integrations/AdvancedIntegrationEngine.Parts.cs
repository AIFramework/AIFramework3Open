using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations;

public static partial class AdvancedIntegrationEngine
{
    #region Интегрирование по частям

    private const int MaxPartsDepth = 4;

    private static Expression? TryIntegrationByParts(Expression expr, string variable)
    {
        return TryIntegrationByPartsRecursive(expr, variable, 0);
    }

    private static Expression? TryIntegrationByPartsRecursive(Expression expr, string variable, int depth)
    {
        if (depth > MaxPartsDepth) return null;
        if (expr is not Multiply mult) return null;

        // Быстрые точные паттерны (без рекурсии)
        var quick = TryKnownByPartsPatterns(mult, variable);
        if (quick != null) return quick;

        // Общий алгоритм LIATE
        return TryGeneralByParts(mult, variable, depth);
    }

    /// <summary>
    /// Быстрые паттерны для известных интегралов:
    /// sin·cos, exp·sin, exp·cos — случаи, где рекурсия не нужна или требует
    /// специальной обработки (табулярный метод для exp·trig).
    /// </summary>
    private static Expression? TryKnownByPartsPatterns(Multiply mult, string variable)
    {
        var x = new Variable(variable);

        // ∫ exp(x)·sin(x) dx = exp(x)·(sin(x)-cos(x))/2
        if (IsExpOfVar(mult.Left, variable) && IsSinOfVar(mult.Right, variable) ||
            IsSinOfVar(mult.Left, variable) && IsExpOfVar(mult.Right, variable))
            return new Multiply(new Constant(0.5),
                new Multiply(new Exp(x), new Add(new Sin(x), new Multiply(new Constant(-1), new Cos(x)))));

        // ∫ exp(x)·cos(x) dx = exp(x)·(sin(x)+cos(x))/2
        if (IsExpOfVar(mult.Left, variable) && IsCosOfVar(mult.Right, variable) ||
            IsCosOfVar(mult.Left, variable) && IsExpOfVar(mult.Right, variable))
            return new Multiply(new Constant(0.5),
                new Multiply(new Exp(x), new Add(new Sin(x), new Cos(x))));

        // ∫ sin(ax+b)·cos(ax+b) dx = sin²(ax+b)/(2a)
        var sinCos = TryIntegrateSinCosProduct(mult, variable);
        if (sinCos != null) return sinCos;

        // ∫ exp(ax)·sin(bx) dx = exp(ax)·(a·sin(bx) - b·cos(bx))/(a²+b²)
        if (TryExpTrigProduct(mult, variable, out var expTrigResult))
            return expTrigResult;

        return null;
    }

    /// <summary>
    /// Обрабатывает ∫ exp(ax)·sin(bx) dx и ∫ exp(ax)·cos(bx) dx
    /// через формулу табулярного метода (цикл через 2 шага).
    /// </summary>
    private static bool TryExpTrigProduct(Multiply mult, string variable,
        out Expression? result)
    {
        result = null;
        Expression? expPart = null, trigPart = null;

        if (mult.Left is Exp && (mult.Right is Sin || mult.Right is Cos))
            { expPart = mult.Left; trigPart = mult.Right; }
        else if (mult.Right is Exp && (mult.Left is Sin || mult.Left is Cos))
            { expPart = mult.Right; trigPart = mult.Left; }
        else return false;

        var exp = (Exp)expPart;
        if (!IsLinearInVariable(exp.Argument, variable, out double a, out double _b1))
            return false;
        if (System.Math.Abs(a) < 1e-12) return false;

        Expression trigArg;
        bool isSin;
        if (trigPart is Sin sinT) { trigArg = sinT.Argument; isSin = true; }
        else { trigArg = ((Cos)trigPart).Argument; isSin = false; }

        if (!IsLinearInVariable(trigArg, variable, out double b, out double _b2))
            return false;
        if (System.Math.Abs(b) < 1e-12) return false;

        double denom = a * a + b * b;
        var x = new Variable(variable);

        // ∫ exp(ax)·sin(bx) = exp(ax)·(a·sin(bx) - b·cos(bx)) / (a²+b²)
        // ∫ exp(ax)·cos(bx) = exp(ax)·(a·cos(bx) + b·sin(bx)) / (a²+b²)
        if (isSin)
        {
            result = new Multiply(
                new Constant(1.0 / denom),
                new Multiply(exp,
                    new Add(
                        new Multiply(new Constant(a), new Sin(trigArg)),
                        new Multiply(new Constant(-b), new Cos(trigArg)))));
        }
        else
        {
            result = new Multiply(
                new Constant(1.0 / denom),
                new Multiply(exp,
                    new Add(
                        new Multiply(new Constant(a), new Cos(trigArg)),
                        new Multiply(new Constant(b), new Sin(trigArg)))));
        }
        return true;
    }

    /// <summary>
    /// Общий алгоритм интегрирования по частям с эвристикой LIATE.
    /// ∫ u dv = u·v - ∫ v·du
    /// u выбирается по приоритету: Log > InverseTrig > Algebraic > Trig > Exp
    /// </summary>
    private static Expression? TryGeneralByParts(Multiply mult, string variable, int depth)
    {
        // Разделяем на два множителя
        var (factor1, factor2) = (mult.Left, mult.Right);

        // Выбираем u (то, что дифференцируем) и dv (то, что интегрируем)
        Expression u, dv;
        if (LiatePriority(factor1) >= LiatePriority(factor2))
            { u = factor1; dv = factor2; }
        else
            { u = factor2; dv = factor1; }

        // Проверяем, что u содержит переменную (иначе незачем)
        var vars = new HashSet<string>();
        ExpressionEvaluator.CollectVariables(u, vars);
        if (!vars.Contains(variable)) return null;

        // dv должен интегрироваться без частей (иначе бесконечная рекурсия)
        var v = TryIntegrateWithoutParts(dv, variable);
        if (v == null) return null;

        var du = u.Derivative(variable).Simplify();

        // Если du стало 0 — тривиальный случай: ∫ c·f(x) dx
        if (du is Constant cdu && System.Math.Abs(cdu.Value) < 1e-12)
            return new Multiply(u, v);

        // ∫ v·du — пробуем интегрировать рекурсивно
        var vdu = new Multiply(v, du).Simplify();

        // Сначала пробуем базовое + табличное интегрирование
        var innerIntegral = TryBasicIntegration(vdu, variable)
                         ?? TryTableIntegration(vdu, variable)
                         ?? TryIntegrationByPartsRecursive(vdu, variable, depth + 1);

        if (innerIntegral == null) return null;

        // u·v - ∫ v·du
        return new Add(
            new Multiply(u, v),
            new Multiply(new Constant(-1), innerIntegral));
    }

    /// <summary>
    /// Интегрирует выражение без использования метода по частям
    /// (базовые + табличные + подстановка + спецфункции).
    /// </summary>
    private static Expression? TryIntegrateWithoutParts(Expression expr, string variable)
    {
        expr = expr.Simplify();
        return TryBasicIntegration(expr, variable)
            ?? TryTableIntegration(expr, variable)
            ?? TryTrigonometricPowers(expr, variable)
            ?? TrySubstitution(expr, variable)
            ?? TrySpecialFunctions(expr, variable);
    }

    /// <summary>
    /// Приоритет LIATE: чем выше число, тем приоритетнее выбор как u.
    /// L(5) > I(4) > A(3) > T(2) > E(1)
    /// </summary>
    private static int LiatePriority(Expression expr)
    {
        return expr switch
        {
            Ln or Log or Log10 => 5,
            Asin or Acos or Atan => 4,
            Asinh or Acosh or Atanh => 4,
            // Полином: x^n или x или константа·x^n
            Power p when p.Base is Variable && p.Exponent is Constant => 3,
            Variable => 3,
            Multiply m when IsPolynomialTerm(m) => 3,
            Sin or Cos or Tan or Cot or Sec or Csc => 2,
            Sinh or Cosh or Tanh => 2,
            Exp => 1,
            Constant => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Проверяет, является ли выражение полиномиальным членом (const * x^n).
    /// </summary>
    private static bool IsPolynomialTerm(Expression expr)
    {
        if (expr is Variable) return true;
        if (expr is Constant) return true;
        if (expr is Power p && p.Base is Variable && p.Exponent is Constant) return true;
        if (expr is Multiply m)
        {
            if (m.Left is Constant && IsPolynomialTerm(m.Right)) return true;
            if (m.Right is Constant && IsPolynomialTerm(m.Left)) return true;
        }
        return false;
    }

    private static bool IsExpOfVar(Expression e, string v) =>
        e is Exp exp && exp.Argument is Variable ve && ve.Name == v;

    private static bool IsSinOfVar(Expression e, string v) =>
        e is Sin sin && sin.Argument is Variable vs && vs.Name == v;

    private static bool IsCosOfVar(Expression e, string v) =>
        e is Cos cos && cos.Argument is Variable vc && vc.Name == v;

    #endregion
}
