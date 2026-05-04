using System.Text;

namespace AI.Solvers.Math.Core.Parsers;

public class ImprovedTokenizer
{
    private string _input;
    private int _position;
    private List<Token> _tokens;

    public ImprovedTokenizer(string input)
    {
        _input = input.Replace(" ", "");
        _position = 0;
        _tokens = new List<Token>();
    }

    public List<Token> Tokenize()
    {
        while (_position < _input.Length)
        {
            char current = _input[_position];

            if (char.IsDigit(current) || current == '.')
            {
                _tokens.Add(ReadNumber());
                continue;
            }

            if (char.IsLetter(current))
            {
                _tokens.Add(ReadIdentifier());
                continue;
            }

            switch (current)
            {
                case '+':
                    _tokens.Add(new Token(TokenType.Plus, "+"));
                    _position++;
                    break;
                case '-':
                    _tokens.Add(new Token(TokenType.Minus, "-"));
                    _position++;
                    break;
                case '*':
                    _tokens.Add(new Token(TokenType.Multiply, "*"));
                    _position++;
                    break;
                case '/':
                    _tokens.Add(new Token(TokenType.Divide, "/"));
                    _position++;
                    break;
                case '^':
                    _tokens.Add(new Token(TokenType.Power, "^"));
                    _position++;
                    break;
                case '(':
                    _tokens.Add(new Token(TokenType.LeftParen, "("));
                    _position++;
                    break;
                case ')':
                    _tokens.Add(new Token(TokenType.RightParen, ")"));
                    _position++;
                    break;
                case ',':
                    _tokens.Add(new Token(TokenType.Comma, ","));
                    _position++;
                    break;
                case '∫':
                    _tokens.Add(new Token(TokenType.Integral, "∫"));
                    _position++;
                    break;
                default:
                    throw new Exception($"Неизвестный символ: '{current}' на позиции {_position}");
            }
        }

        // Добавляем неявное умножение
        InsertImplicitMultiplication();

        _tokens.Add(new Token(TokenType.End));
        return _tokens;
    }

    private void InsertImplicitMultiplication()
    {
        var result = new List<Token>();

        for (int i = 0; i < _tokens.Count; i++)
        {
            result.Add(_tokens[i]);

            if (i < _tokens.Count - 1)
            {
                var current = _tokens[i];
                var next = _tokens[i + 1];

                bool needsMultiply = false;

                // число + переменная/функция/скобка: 2x, 3sin(x), 2(x+1)
                if (current.Type == TokenType.Number)
                {
                    if (next.Type == TokenType.Variable ||
                        next.Type == TokenType.Function ||
                        next.Type == TokenType.LeftParen)
                    {
                        needsMultiply = true;
                    }
                }
                // переменная + переменная/функция/скобка: xy, x(x+1)
                // НО НЕ перед степенью: x^2 не должно превращаться в x*^2
                else if (current.Type == TokenType.Variable)
                {
                    if ((next.Type == TokenType.Variable ||
                        next.Type == TokenType.Function ||
                        next.Type == TokenType.LeftParen) &&
                        next.Type != TokenType.Power)
                    {
                        needsMultiply = true;
                    }
                }
                // ) + число/переменная/функция/скобка: (x+1)2, (x+1)sin(x), (x+1)(x-1)
                else if (current.Type == TokenType.RightParen)
                {
                    if (next.Type == TokenType.Number ||
                        next.Type == TokenType.Variable ||
                        next.Type == TokenType.Function ||
                        next.Type == TokenType.LeftParen)
                    {
                        needsMultiply = true;
                    }
                }

                if (needsMultiply)
                {
                    result.Add(new Token(TokenType.Multiply, "*"));
                }
            }
        }

        _tokens = result;
    }

    private Token ReadNumber()
    {
        var sb = new StringBuilder();

        while (_position < _input.Length &&
               (char.IsDigit(_input[_position]) || _input[_position] == '.'))
        {
            sb.Append(_input[_position]);
            _position++;
        }

        return new Token(TokenType.Number, sb.ToString());
    }

    private Token ReadIdentifier()
    {
        var sb = new StringBuilder();

        while (_position < _input.Length &&
               (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
        {
            sb.Append(_input[_position]);
            _position++;
        }

        string identifier = sb.ToString().ToLower();

        var functions = new HashSet<string>
    {
        "sin", "cos", "tan", "cot", "sec", "csc",
        "asin", "acos", "atan",
        "sinh", "cosh", "tanh",
        "asinh", "acosh", "atanh",
        "exp", "ln", "log", "log10",
        "sqrt", "abs",
        "erf", "erfc", "sgn", "heaviside",
        "si", "ci", "ei", "li",
        "fresnels", "fresnelc",
        "integral", "int", "d"
    };

        if (identifier == "integral" || identifier == "int")
            return new Token(TokenType.Integral, identifier);

        if (functions.Contains(identifier))
            return new Token(TokenType.Function, identifier);

        return new Token(TokenType.Variable, sb.ToString());
    }
}
