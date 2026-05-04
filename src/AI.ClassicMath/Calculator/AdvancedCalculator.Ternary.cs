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

public partial class AdvancedCalculator
{
    private object EvaluateExpression(string expression, ExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // УЛУЧШЕНИЕ 2: Поддержка тернарного оператора (? :)
        // Проверяем есть ли тернарный оператор НА ВЕРХНЕМ УРОВНЕ (не в скобках)
        if (HasTopLevelTernary(expression))
        {
            return EvaluateTernary(expression, context, cancellationToken);
        }

        // НОВОЕ: Пре-обработка скобочных выражений с тернарным
        // Если в expression есть (expr ? a : b), заменяем его на результат
        expression = PreprocessTernaryInGroups(expression, context, cancellationToken);

        // ИСПРАВЛЕНИЕ: Обработка тернарных в аргументах функций
        expression = PreprocessTernaryInFunctions(expression, context, cancellationToken);

        var tokens = Tokenize(expression, cancellationToken);
        var rpn = ConvertToRpn(tokens, context, cancellationToken);
        return EvaluateRpn(rpn, context, cancellationToken);
    }

/// <summary>
/// Пре-обработка скобочных выражений с тернарным оператором
/// (1 > 0 ? 5 : 10) * 2 -> 5 * 2
/// </summary>
private string PreprocessTernaryInGroups(string expression, ExecutionContext context, CancellationToken cancellationToken)
{
    while (true)
    {
        int parenDepth = 0;
        int startPos = -1;
        int endPos = -1;
        bool hasTernary = false;

        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];

            if (c == '(')
            {
                if (parenDepth == 0) startPos = i;
                parenDepth++;
            }
            else if (c == ')')
            {
                parenDepth--;
                if (parenDepth == 0)
                {
                    endPos = i;
                    // Проверяем есть ли ? внутри этих скобок
                    string innerExpr = expression.Substring(startPos + 1, endPos - startPos - 1);
                    if (innerExpr.Contains("?"))
                    {
                        // ИСПРАВЛЕНИЕ: Проверяем, это функция или просто скобки?
                        // Если перед ( идёт имя (буквы/цифры), то это функция — НЕ обрабатываем здесь!
                        bool isFunctionCall = false;
                        if (startPos > 0)
                        {
                            int j = startPos - 1;
                            // Пропускаем пробелы
                            while (j >= 0 && char.IsWhiteSpace(expression[j])) j--;
                            // Если перед ( есть буквы/цифры — это функция
                            if (j >= 0 && (char.IsLetterOrDigit(expression[j]) || expression[j] == '_'))
                                isFunctionCall = true;
                        }

                        if (!isFunctionCall)
                        {
                            hasTernary = true;
                            break;
                        }
                    }
                }
            }
        }

        if (!hasTernary || startPos == -1 || endPos == -1)
            break; // Нет тернарных в скобках (или все скобки — это функции)

        // Вычисляем выражение внутри скобок
        string innerExpr2 = expression.Substring(startPos + 1, endPos - startPos - 1);
        var result = EvaluateExpression(innerExpr2, context, cancellationToken);

        // Форматируем результат как строку для подстановки
        string resultStr;
        if (result is Complex complexVal)
        {
            if (Math.Abs(complexVal.Imaginary) < 1e-10) // Реальное число
                resultStr = complexVal.Real.ToString(CultureInfo.InvariantCulture);
            else // Комплексное число
                resultStr = $"({complexVal.Real.ToString(CultureInfo.InvariantCulture)}+{complexVal.Imaginary.ToString(CultureInfo.InvariantCulture)}*i)";
        }
        else if (result is string strVal)
        {
            resultStr = $"\"{strVal}\""; // Строки в кавычках
        }
        else
        {
            resultStr = result.ToString();
        }

        // Заменяем (expr) на result
        expression = expression.Substring(0, startPos) + resultStr + expression.Substring(endPos + 1);
    }

    return expression;
}

/// <summary>
/// Обрабатывает тернарные операторы внутри аргументов функций
/// abs(1 > 0 ? -5 : 5) -> abs(-5)
/// </summary>
private string PreprocessTernaryInFunctions(string expression, ExecutionContext context, CancellationToken cancellationToken)
{
    // Ищем функции вида funcName(...)
    var regex = new Regex(@"(\w+)\s*\(");
    var matches = regex.Matches(expression);

    if (matches.Count == 0)
        return expression; // Нет функций

    // Обрабатываем каждую функцию
    foreach (Match match in matches)
    {
        var funcName = match.Groups[1].Value;
        var openParenIndex = match.Index + match.Length - 1; // Индекс '('

        // Проверяем, это функция или просто переменная со скобкой
        if (!Functions.ContainsKey(funcName))
            continue;

        // Находим закрывающую скобку
        int parenDepth = 0;
        int closeParenIndex = -1;
        for (int i = openParenIndex; i < expression.Length; i++)
        {
            if (expression[i] == '(') parenDepth++;
            else if (expression[i] == ')')
            {
                parenDepth--;
                if (parenDepth == 0)
                {
                    closeParenIndex = i;
                    break;
                }
            }
        }

        if (closeParenIndex == -1)
            continue; // Не нашли закрывающую скобку

        // Извлекаем аргументы
        string argsString = expression.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1);

        // Проверяем есть ли тернарный в аргументах
        if (!argsString.Contains("?"))
            continue;

        // Разбиваем на аргументы (учитывая вложенные скобки)
        var args = SplitFunctionArguments(argsString);

        // Обрабатываем каждый аргумент
        bool changed = false;
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].Contains("?"))
            {
                try
                {
                    // Вычисляем тернарный в аргументе
                    var result = EvaluateTernary(args[i], context, cancellationToken);
                    args[i] = FormatComplexResult(result);
                    changed = true;
                }
                catch
                {
                    // Если не удалось обработать - оставляем как есть
                }
            }
        }

        if (changed)
        {
            // Собираем функцию обратно
            string newFuncCall = funcName + "(" + string.Join(", ", args) + ")";
            expression = expression.Substring(0, match.Index) + newFuncCall + expression.Substring(closeParenIndex + 1);

            // Рекурсивно обрабатываем дальше (могут быть ещё функции)
            return PreprocessTernaryInFunctions(expression, context, cancellationToken);
        }
    }

    return expression;
}

