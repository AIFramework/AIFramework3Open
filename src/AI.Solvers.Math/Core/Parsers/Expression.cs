namespace AI.Solvers.Math.Core.Parsers;

public abstract class Expression
{
    public abstract Expression Derivative(string variable);
    public abstract Expression Simplify();
    public abstract Expression Clone();

    public override string ToString()
    {
        return base.ToString(); 
    }
}
