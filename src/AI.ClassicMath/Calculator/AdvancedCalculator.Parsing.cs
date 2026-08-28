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
    /// <summary>
    /// Метка «эта скобка открывает ВЗЯТИЕ ЭЛЕМЕНТА», а не литерал списка.
    /// </summary>
    /// <remarks>
    /// На вид скобки одинаковы: и <c>[1, 2, 3]</c>, и <c>a[0]</c> начинаются с '['. Различает их
    /// предыдущий токен, и решение надо запомнить до закрывающей скобки — иначе она не знает,
    /// собирать список или брать элемент. Символ выбран такой, какого в языке нет.
    /// </remarks>
    private const string IndexMarker = "@";

    private List<string> Tokenize(string expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Примечание: Комментарии (#) удаляются в Processor.PreprocessScript
        // перед токенизацией с правильной обработкой строковых литералов.
        // Здесь их удалять НЕ нужно, так как это может повредить # внутри строк.

        // Сначала защищаем строковые литералы от замены ключевых слов
        // Паттерн поддерживает escaped кавычки: "say \"hello\""
        var stringLiterals = new List<string>();
        var strProtectPattern = @"""(?:[^""\\]|\\.)*""";
        expression = Regex.Replace(expression, strProtectPattern, m =>
        {
            stringLiterals.Add(m.Value);
            return $"__TKSTR_{stringLiterals.Count - 1}__";
        });

        // Заменяем логические операторы на символы (строки уже защищены)
        expression = Regex.Replace(expression, @"\band\b", "&&", RegexOptions.IgnoreCase);
        expression = Regex.Replace(expression, @"\bor\b", "||", RegexOptions.IgnoreCase);
        expression = Regex.Replace(expression, @"\bnot\b", "!", RegexOptions.IgnoreCase);

        // Восстанавливаем строковые литералы
        for (int si = 0; si < stringLiterals.Count; si++)
            expression = expression.Replace($"__TKSTR_{si}__", stringLiterals[si]);

        // Токенизация: извлекаем строковые литералы (с поддержкой escaped кавычек),
        // числа (включая научную нотацию), идентификаторы, операторы
        // Идентификатор — буквы ЛЮБОГО алфавита: имена данных и результатов даёт не программист,
        // а тот, кто их подаёт, и «часы» с «итого» латиницей никто писать не станет. До этого
        // кириллическое имя не попадало в токен-идентификатор и разбор падал на «неизвестный токен».
        var pattern = @"(""(?:[^""\\]|\\.)*"")|([0-9]+\.?[0-9]*(?:[eE][+-]?[0-9]+)?|[0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)|([\p{L}_][\p{L}\p{N}_]*)|(<<|>>|>=|<=|==|!=|&&|\|\|)|(.)";
        var tokens = Regex.Matches(expression, pattern).Cast<Match>()
            .Where(m => !string.IsNullOrWhiteSpace(m.Value))
            .Select(m => m.Value).ToList();

        for (int i = 0; i < tokens.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (tokens[i] == "-")
            {
                bool isUnary = (i == 0) || Operators.ContainsKey(tokens[i - 1]) || "([,".Contains(tokens[i - 1]);
                if (isUnary)
                {
                    // УЛУЧШЕНИЕ 6: Объединяем унарный минус с числом
                    // Вместо: ["-", "5"] -> ["~", "5"]
                    // Делаем: ["-", "5"] -> ["-5"]
                    if (i + 1 < tokens.Count && double.TryParse(tokens[i + 1], NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    {
                        tokens[i] = "-" + tokens[i + 1]; // Объединяем
                        tokens.RemoveAt(i + 1); // Удаляем следующий токен
                    }
                    else
                    {
                        tokens[i] = "~"; // Оставляем ~ для других случаев (например ~x)
                    }
                }
            }
            else if (tokens[i] == "!")
            {
                // Проверяем, это логическое НЕ или != (не равно)
                if (i + 1 < tokens.Count && tokens[i + 1] == "=")
                {
                    // Это !=, объединяем
                    tokens[i] = "!=";
                    tokens.RemoveAt(i + 1);
                }
                else
                {
                    // Это логическое НЕ (унарный оператор)
                    // Оставляем как есть
                }
            }
        }
        return tokens;
    }

    private Queue<string> ConvertToRpn(List<string> tokens, ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var outputQueue = new Queue<string>();
        var operatorStack = new Stack<string>();
        var argCountStack = new Stack<int>();

        // Для тернарного оператора нужно отслеживать ?
        var ternaryStack = new Stack<int>(); // Позиции в outputQueue где нужно вставить результат

        for (int i = 0; i < tokens.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var token = tokens[i];
            if (IsValue(token, context))
            {
                outputQueue.Enqueue(token);

                // УЛУЧШЕНИЕ 5 (исправлено): Отслеживаем первый элемент в массиве
                // Увеличиваем счётчик только если это первый элемент (count == 0)
                if (operatorStack.Count > 0 && operatorStack.Peek() == "[" && argCountStack.Count > 0)
                {
                    var currentCount = argCountStack.Peek(); // Peek вместо Pop!
                    if (currentCount == 0) // Первый элемент - устанавливаем count = 1
                    {
                        argCountStack.Pop();
                        argCountStack.Push(1);
                    }
                }
            }
            else if (TryGetFunction(token, context, out _))
            {
                operatorStack.Push(token);
                argCountStack.Push(1);

                // ИСПРАВЛЕНИЕ: Если функция - это первый элемент массива, увеличиваем счетчик массива
                // Проверяем стек на уровень выше (под функцией может быть массив "[")
                if (operatorStack.Count > 1)
                {
                    // Копируем стек чтобы заглянуть ниже
                    var stackArray = operatorStack.ToArray();
                    // stackArray[0] - это только что добавленная функция
                    // stackArray[1] - это то что было до функции
                    if (stackArray.Length > 1 && stackArray[1] == "[" && argCountStack.Count > 1)
                    {
                        // Под функцией - массив, проверяем его счетчик
                        var argCountArray = argCountStack.ToArray();
                        // argCountArray[0] - счетчик аргументов функции (=1)
                        // argCountArray[1] - счетчик элементов массива
                        if (argCountArray.Length > 1 && argCountArray[1] == 0)
                        {
                            // Первый элемент массива - функция, увеличиваем счетчик
                            var funcArgCount = argCountStack.Pop();
                            var arrayCount = argCountStack.Pop();
                            argCountStack.Push(1); // Массив: первый элемент
                            argCountStack.Push(funcArgCount); // Функция: 1 аргумент
                        }
                    }
                }
            }
            else if (token == ",")
            {
                while (operatorStack.Count > 0 && !"([@".Contains(operatorStack.Peek()))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    outputQueue.Enqueue(operatorStack.Pop());
                }
                if (operatorStack.Count > 0 && "([".Contains(operatorStack.Peek()))
                {
                    // УЛУЧШЕНИЕ 4: Правильный подсчет элементов в массиве/функции
                    if (argCountStack.Count > 0)
                    {
                        var currentCount = argCountStack.Pop();
                        argCountStack.Push(currentCount + 1);
                    }
                }
                else throw new ArgumentException("Лишняя запятая или запятая вне вызова функции/вектора.");
            }
            else if (token == "~")
            {
                operatorStack.Push(token);
            }
            else if (token == "!")
            {
                // Логическое НЕ (унарное)
                operatorStack.Push(token);
            }
            else if (Operators.ContainsKey(token))
            {
                while (operatorStack.Count > 0 && Operators.TryGetValue(operatorStack.Peek(), out
                    var op2) && (op2.Precedence > Operators[token].Precedence || (op2.Precedence == Operators[token].Precedence && op2.Associativity == "Left")))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var op = operatorStack.Pop();
                    if (op != "?:" && op != "?") // Не выталкиваем тернарный оператор
                        outputQueue.Enqueue(op);
                }

                operatorStack.Push(token);
            }
            else if ("([".Contains(token))
            {
                if (i > 0 && token == "(" && context.Memory.ContainsKey(tokens[i - 1]) && !TryGetFunction(tokens[i - 1], context, out _)) throw new ArgumentException($"Переменная '{tokens[i - 1]}' не является функцией.");

                if (token == "[" && i > 0 && IsIndexTarget(tokens[i - 1], context))
                {
                    // a[0], f(x)[1], [1, 2][0] — взятие элемента, а не новый список.
                    operatorStack.Push(IndexMarker);
                }
                else
                {
                    operatorStack.Push(token);
                    if (token == "[")
                        argCountStack.Push(0); // УЛУЧШЕНИЕ 3: Начинаем с 0 для поддержки пустых массивов
                }
            }
            else if (")]".Contains(token))
            {
                var indexing = token == "]" && ClosesIndex(operatorStack);
                string openBracket = token == ")" ? "(" : indexing ? IndexMarker : "[";

                // Проверка на пустой массив []
                bool isEmptyArray = false;
                if (token == "]" && i > 0 && tokens[i - 1] == "[")
                {
                    isEmptyArray = true;
                }

                while (operatorStack.Count > 0 && operatorStack.Peek() != openBracket)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    outputQueue.Enqueue(operatorStack.Pop());
                }

                if (operatorStack.Count == 0)
                    throw new ArgumentException($"Отсутствует парная открывающая скобка '{openBracket}'.");
                operatorStack.Pop();

                if (indexing)
                {
                    // Взятие элемента — это обычный вызов index(список, номер).
                    outputQueue.Enqueue("index_2");
                }
                else if (token == ")" && operatorStack.Count > 0 && TryGetFunction(operatorStack.Peek(), context, out _))
                {
                    // f() — вызов без аргументов. Счётчик заводится на единице (первый аргумент
                    // ожидается всегда), поэтому пустые скобки надо распознать отдельно: иначе
                    // функция без параметров просит один и не находит его в стеке.
                    var withoutArguments = i > 0 && tokens[i - 1] == "(";
                    var count = argCountStack.Any() ? argCountStack.Pop() : 1;

                    outputQueue.Enqueue($"{operatorStack.Pop()}_{(withoutArguments ? 0 : count)}");
                }
                else if (token == "]")
                {
                    int count = 0;
                    if (isEmptyArray)
                    {
                        count = 0; // Пустой массив
                        if (argCountStack.Any()) argCountStack.Pop(); // Убираем счетчик
                    }
                    else if (argCountStack.Any())
                    {
                        count = argCountStack.Pop();
                        // Если был хотя бы один элемент, count должен быть минимум 1
                        if (count == 0) count = 1;
                    }
                    else
                    {
                        count = 1; // По умолчанию 1 элемент если нет стека
                    }

                    // ОТЛАДКА: считаем сколько элементов реально в outputQueue после последнего [
                    // Проблема может быть в том, что count не соответствует реальному количеству элементов

                    outputQueue.Enqueue($"vector_{count}");
                }
            }
            else
            {
                throw new ArgumentException($"Неизвестный токен или синтаксическая ошибка: '{token}'");
            }
        }
        while (operatorStack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var op = operatorStack.Pop();
            if ("([@".Contains(op)) throw new ArgumentException($"Отсутствует парная закрывающая скобка.");
            outputQueue.Enqueue(op);
        }
        return outputQueue;
    }

    /// <summary>После чего '[' означает взятие элемента, а не начало списка.</summary>
    private bool IsIndexTarget(string token, ExecutionContext context) =>
        token == ")" || token == "]" || (IsValue(token, context) && !TryGetFunction(token, context, out _));

    /// <summary>Что закрывает ']': ближайшая незакрытая '[' или маркер индексации.</summary>
    private static bool ClosesIndex(Stack<string> operatorStack)
    {
        foreach (var op in operatorStack)
        {
            if (op == IndexMarker) return true;
            if (op == "[") return false;
        }

        return false;
    }
}
