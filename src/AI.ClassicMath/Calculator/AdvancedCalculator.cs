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
        var baseOperators = new LibOperatorsBase();

        Operators = baseOperators.GetOperators();
        OperationsFunctions = baseOperators.GetOperationsFunctions();
        Functions = new Dictionary<string, FunctionDefinition>(StringComparer.OrdinalIgnoreCase);

        Use(new BaseMathLib());
        Use(new EquationLib());
    }

    #endregion


public object Evaluate(string expression, ExecutionContext context, CancellationToken cancellationToken = default)
{
    try
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.CountStep();

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

        // Присваивание элементу списка: a[i] = v. Отдельной веткой, потому что цель здесь не
        // имя, а место в списке, и общий разбор присваивания такую цель отвергает.
        var elementMatch = Regex.Match(expression, @"^(\w+)\s*\[(.+)\]\s*=(?!=)\s*(.+)$");
        if (elementMatch.Success)
        {
            var listName = elementMatch.Groups[1].Value;

            if (!context.Memory.TryGetValue(listName, out var list))
                throw new InvalidOperationException($"Список '{listName}' не определён — присваивать элемент нечему.");

            var position = CastsVar.CastToInt32(
                EvaluateExpression(elementMatch.Groups[2].Value, context, cancellationToken), listName);
            var element = EvaluateExpression(elementMatch.Groups[3].Value, context, cancellationToken);

            context.Memory[listName] = ListOps.SetAt(list, position, element, listName);
            return element;
        }

        // Ищем оператор присваивания '=' ВНЕ строковых литералов
        // (пропускаем ==, !=, <=, >=, +=, -=, *=, /=, %=, ^= — они обработаны выше)
        var assignIdx = FindAssignmentEqualsIndex(expression);
        if (assignIdx > 0)
        {
            var varName = expression.Substring(0, assignIdx).Trim();

            // Присваивание имени, занятому функцией. Без этой ветки разбор доходил до места, где
            // '=' уже не при чём, и отвечал «неизвестный токен: =» — по такому сообщению не
            // догадаться, что дело в имени. Функций в наборе теперь под сотню, и обычные слова
            // (limit, rows, check, solve) среди них.
            if (IsNameShape(varName) && TryGetFunction(varName, context, out _))
                throw new InvalidOperationException(
                    $"Имя '{varName}' занято функцией вычислителя — назовите переменную иначе.");

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

    /// <summary>
    /// Подключает библиотеку функций к калькулятору.
    /// </summary>
    /// <remarks>
    /// Состав функций — решение ВЫЗЫВАЮЩЕГО, а не калькулятора: тяжёлая математика (символьные
    /// преобразования, матричные разложения) живёт в проектах, которые сами ссылаются на этот,
    /// и попасть внутрь конструктора не может — получилась бы циклическая ссылка.
    /// <para>
    /// Совпадающие имена перекрываются поздней библиотекой: хост, дающий свою реализацию
    /// функции, должен иметь возможность заменить базовую.
    /// </para>
    /// </remarks>
    public AdvancedCalculator Use(IMathLib library)
    {
        if (library == null) throw new ArgumentNullException(nameof(library));

        foreach (var function in library.GetFunctions())
            Functions[function.Key] = function.Value;

        return this;
    }

    /// <summary>
    /// Функция набора либо объявленная скриптом.
    /// </summary>
    /// <remarks>
    /// Один поиск на всех: разбор выражения решает по нему, что идентификатор — вызов, а не
    /// переменная, и вычисление берёт по нему тело. Разъехавшись, эти двое дали бы «неизвестный
    /// токен» на функцию, которая объявлена.
    /// </remarks>
    private bool TryGetFunction(string name, ExecutionContext context, out FunctionDefinition definition)
    {
        if (Functions.TryGetValue(name, out definition)) return true;
        if (context != null && context.UserFunctions.TryGetValue(name, out definition)) return true;

        definition = null;
        return false;
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
    private bool IsValidVarName(string name) => IsNameShape(name) && !Functions.ContainsKey(name);

    /// <summary>Похоже ли на имя переменной — без учёта того, занято оно или нет.</summary>
    private static bool IsNameShape(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(c => char.IsLetterOrDigit(c) || c == '_');
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
