using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Логарифм с произвольным основанием
public class Log : Expression
{
    public Expression Base { get; }
    public Expression Argument { get; }

    public Log(Expression baseExpr, Expression argument)
    {
        Base = baseExpr;
        Argument = argument;
    }

    // d/dx log_a(f) = 1/(f * ln(a)) * f'
    public override Expression Derivative(string variable)
    {
        return new Multiply(
            new Power(
                new Multiply(
                    Argument,
                    new Ln(Base)
                ),
                new Constant(-1)
            ),
            Argument.Derivative(variable)
        );
    }

    public override Expression Simplify()
    {
        var baseExpr = Base.Simplify();
        var arg = Argument.Simplify();

        // log_a(1) = 0
        if (arg is Constant c1 && c1.Value == 1)
            return new Constant(0);

        // log_a(a) = 1
        if (baseExpr is Constant cb && arg is Constant ca &&
            System.Math.Abs(cb.Value - ca.Value) < 1e-10)
            return new Constant(1);

        if (baseExpr is Constant cb2 && arg is Constant ca2)
            return new Constant(System.Math.Log(ca2.Value, cb2.Value));

        // log_e(x) = ln(x)
        if (baseExpr is Constant ce && System.Math.Abs(ce.Value - System.Math.E) < 1e-10)
            return new Ln(arg);

        return new Log(baseExpr, arg);
    }

    public override string ToString() => $"log_{Base}({Argument})";

    public override Expression Clone() => new Log(Base.Clone(), Argument.Clone());
}
