using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Complex = System.Numerics.Complex;

namespace AI.ClassicMath.Calculator;

/// <summary>
/// Хранит состояние сессии калькулятора, в первую очередь - память о переменных.
/// </summary>
public class ExecutionContext
{
    /// <summary>
    /// Потолок шагов интерпретатора по умолчанию: страховка от незавершающегося цикла.
    /// </summary>
    /// <remarks>
    /// Таймаут вызывающего такую защиту не заменяет. Он обрывает скрипт молча и по часам, а за
    /// вычислителем ждёт живой диалог: там нужна причина («цикл не завершается»), а не пауза.
    /// </remarks>
    public const int DefaultStepLimit = 1_000_000;

    /// <summary>
    /// Предел вложенности вызовов функций скрипта.
    /// </summary>
    /// <remarks>
    /// Это не удобство, а защита процесса: тело функции исполняется рекурсией по стеку CLR, а
    /// <c>StackOverflowException</c> в .NET не перехватывается — незавершающаяся рекурсия в
    /// скрипте убила бы весь хост. Потолок шагов здесь не спасает: до него дело не дойдёт.
    /// </remarks>
    public const int MaxCallDepth = 64;

    /// <summary>Корневой контекст прогона; <c>null</c> у него самого.</summary>
    private readonly ExecutionContext _root;

    private int _steps;

