using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations;

// Класс для невычисленных интегралов
public class UnevaluatedIntegral : Expression
{
    public Expression Integrand { get; }
    public string Variable { get; }

    public UnevaluatedIntegral(Expression integrand, string variable)
    {
        Integrand = integrand;
        Variable = variable;
    }

    public override Expression Derivative(string variable)
    {
        if (variable == Variable)
            return Integrand;
        return new Constant(0);
    }

    public override Expression Simplify() => this;

    public override string ToString() => $"integral ({Integrand}) d{Variable}";

    public override Expression Clone() => new UnevaluatedIntegral(Integrand.Clone(), Variable);
}
