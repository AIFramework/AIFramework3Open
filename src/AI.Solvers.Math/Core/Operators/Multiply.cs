using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Operators;

public class Multiply : Expression
{
    public Expression Left { get; }
    public Expression Right { get; }

    public Multiply(Expression left, Expression right)
    {
        Left = left;
        Right = right;
    }

    public override Expression Derivative(string variable) =>
        new Add(
            new Multiply(Left.Derivative(variable), Right),
            new Multiply(Left, Right.Derivative(variable))
        );

    public override Expression Simplify()
    {
        var left = Left.Simplify();
        var right = Right.Simplify();

        if (left is Constant c1 && System.Math.Abs(c1.Value) < 1e-10)
            return new Constant(0);
        if (right is Constant c2 && System.Math.Abs(c2.Value) < 1e-10)
            return new Constant(0);

        if (left is Constant c3 && System.Math.Abs(c3.Value - 1) < 1e-10)
            return right;
        if (right is Constant c4 && System.Math.Abs(c4.Value - 1) < 1e-10)
            return left;

        if (left is Constant c5 && System.Math.Abs(c5.Value + 1) < 1e-10 &&
            right is Constant c6 && System.Math.Abs(c6.Value + 1) < 1e-10)
            return new Constant(1);

        if (left is Constant cl && right is Constant cr)
            return new Constant(cl.Value * cr.Value);

        if (left is Constant c7 && System.Math.Abs(c7.Value + 1) < 1e-10)
        {
            if (right is Multiply m1 && m1.Left is Constant c8 && System.Math.Abs(c8.Value + 1) < 1e-10)
            {
                return m1.Right.Simplify();
            }
        }

        if (right is Constant c9 && System.Math.Abs(c9.Value + 1) < 1e-10)
        {
            if (left is Multiply m2 && m2.Left is Constant c10 && System.Math.Abs(c10.Value + 1) < 1e-10)
            {
                return m2.Right.Simplify();
            }
        }

        if (left is Constant c11 && right is Multiply m3 && m3.Left is Constant c12)
        {
            return new Multiply(
                new Constant(c11.Value * c12.Value),
                m3.Right
            ).Simplify();
        }

        if (left is Multiply m4 && m4.Left is Constant c13 && right is Constant c14)
        {
            return new Multiply(
                new Constant(c13.Value * c14.Value),
                m4.Right
            ).Simplify();
        }

        if (left is Multiply m5 && m5.Left is Constant c15 &&
            right is Multiply m6 && m6.Left is Constant c16)
        {
            return new Multiply(
                new Multiply(new Constant(c15.Value * c16.Value), m5.Right),
                m6.Right
            ).Simplify();
        }

        if (right is Constant && !(left is Constant))
            return new Multiply(right, left).Simplify();

        if (left is Multiply m7 && m7.Left is Constant)
        {
            return new Multiply(left, right);
        }

        return new Multiply(left, right);
    }

    public override string ToString()
    {
        if (Left is Constant c && System.Math.Abs(c.Value + 1) < 1e-10)
            return $"-{FormatRightOperand(Right)}";

        if (Left is Constant c2 && System.Math.Abs(c2.Value - 1) < 1e-10)
            return Right.ToString();

        string leftStr = FormatLeftOperand(Left);
        string rightStr = FormatRightOperand(Right);

        bool needStar = NeedMultiplicationSign(Left, Right);

        if (needStar)
            return $"{leftStr}*{rightStr}";
        else
            return $"{leftStr}{rightStr}";
    }

    private string FormatLeftOperand(Expression expr)
    {
        return expr.ToString();
    }

    private string FormatRightOperand(Expression expr)
    {
        if (expr is Add)
            return $"({expr})";

        if (expr is Multiply mult && mult.Left is Constant c && c.Value < 0)
            return $"({expr})";

        return expr.ToString();
    }

    private bool NeedMultiplicationSign(Expression left, Expression right)
    {
        if (left is Constant && right is Variable)
            return false;

        if (left is Constant && IsSimpleFunction(right))
            return false;

        if (left is Constant && right is Power)
            return false;

        return true;
    }

    private bool IsSimpleFunction(Expression expr)
    {
        return expr is Sin || expr is Cos || expr is Tan ||
               expr is Exp || expr is Ln || expr is Sinh ||
               expr is Cosh || expr is Tanh || expr is Abs ||
               expr is Sec || expr is Csc || expr is Cot ||
               expr is Asin || expr is Acos || expr is Atan ||
               expr is Asinh || expr is Acosh || expr is Atanh ||
               expr is Erf || expr is Erfc;
    }

    public override Expression Clone() => new Multiply(Left.Clone(), Right.Clone());
}