using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core;

public class Variable : Expression
{
    public string Name { get; }

    public Variable(string name)
    {
        Name = name;
    }

    public override Expression Derivative(string variable) =>
        Name == variable ? new Constant(1) : new Constant(0);

    public override Expression Simplify() => this;

    public override string ToString() => Name;

    public override Expression Clone() => new Variable(Name);
}