    /// <summary>
    /// Словарь для хранения переменных.
    /// Теперь регистрозависимый - M и m это разные переменные!
    /// </summary>
    public Dictionary<string, object> Memory { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Функции, объявленные самим скриптом (<c>def</c>).
    /// </summary>
    /// <remarks>
    /// Отдельно от набора калькулятора: набор — это то, что дал хост, и переживает прогон, а
    /// объявленное скриптом живёт ровно один прогон и не должно протекать в следующий.
    /// </remarks>
    public Dictionary<string, FunctionDefinition> UserFunctions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Именованные результаты прогона (<c>emit</c>).
    /// </summary>
    /// <remarks>
    /// Вывод скрипта — транскрипт для человека и модели, из него числа приходится вычитывать
    /// текстом. Сюда же кладётся то, что скрипт объявил результатом: подстановка в документ и
    /// приёмка числами берут значения отсюда, а не разбором строк.
    /// </remarks>
    public Dictionary<string, object> Emitted { get; } = new(StringComparer.Ordinal);

    /// <summary>Отмена прогона: её видят и тела функций, а не только внешний цикл.</summary>
    public CancellationToken Cancellation { get; set; }

    /// <summary>Потолок шагов; ноль и меньше — без потолка.</summary>
    public int StepLimit { get; set; } = DefaultStepLimit;

    /// <summary>Глубина вложенности вызовов функций скрипта.</summary>
    public int Depth { get; }

    /// <summary>
    /// Значение последнего вычисленного выражения.
    /// </summary>
    /// <remarks>
    /// Нужно функциям скрипта: тело без <c>return</c> отдаёт то, что посчитала последняя
    /// строка. Требовать <c>return</c> нельзя — функция, которая только пишет в <c>emit</c>,
    /// тоже осмысленна, а возвращать из неё «ничего» модель потом сложит с числом.
    /// </remarks>
    public object LastValue { get; set; }

    /// <summary>Сколько шагов интерпретатора уже сделано за прогон.</summary>
    public int Steps => _root?.Steps ?? _steps;

    public ExecutionContext()
    {
        Memory["pi"] = Math.PI;
        Memory["e"] = Math.E;
        Memory["phi"] = 1.61803398874989;
    }

    /// <summary>
    /// Контекст вызова функции: своя память, общие с прогоном счётчик шагов, объявления и итоги.
    /// </summary>
    /// <remarks>
    /// Память копируется, а не разделяется: тело функции видит уже посчитанное снаружи, но его
    /// присваивания наружу не протекают. Иначе функция-проверка меняла бы данные, которые
    /// проверяет, и результат зависел бы от порядка вызовов.
    /// <para>
    /// Счётчик шагов, наоборот, общий: свой у каждого вызова означал бы, что рекурсия обходит
    /// потолок, обнуляя его на каждом уровне.
    /// </para>
    /// </remarks>
    public ExecutionContext(ExecutionContext parent)
    {
        if (parent == null) throw new ArgumentNullException(nameof(parent));

        _root = parent._root ?? parent;
        Depth = parent.Depth + 1;
        StepLimit = _root.StepLimit;
        Cancellation = parent.Cancellation;
        UserFunctions = parent.UserFunctions;
        Emitted = _root.Emitted;

        foreach (var variable in parent.Memory) Memory[variable.Key] = variable.Value;
    }

    /// <summary>
    /// Считает шаг интерпретатора и обрывает выполнение при выходе за потолок.
    /// </summary>
    /// <remarks>
    /// Шагом считается вычисление одного выражения: тело цикла, его условие и приращение. Так
    /// потолок ограничивает именно работу, а не длину исходника.
    /// </remarks>
    public void CountStep()
    {
        if (_root != null)
        {
            _root.CountStep();
            return;
        }

        if (StepLimit > 0 && ++_steps > StepLimit)
            throw new InvalidOperationException(
                $"Скрипт превысил потолок в {StepLimit} шагов: цикл не завершается либо задача слишком велика для вычислителя.");
    }

    /// <summary>
    /// Кладёт в память данные, подготовленные ВЫЗЫВАЮЩИМ.
    /// </summary>
    /// <remarks>
    /// Пока данные попадали в скрипт единственным способом — модель вписывала числа в его
    /// текст, — точность вычислителя не спасала: колонку из сорока цен она переносила руками,
    /// и терялась цифра при переносе, а не при счёте.
    /// <para>
    /// Имена, непригодные в качестве переменной (с пробелом, со знаком), молча пропускаются:
    /// имя данным даёт не программист, а тот, кто их подаёт, и ронять из-за этого прогон
    /// незачем. Тип, который язык не умеет, наоборот, отвергается сразу — это ошибка хоста.
    /// </remarks>
    public void Seed(IReadOnlyDictionary<string, object> values)
    {
        if (values == null) return;

        foreach (var pair in values)
        {
            if (!IsIdentifier(pair.Key)) continue;

            Memory[pair.Key] = ToScriptValue(pair.Key, pair.Value);
        }
    }

    public void AddDoubleConstant(string constantName, double constant)
    {
        if (!Memory.ContainsKey(constantName))
            Memory.Add(constantName, constant);
        else Memory[constantName] = constant;
    }

    /// <summary>
    /// Загрузка списка скалярных констант
    /// </summary>
    /// <param name="constants">Строка вида "name1=value1\nname2=value2"</param>
    public void AddDoubleConstants(string constants)
    {
        constants = constants.Replace(" ", "");
        List<string> constsArray = constants.Split('\n').ToList();

        foreach (string constant in constsArray)
        {
            if (string.IsNullOrWhiteSpace(constant)) continue;
            var nameValue = constant.Split('=');
            if (nameValue.Length < 2) continue;

            var isValue = double.TryParse(nameValue[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var numValue);
            if (isValue) AddDoubleConstant(nameValue[0], numValue);
        }
    }

    /// <summary>Список чисел в виде значения языка.</summary>
    private static ComplexVector Numbers(IEnumerable<double> numbers) =>
        new(numbers.Select(number => new Complex(number, 0)));

    /// <summary>Годится ли имя в переменные скрипта.</summary>
    private static bool IsIdentifier(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && (char.IsLetter(name[0]) || name[0] == '_')
        && name.All(symbol => char.IsLetterOrDigit(symbol) || symbol == '_');

    /// <summary>
    /// Переводит значение хоста в значение языка.
    /// </summary>
    /// <remarks>
    /// Список типов короткий намеренно: в языке есть числа, строки, даты и списки — всё
    /// остальное пришлось бы печатать неизвестно как. Неподдерживаемый тип — ошибка того, кто
    /// подаёт данные, и молчать о ней значит отдать скрипту «System.Object[]» вместо колонки.
    /// </remarks>
    private static object ToScriptValue(string name, object value)
    {
        switch (value)
        {
            case null: return 0.0;
            case string text: return text;
            case DateTime moment: return moment;
            case bool flag: return flag ? 1.0 : 0.0;
            case double number: return number;
            case float number: return (double)number;
            case decimal number: return (double)number;
            case int number: return (double)number;
            case long number: return (double)number;
            case Complex complex: return complex;
            case Vector vector: return vector;
            case ComplexVector vector: return vector;
            case string[] strings: return strings;
            case IEnumerable<string> strings: return strings.ToArray();
            // Комплексным вектором, как и литерал [1, 2, 3]: список от хоста обязан вести себя
            // как обычный список языка.
            case IEnumerable<double> numbers: return Numbers(numbers);
            case IEnumerable<int> numbers: return Numbers(numbers.Select(number => (double)number));
            case IEnumerable<decimal> numbers: return Numbers(numbers.Select(number => (double)number));

            default:
                throw new ArgumentException(
                    $"Данные '{name}': тип {value.GetType().Name} не переводится в значение вычислителя. " +
                    "Годятся число, строка, дата и список чисел либо строк.");
        }
    }
}
