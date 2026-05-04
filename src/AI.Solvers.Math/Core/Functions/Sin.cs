
// Синус
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

public class Sin : Expression
{
    public Expression Argument { get; }
    
    public Sin(Expression argument) => Argument = argument;
    
    public override Expression Derivative(string variable) =>
        new Multiply(new Cos(Argument), Argument.Derivative(variable));
    
    public override Expression Simplify()
    {
        var arg = Argument.Simplify();
        
        // sin(0) = 0
        if (arg is Constant c && c.Value == 0)
            return new Constant(0);
        
        return new Sin(arg);
    }
    
    public override string ToString() => $"sin({Argument})";
    
    public override Expression Clone() => new Sin(Argument.Clone());
}
