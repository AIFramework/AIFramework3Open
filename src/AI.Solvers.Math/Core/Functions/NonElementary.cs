using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Functions;

// Класс для представления неберущихся интегралов с описанием
public class NonElementary : Expression
{
    public string Description { get; }
    public Expression Integrand { get; }
    public string Variable { get; }

    public NonElementary(Expression integrand, string variable, string description)
    {
        Integrand = integrand;
        Variable = variable;
        Description = description;
    }

    public override Expression Derivative(string variable)
    {
        // Производная интеграла - это подынтегральная функция
        if (variable == Variable)
            return Integrand;
        return new Constant(0);
    }

    public override Expression Simplify()
    {
        return this;
    }

    public override string ToString() => $"∫({Integrand}) d{Variable}  [Неберущийся: {Description}]";

    public override Expression Clone() => new NonElementary(Integrand.Clone(), Variable, Description);
}

