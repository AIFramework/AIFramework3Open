using AI.ClassicMath.Calculator.Libs;
using AI.ClassicMath.Calculator.Libs.Algebra;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Complex = System.Numerics.Complex;
using System.Text.RegularExpressions;
using System.Threading;

namespace AI.ClassicMath.Calculator;

/// <summary>
/// Калькулятор с поддержкой библиотек, векторов и комплексных чисел.
/// </summary>
[Serializable]
public partial class AdvancedCalculator
{
    #region Поля и конструктор

    /// <summary>
    /// Зарегистрированные операторы
    /// </summary>
    public Dictionary<string, (int Precedence, string Associativity)> Operators { get; set; }

    /// <summary>
    /// Операции с этими операторами
    /// </summary>
    public Dictionary<(Type, Type, string), Func<object, object, object>> OperationsFunctions { get; set; }

    /// <summary>
    /// Функции (математические)
    /// </summary>
    public Dictionary<string, FunctionDefinition> Functions { get; set; }


    /// <summary>
    /// Калькулятор с поддержкой библиотек, векторов и комплексных чисел.
    /// </summary>
    public AdvancedCalculator()
    {
        var baseMathLib = new BaseMathLib();
        var eq = new EquationLib();

        var baseOperators = new LibOperatorsBase();

        Operators = baseOperators.GetOperators();
        OperationsFunctions = baseOperators.GetOperationsFunctions();

        Functions = baseMathLib.GetFunctions();

        foreach (var func in eq.GetFunctions())
        {
            Functions.Add(func.Key, func.Value);
        }
    }

    #endregion


public object Evaluate(string expression, ExecutionContext context, CancellationToken cancellationToken = default)
{
    try
    {
        cancellationToken.ThrowIfCancellationRequested();

        expression = expression.Trim();
        if (string.IsNullOrEmpty(expression)) return null;

        // Обработка ++ и --
        var incrementMatch = Regex.Match(expression, @"^(\w+)\+\+$");
        var decrementMatch = Regex.Match(expression, @"^(\w+)--$");
        var preIncrementMatch = Regex.Match(expression, @"^\+\+(\w+)$");
        var preDecrementMatch = Regex.Match(expression, @"^--(\w+)$");

        if (incrementMatch.Success)
        {
            var varName = incrementMatch.Groups[1].Value;
            expression = $"{varName} = {varName} + 1";
        }
        else if (decrementMatch.Success)
        {
            var varName = decrementMatch.Groups[1].Value;
            expression = $"{varName} = {varName} - 1";
        }
        else if (preIncrementMatch.Success)
        {
            var varName = preIncrementMatch.Groups[1].Value;
            expression = $"{varName} = {varName} + 1";
        }
        else if (preDecrementMatch.Success)
        {
            var varName = preDecrementMatch.Groups[1].Value;
            expression = $"{varName} = {varName} - 1";
        }

        // Обработка составных операторов (+=, -=, *=, /=, %=, ^=)
        var compoundMatch = Regex.Match(expression, @"^(\w+)\s*([+\-*/%^])=\s*(.+)$");
        if (compoundMatch.Success)
        {
            var varName = compoundMatch.Groups[1].Value;
            var op = compoundMatch.Groups[2].Value;
            var rightExpr = compoundMatch.Groups[3].Value;
            expression = $"{varName} = {varName} {op} ({rightExpr})";
        }

        // Ищем оператор присваивания '=' ВНЕ строковых литералов
        // (пропускаем ==, !=, <=, >=, +=, -=, *=, /=, %=, ^= — они обработаны выше)
        var assignIdx = FindAssignmentEqualsIndex(expression);
        if (assignIdx > 0)
        {
            var varName = expression.Substring(0, assignIdx).Trim();
            if (IsValidVarName(varName) && IsSimpleAssignmentTarget(expression.Substring(0, assignIdx)))
            {
                var exprToEvaluate = expression.Substring(assignIdx + 1);
                var result = EvaluateExpression(exprToEvaluate, context, cancellationToken);
                context.Memory[varName] = result;
                return result;
            }
        }
        return EvaluateExpression(expression, context, cancellationToken);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(ex.Message, ex);
    }
}

    #region Вспомогательные методы

    /// <summary>
    /// Применяет оператор к двум верхним элементам стека.
    /// </summary>
    private void ApplyOperator(string op, Stack<object> stack)
    {
        if (stack.Count < 2)
        {
            throw new InvalidOperationException($"Недостаточно операндов для оператора '{op}'.");
        }

        var op2 = stack.Pop();
        var op1 = stack.Pop();

        var normalizedOp1 = Normalize(op1);
        var normalizedOp2 = Normalize(op2);

        var key = (normalizedOp1.GetType(), normalizedOp2.GetType(), op);

        if (OperationsFunctions.TryGetValue(key, out var operationFunc))
        {
            object result = operationFunc(normalizedOp1, normalizedOp2);
            stack.Push(result);
        }
        else
        {
            // Если в словаре не нашлось подходящей операции, генерируем исключение
            throw new InvalidOperationException($"Оператор '{op}' не применим к типам {op1.GetType().Name} и {op2.GetType().Name}.");
        }
    }

    private object Normalize(object obj) => obj
    switch
    {
        double d => new Complex(d, 0),
        int i => new Complex(i, 0),
        Vector rv => new ComplexVector(rv.Select(c => new Complex(c, 0)).ToArray()),
        _ => obj
    };
    private bool IsValue(string token, ExecutionContext context) =>
        token == "i" || 
        (token.StartsWith("\"") && token.EndsWith("\"")) ||  // Строковый литерал
        double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out _) || 
        context.Memory.ContainsKey(token);
    private bool IsValidVarName(string name) =>
        !string.IsNullOrWhiteSpace(name) && (char.IsLetter(name[0]) || name[0] == '_') && name.All(c => char.IsLetterOrDigit(c) || c == '_') && !Functions.ContainsKey(name);
    /// <summary>
    /// Находит индекс оператора присваивания '=' вне строковых литералов,
    /// пропуская ==, !=, <=, >=. Возвращает -1 если не найден.
    /// </summary>
    private int FindAssignmentEqualsIndex(string expression)
    {
        bool inString = false;
        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];
            if (c == '"')
            {
                // Проверяем escaped кавычки
                int bs = 0;
                int k = i - 1;
                while (k >= 0 && expression[k] == '\\') { bs++; k--; }
                if (bs % 2 == 0) inString = !inString;
            }
            else if (c == '=' && !inString)
            {
                // Пропускаем ==
                if (i + 1 < expression.Length && expression[i + 1] == '=') { i++; continue; }
                // Пропускаем !=, <=, >=
                if (i > 0 && (expression[i - 1] == '!' || expression[i - 1] == '<' || expression[i - 1] == '>')) continue;
                // Пропускаем +=, -=, *=, /=, %=, ^= (уже обработаны compoundMatch выше)
                if (i > 0 && "+-*/%^".Contains(expression[i - 1])) continue;
                return i;
            }
        }
        return -1;
    }

    private bool IsSimpleAssignmentTarget(string expression) =>
        !expression.Any(c => "()[]<>!".Contains(c));

    #endregion
}