/// <summary>
/// Разбивает строку аргументов функции на отдельные аргументы (учитывая вложенные скобки)
/// </summary>
private List<string> SplitFunctionArguments(string argsString)
{
    var args = new List<string>();
    int parenDepth = 0;
    int start = 0;

    for (int i = 0; i < argsString.Length; i++)
    {
        if (argsString[i] == '(') parenDepth++;
        else if (argsString[i] == ')') parenDepth--;
        else if (argsString[i] == ',' && parenDepth == 0)
        {
            args.Add(argsString.Substring(start, i - start).Trim());
            start = i + 1;
        }
    }

    // Добавляем последний аргумент
    if (start < argsString.Length)
        args.Add(argsString.Substring(start).Trim());

    return args;
}

/// <summary>
/// Форматирует результат вычисления для подстановки в выражение
/// </summary>
private string FormatComplexResult(object result)
{
    if (result is Complex complexVal)
    {
        if (Math.Abs(complexVal.Imaginary) < 1e-10) // Реальное число
            return complexVal.Real.ToString(CultureInfo.InvariantCulture);
        else // Комплексное число
            return $"({complexVal.Real.ToString(CultureInfo.InvariantCulture)}+{complexVal.Imaginary.ToString(CultureInfo.InvariantCulture)}*i)";
    }
    else if (result is string strVal)
    {
        return $"\"{strVal}\""; // Строки в кавычках
    }
    else
    {
        return result.ToString();
    }
}

/// <summary>
/// Проверяет есть ли тернарный оператор на верхнем уровне (вне скобок)
/// ИЗМЕНЕНИЕ: Теперь ищет '?' НА ЛЮБОЙ ГЛУБИНЕ, потому что
/// выражение (1 > 0 ? 5 : 10) * 2 тоже нужно обработать!
/// </summary>
private bool HasTopLevelTernary(string expression)
{
    int parenDepth = 0;
    int bracketDepth = 0;
    bool inString = false;
    int topLevelQuestionPos = -1;

    for (int i = 0; i < expression.Length; i++)
    {
        char c = expression[i];

        if (c == '"' && (i == 0 || expression[i - 1] != '\\'))
            inString = !inString;

        if (inString) continue;

        if (c == '(') parenDepth++;
        else if (c == ')') parenDepth--;
        else if (c == '[') bracketDepth++;
        else if (c == ']') bracketDepth--;
        else if (c == '?')
        {
            // Ищем ПЕРВЫЙ '?' на НУЛЕВОМ уровне скобок
            if (parenDepth == 0 && bracketDepth == 0 && topLevelQuestionPos == -1)
            {
                topLevelQuestionPos = i;
                return true; // Есть тернарный оператор на верхнем уровне
            }
        }
    }

    return false;
}

/// <summary>
/// Обрабатывает тернарный оператор: condition ? trueValue : falseValue
/// ПОДДЕРЖИВАЕТ вложенные тернарные и тернарные в скобках!
/// </summary>
private object EvaluateTernary(string expression, ExecutionContext context, CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();

    // Найти самый внешний ? (не внутри скобок)
    int questionPos = -1;
    int colonPos = -1;
    int parenDepth = 0;
    int bracketDepth = 0;
    bool inString = false;
    int nestedTernaryCount = 0; // Счётчик вложенных ? после первого

    for (int i = 0; i < expression.Length; i++)
    {
        char c = expression[i];

        if (c == '"' && (i == 0 || expression[i - 1] != '\\'))
            inString = !inString;

        if (inString) continue;

        if (c == '(') parenDepth++;
        else if (c == ')') parenDepth--;
        else if (c == '[') bracketDepth++;
        else if (c == ']') bracketDepth--;
        else if (parenDepth == 0 && bracketDepth == 0)
        {
            if (c == '?')
            {
                if (questionPos == -1)
                {
                    questionPos = i; // Первый ? на верхнем уровне
                }
                else
                {
                    nestedTernaryCount++; // Это вложенный тернарный
                }
            }
            else if (c == ':' && questionPos != -1)
            {
                if (nestedTernaryCount > 0)
                {
                    nestedTernaryCount--; // Это : для вложенного тернарного
                }
                else
                {
                    colonPos = i; // Нашли соответствующую :
                    break;
                }
            }
        }
    }

    if (questionPos == -1 || colonPos == -1)
        throw new ArgumentException("Некорректный тернарный оператор: отсутствует '?' или ':'");

    var conditionExpr = expression.Substring(0, questionPos).Trim();
    var trueExpr = expression.Substring(questionPos + 1, colonPos - questionPos - 1).Trim();
    var falseExpr = expression.Substring(colonPos + 1).Trim();

    var conditionResult = EvaluateExpression(conditionExpr, context, cancellationToken);
    var conditionValue = CastsVar.CastToDouble(conditionResult, "ternary");

    if (Math.Abs(conditionValue) > 1e-10) // true
        return EvaluateExpression(trueExpr, context, cancellationToken);
    else // false
        return EvaluateExpression(falseExpr, context, cancellationToken);
}
}
