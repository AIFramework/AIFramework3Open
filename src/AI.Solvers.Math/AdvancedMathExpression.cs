using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math;

public static class AdvancedMathExpression
{
    public static Expression Parse(string input)
    {
        var tokenizer = new ImprovedTokenizer(input);
        var tokens = tokenizer.Tokenize();
        var parser = new AdvancedMathParser(tokens);
        return parser.Parse();
    }

    public static Expression Integrate(string input)
    {
        var expr = Parse(input);
        return expr.Simplify();
    }

    public static Expression Derivative(string input, string variable)
    {
        var expr = Parse(input);
        return expr.Derivative(variable).Simplify();
    }
}
