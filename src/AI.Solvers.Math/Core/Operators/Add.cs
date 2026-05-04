using AI.Solvers.Math.CAS;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Operators;
public class Add : Expression
{
    public Expression Left { get; }
    public Expression Right { get; }

    public Add(Expression left, Expression right)
    {
        Left = left;
        Right = right;
    }

    public override Expression Derivative(string variable) =>
        new Add(Left.Derivative(variable), Right.Derivative(variable));

    public override Expression Simplify()
    {
        var left = Left.Simplify();
        var right = Right.Simplify();

        if (left is Constant c1 && System.Math.Abs(c1.Value) < 1e-10)
            return right;
        if (right is Constant c2 && System.Math.Abs(c2.Value) < 1e-10)
            return left;

        if (left is Constant cl && right is Constant cr)
            return new Constant(cl.Value + cr.Value);

        if (IsSinSquared(left, out var sinArg1) && IsCosSquared(right, out var cosArg1))
        {
            if (AlgebraicSimplifier.ExpressionsEqual(sinArg1, cosArg1))
                return new Constant(1);
        }

        if (IsCosSquared(left, out var cosArg2) && IsSinSquared(right, out var sinArg2))
        {
            if (AlgebraicSimplifier.ExpressionsEqual(sinArg2, cosArg2))
                return new Constant(1);
        }

        if (left is Constant c3 && System.Math.Abs(c3.Value - 1) < 1e-10)
        {
            if (right is Multiply m1 &&
                m1.Left is Constant cm1 && System.Math.Abs(cm1.Value + 1) < 1e-10 &&
                IsSinSquared(m1.Right, out var sinArg3))
            {
                return new Power(new Cos(sinArg3), new Constant(2));
            }
        }

        if (left is Constant c4 && System.Math.Abs(c4.Value - 1) < 1e-10)
        {
            if (right is Multiply m2 &&
                m2.Left is Constant cm2 && System.Math.Abs(cm2.Value + 1) < 1e-10 &&
                IsCosSquared(m2.Right, out var cosArg3))
            {
                return new Power(new Sin(cosArg3), new Constant(2));
            }
        }

        return new Add(left, right);
    }

    public override string ToString()
    {
        // все члены суммы в список для правильного форматирования
        var terms = new List<(bool isNegative, string term)>();
        CollectTermsForDisplay(this, terms);

        if (terms.Count == 0) return "0";
        if (terms.Count == 1)
        {
            var (isNeg, term) = terms[0];
            return isNeg ? $"-{term}" : term;
        }

        // Формат с правильными знаками
        var result = new System.Text.StringBuilder();
        bool first = true;
        foreach (var (isNeg, term) in terms)
        {
            if (first)
            {
                result.Append(isNeg ? $"-{term}" : term);
                first = false;
            }
            else
            {
                result.Append(isNeg ? $" - {term}" : $" + {term}");
            }
        }

        return result.ToString();
    }

    // Вспомогательный метод для сбора членов
    private static void CollectTermsForDisplay(Expression expr, List<(bool, string)> terms)
    {
        if (expr is Add add)
        {
            CollectTermsForDisplay(add.Left, terms);
            CollectTermsForDisplay(add.Right, terms);
        }
        else if (expr is Multiply mult && mult.Left is Constant c)
        {
            if (c.Value < 0)
            {
                if (System.Math.Abs(c.Value + 1) < 1e-10)
                    terms.Add((true, mult.Right.ToString()));
                else
                    terms.Add((true, new Multiply(new Constant(-c.Value), mult.Right).ToString()));
            }
            else
            {
                terms.Add((false, expr.ToString()));
            }
        }
        else if (expr is Constant constant && constant.Value < 0)
        {
            terms.Add((true, new Constant(-constant.Value).ToString()));
        }
        else
        {
            terms.Add((false, expr.ToString()));
        }
    }

    private static bool IsSinSquared(Expression expr, out Expression argument)
    {
        argument = null!;

        if (expr is Power pow &&
            pow.Base is Sin sin &&
            pow.Exponent is Constant c &&
            System.Math.Abs(c.Value - 2) < 1e-10)
        {
            argument = sin.Argument;
            return true;
        }

        return false;
    }

    private static bool IsCosSquared(Expression expr, out Expression argument)
    {
        argument = null!;

        if (expr is Power pow &&
            pow.Base is Cos cos &&
            pow.Exponent is Constant c &&
            System.Math.Abs(c.Value - 2) < 1e-10)
        {
            argument = cos.Argument;
            return true;
        }

        return false;
    }

    public override Expression Clone() => new Add(Left.Clone(), Right.Clone());
}