using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;
public class Cos : Expression
{
    public Expression Argument { get; }
    
    public Cos(Expression argument) => Argument = argument;
    
    public override Expression Derivative(string variable) =>
        new Multiply(
            new Constant(-1),
            new Multiply(new Sin(Argument), Argument.Derivative(variable))
        );
    
    public override Expression Simplify()
    {
        var arg = Argument.Simplify();
        
        // cos(0) = 1
        if (arg is Constant c && c.Value == 0)
            return new Constant(1);
        
        return new Cos(arg);
    }
    
    public override string ToString() => $"cos({Argument})";
    
    public override Expression Clone() => new Cos(Argument.Clone());
}
