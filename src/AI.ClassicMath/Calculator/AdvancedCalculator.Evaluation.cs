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
    private object EvaluateRpn(Queue<string> rpnTokens, ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var evalStack = new Stack<object>();

        // ОТЛАДКА: выводим RPN для диагностики
        var rpnList = rpnTokens.ToList();
        // Console.WriteLine($"RPN: {string.Join(" ", rpnList)}");
        rpnTokens = new Queue<string>(rpnList);

        foreach (var token in rpnTokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out
                var num))
            {
                evalStack.Push(new Complex(num, 0));
            }
            else if (token == "i")
            {
                // Проверяем контекст: если 'i' определена как переменная, используем её значение
                // Иначе - это мнимая единица
                if (context.Memory.ContainsKey("i"))
                {
                    evalStack.Push(context.Memory["i"]);
                }
                else
                {
                    evalStack.Push(Complex.ImaginaryOne);
                }
            }
            else if (token.StartsWith("\"") && token.EndsWith("\""))
            {
                // Строковый литерал - убираем кавычки
                // Обрабатываем только escaped кавычки (\"), остальные escape-последовательности
                // сохраняются как есть (калькулятор не интерпретирует \\ \t \n и т.п.)
                var strContent = token.Substring(1, token.Length - 2);
                strContent = strContent.Replace("\\\"", "\"");  // \" -> "
                evalStack.Push(strContent);
            }
            else if (context.Memory.TryGetValue(token, out
                var value))
            {
                evalStack.Push(value);
            }
            else if (token == "~")
            {
                if (evalStack.Count < 1) throw new InvalidOperationException("Недостаточно операндов для унарного минуса.");
                object operand = evalStack.Pop();
                if (operand is Complex c) evalStack.Push(Complex.Negate(c));
                else if (operand is ComplexVector cv) evalStack.Push(cv * -1);
                else if (operand is Vector rv) evalStack.Push(rv * -1);
                else throw new InvalidOperationException($"Унарный минус не применим к типу {operand.GetType().Name}.");
            }
            else if (token == "!")
            {
                // Логическое НЕ
                if (evalStack.Count < 1) throw new InvalidOperationException("Недостаточно операндов для логического НЕ.");
                object operand = evalStack.Pop();
                if (operand is Complex c)
                {
                    evalStack.Push(new Complex(c.Real == 0 ? 1.0 : 0.0, 0));
                }
                else if (operand is double d)
                {
                    evalStack.Push(new Complex(d == 0 ? 1.0 : 0.0, 0));
                }
                else throw new InvalidOperationException($"Логическое НЕ не применимо к типу {operand.GetType().Name}.");
            }
            else if (Operators.ContainsKey(token))
            {
                if (evalStack.Count < 2) throw new InvalidOperationException($"Недостаточно операндов для оператора '{token}'.");
                ApplyOperator(token, evalStack);
            }
            else if (token == "?:")
            {
                // Тернарный оператор: condition ? true_val : false_val
                if (evalStack.Count < 3) throw new InvalidOperationException("Недостаточно операндов для тернарного оператора.");

                var falseVal = evalStack.Pop();
                var trueVal = evalStack.Pop();
                var condition = evalStack.Pop();

                // Проверяем условие
                bool isTrue = false;
                if (condition is Complex c)
                    isTrue = c.Real != 0;
                else if (condition is double d)
                    isTrue = d != 0;

                evalStack.Push(isTrue ? trueVal : falseVal);
            }
            else if (TrySplitCall(token, out var funcName, out var argCount))
            {
                if (argCount == 0 && funcName == "vector")
                {
                    evalStack.Push(new ComplexVector(0));
                    continue;
                }
                if (evalStack.Count < argCount) throw new InvalidOperationException($"Недостаточно операндов в стеке для '{funcName}' (нужно {argCount}, найдено {evalStack.Count}).");
                var args = new object[argCount];
                for (int i = argCount - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    args[i] = evalStack.Pop();
                }
                if (funcName == "vector")
                {
                    // Проверяем: это массив чисел или строк?
                    // Если хотя бы один элемент - строка, создаем массив строк
                    bool hasStrings = args.Any(a => a is string);

                    if (hasStrings)
                    {
                        // Массив строк (все элементы преобразуем в string)
                        evalStack.Push(args.Select(a => a?.ToString() ?? "").ToArray());
                    }
                    else
                    {
                        // Массив чисел (ComplexVector)
                        evalStack.Push(new ComplexVector(args.Select(a => CastsVar.CastToComplex(a, "vector component"))));
                    }
                }
                else
                {
                    if (!TryGetFunction(funcName, context, out var funcDef)) throw new NotSupportedException($"Функция '{funcName}' не найдена.");
                    if (funcDef.ArgumentCount != -1 && funcDef.ArgumentCount != argCount) throw new ArgumentException($"Функция '{funcName}' ожидает {funcDef.ArgumentCount} аргументов, но получила {argCount}.");
                    evalStack.Push(funcDef.Invoke(args, context));
                }
            }
            else
            {
                throw new InvalidOperationException($"Неизвестный токен в RPN: {token}");
            }
        }

        if (evalStack.Count > 1) throw new InvalidOperationException("Ошибка в синтаксисе выражения: в стеке осталось больше одного элемента.");
        return evalStack.Count == 0 ? null : evalStack.Pop();
    }

    /// <summary>
    /// Разбирает токен вызова вида «имя_числоАргументов».
    /// </summary>
    /// <remarks>
    /// Делить надо по ПОСЛЕДНЕМУ подчёркиванию: имя функции само может его содержать, и деление
    /// по первому ломало любой вызов вроде <c>s_nds(1000)</c> — обычное имя, как только функции
    /// стал объявлять сам скрипт.
    /// </remarks>
    private static bool TrySplitCall(string token, out string name, out int argCount)
    {
        name = null;
        argCount = 0;

        var separator = token.LastIndexOf('_');
        if (separator <= 0) return false;

        if (!int.TryParse(token.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out argCount))
            return false;

        name = token.Substring(0, separator);
        return true;
    }
}
