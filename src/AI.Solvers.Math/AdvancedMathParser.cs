using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Integrations;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math;
public class AdvancedMathParser
{
    private List<Token> _tokens;
    private int _position;

    public AdvancedMathParser(List<Token> tokens)
    {
        _tokens = tokens;
        _position = 0;
    }

    private Token CurrentToken => _tokens[_position];

    private void Advance() => _position++;

    private void Expect(TokenType type)
    {
        if (CurrentToken.Type != type)
            throw new Exception($"Ожидался токен {type}, получен {CurrentToken.Type}");
        Advance();
    }

    public Expression Parse()
    {
        var expr = ParseExpression();
        if (CurrentToken.Type != TokenType.End)
            throw new Exception($"Неожиданные символы в конце: {CurrentToken}");
        return expr;
    }

    private Expression ParseExpression() => ParseAddSubtract();

    private Expression ParseAddSubtract()
    {
        var left = ParseMultiplyDivide();

        while (CurrentToken.Type == TokenType.Plus ||
               CurrentToken.Type == TokenType.Minus)
        {
            var op = CurrentToken.Type;
            Advance();
            var right = ParseMultiplyDivide();

            if (op == TokenType.Plus)
                left = new Add(left, right);
            else
                left = new Add(left, new Multiply(new Constant(-1), right));
        }

        return left;
    }

    private Expression ParseMultiplyDivide()
    {
        var left = ParsePower();

        while (CurrentToken.Type == TokenType.Multiply ||
               CurrentToken.Type == TokenType.Divide)
        {
            var op = CurrentToken.Type;
            Advance();
            var right = ParsePower();

            if (op == TokenType.Multiply)
                left = new Multiply(left, right);
            else
                left = new Multiply(left, new Power(right, new Constant(-1)));
        }

        return left;
    }

    private Expression ParsePower()
    {
        var left = ParseUnary();

        if (CurrentToken.Type == TokenType.Power)
        {
            Advance();
            var right = ParsePower();
            return new Power(left, right);
        }

        return left;
    }

    private Expression ParseUnary()
    {
        if (CurrentToken.Type == TokenType.Minus)
        {
            Advance();
            return new Multiply(new Constant(-1), ParseUnary());
        }

        if (CurrentToken.Type == TokenType.Plus)
        {
            Advance();
            return ParseUnary();
        }

        return ParsePostfix();
    }

    private Expression ParsePostfix()
    {
        var expr = ParseAtom();

        // Обработка постфиксных операторов, например ^2 после функции
        while (CurrentToken.Type == TokenType.Power)
        {
            Advance();
            var exponent = ParseUnary();
            expr = new Power(expr, exponent);
        }

        return expr;
    }

    private Expression ParseAtom()
    {
        // Интегралы
        if (CurrentToken.Type == TokenType.Integral)
        {
            return ParseIntegral();
        }

        // Числа
        if (CurrentToken.Type == TokenType.Number)
        {
            double value = double.Parse(CurrentToken.Value,
                System.Globalization.CultureInfo.InvariantCulture);
            Advance();
            return new Constant(value);
        }

        // Функции
        if (CurrentToken.Type == TokenType.Function)
        {
            return ParseFunction();
        }

        // Переменные и математические константы
        if (CurrentToken.Type == TokenType.Variable)
        {
            string varName = CurrentToken.Value;
            
            // Проверяем, является ли это математической константой
            if (varName.ToLower() == "e")
            {
                Advance();
                return new Constant(System.Math.E);  // Число Эйлера ≈ 2.71828
            }
            
            if (varName.ToLower() == "pi")
            {
                Advance();
                return new Constant(System.Math.PI);  // Число Пи ≈ 3.14159
            }
            
            Advance();
            return new Variable(varName);
        }

        // Скобки
        if (CurrentToken.Type == TokenType.LeftParen)
        {
            Advance();
            var expr = ParseExpression();
            Expect(TokenType.RightParen);
            return expr;
        }

        throw new Exception($"Неожиданный токен: {CurrentToken}");
    }

    private Expression ParseFunction()
    {
        string funcName = CurrentToken.Value.ToLower();
        Advance();

        // Обрабатываем log(base, arg) отдельно
        if (funcName == "log")
        {
            Expect(TokenType.LeftParen);
            var firstArg = ParseExpression();

            if (CurrentToken.Type == TokenType.Comma)
            {
                Advance();
                var secondArg = ParseExpression();
                Expect(TokenType.RightParen);
                return new Log(firstArg, secondArg);
            }
            else
            {
                Expect(TokenType.RightParen);
                return new Log10(firstArg);
            }
        }

        // Обычные функции
        Expect(TokenType.LeftParen);
        var arg = ParseExpression();
        Expect(TokenType.RightParen);

        return CreateFunction(funcName, arg);
    }

    private Expression CreateFunction(string funcName, Expression arg)
    {
        return funcName switch
        {
            "sin" => new Sin(arg),
            "cos" => new Cos(arg),
            "tan" => new Tan(arg),
            "cot" => new Cot(arg),
            "sec" => new Sec(arg),
            "csc" => new Csc(arg),
            "asin" => new Asin(arg),
            "acos" => new Acos(arg),
            "atan" => new Atan(arg),
            "sinh" => new Sinh(arg),
            "cosh" => new Cosh(arg),
            "tanh" => new Tanh(arg),
            "asinh" => new Asinh(arg),
            "acosh" => new Acosh(arg),
            "atanh" => new Atanh(arg),
            "ln" => new Ln(arg),
            "log10" => new Log10(arg),
            "exp" => new Exp(arg),
            "sqrt" => new Power(arg, new Constant(0.5)),
            "abs" => new Abs(arg),
            "erf" => new Erf(arg),
            "erfc" => new Erfc(arg),
            "sgn" => new Sgn(arg),
            "sign" => new Sgn(arg),
            "heaviside" => new Heaviside(arg),
            "h" => new Heaviside(arg),
            _ => throw new Exception($"Неизвестная функция: {funcName}")
        };
    }

    private Expression ParseIntegral()
    {
        Advance();

        Expression integrand;
        string variable;

        if (CurrentToken.Type == TokenType.LeftParen)
        {
            Advance();
            integrand = ParseExpression();

            if (CurrentToken.Type == TokenType.Comma)
            {
                Expect(TokenType.Comma);

                if (CurrentToken.Type != TokenType.Variable)
                    throw new Exception("Ожидалась переменная интегрирования");

                variable = CurrentToken.Value;
                Advance();
                Expect(TokenType.RightParen);
            }
            else
            {
                throw new Exception("Формат: integral(выражение, переменная)");
            }
        }
        else
        {
            integrand = ParsePower();

            if (CurrentToken.Type == TokenType.Function &&
                CurrentToken.Value.ToLower() == "d")
            {
                Advance();
                if (CurrentToken.Type != TokenType.Variable)
                    throw new Exception("Ожидалась переменная после 'd'");
                variable = CurrentToken.Value;
                Advance();
            }
            else
            {
                throw new Exception("Формат: ∫выражение dx");
            }
        }

        return AdvancedIntegrationEngine.Integrate(integrand, variable);
    }
}