using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions
{
    // Натуральный логарифм
    public class Ln : Expression
    {
        public Expression Argument { get; }

        public Ln(Expression argument) => Argument = argument;

        public override Expression Derivative(string variable) =>
            new Multiply(
                new Power(Argument, new Constant(-1)),
                Argument.Derivative(variable)
            );

        public override Expression Simplify()
        {
            var arg = Argument.Simplify();

            // ln(1) = 0
            if (arg is Constant c && c.Value == 1)
                return new Constant(0);

            // ln(e) = 1
            if (arg is Constant c2 && System.Math.Abs(c2.Value - System.Math.E) < 1e-10)
                return new Constant(1);

            if (arg is Constant c3)
                return new Constant(System.Math.Log(c3.Value));

            return new Ln(arg);
        }

        public override string ToString() => $"ln({Argument})";

        public override Expression Clone() => new Ln(Argument.Clone());
    }
}