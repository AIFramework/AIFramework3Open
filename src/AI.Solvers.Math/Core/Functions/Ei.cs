using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Интегральная экспонента
public class Ei : Expression
{
    public Expression Argument { get; }

    public Ei(Expression argument) => Argument = argument;

    public override Expression Derivative(string variable)
    {
        // d/dx Ei(f(x)) = e^f(x)/f(x) * f'(x)
        return new Multiply(
            new Multiply(
                new Exp(Argument),
                new Power(Argument, new Constant(-1))
            ),
            Argument.Derivative(variable)
        );
    }

    public override Expression Simplify()
    {
        var arg = Argument.Simplify();
        return new Ei(arg);
    }

    public override string ToString() => $"Ei({Argument})";

    public override Expression Clone() => new Ei(Argument.Clone());
}

