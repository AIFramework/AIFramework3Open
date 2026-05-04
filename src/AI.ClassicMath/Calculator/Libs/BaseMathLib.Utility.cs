using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.Distances;
using AI.HighLevelFunctions;
using AI.MathUtils.Combinatorics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Complex = System.Numerics.Complex;

namespace AI.ClassicMath.Calculator.Libs;

public partial class BaseMathLib
{
    //================== Bitwise операции ==================

    private static FunctionDefinition CreateXorFunction()
    {
        const string name = "xor";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                int a = (int)CastsVar.CastToDouble(args[0], name);
                int b = (int)CastsVar.CastToDouble(args[1], name);
                return new Complex(a ^ b, 0);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Битовые операции"],
                Description = "Выполняет битовую операцию XOR (исключающее ИЛИ) над двумя целыми числами.",
                Signature = "Вход: 2 целых числа. Выход: 1 число.",
                Example = "xor(5, 3) // Результат: 6 (101 XOR 011 = 110)"
            }
        };
    }

    private static FunctionDefinition CreateBitNotFunction()
    {
        const string name = "bitnot";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args =>
            {
                int a = (int)CastsVar.CastToDouble(args[0], name);
                return new Complex(~a, 0);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Битовые операции"],
                Description = "Выполняет битовую операцию NOT (инверсия всех битов) над целым числом.",
                Signature = "Вход: 1 целое число. Выход: 1 число.",
                Example = "bitnot(5) // Результат: -6 (инверсия битов 101)"
            }
        };
    }

    //================== Строковые операции ==================

    private static FunctionDefinition CreateLenFunction()
    {
        const string name = "len";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args =>
            {
                if (args[0] is string str)
                    return new Complex(str.Length, 0);
                if (args[0] is string[] strArray)
                    return new Complex(strArray.Length, 0);
                if (args[0] is ComplexVector vec)
                    return new Complex(vec.Count, 0);
                throw new ArgumentException($"Функция '{name}' ожидает строку, массив или вектор.");
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Строковые операции"],
                Description = "Возвращает длину строки или вектора.",
                Signature = "Вход: 1 строка или вектор. Выход: 1 число.",
                Example = "len(\"Hello\") // Результат: 5"
            }
        };
    }

    private static FunctionDefinition CreateConcatFunction()
    {
        const string name = "concat";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = -1, // Переменное количество аргументов
            Delegate = args =>
            {
                if (!args.Any()) throw new ArgumentException("Функция 'concat' требует хотя бы один аргумент.");
                return args.Select(ArgToString).Aggregate((a, b) => a + b);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Строковые операции"],
                Description = "Объединяет строки и/или числа в одну строку. Числа автоматически конвертируются в строки.",
                Signature = "Вход: N строк или чисел (смешанно). Выход: 1 строка.",
                Example = "concat(\"Результат: \", 42, \"/\", 100) // Результат: \"Результат: 42/100\""
            }
        };
    }

    /// <summary>
    /// Конвертирует аргумент (число, строку, вектор и т.д.) в строковое представление для concat/join.
    /// Для Complex-чисел возвращает чистое число без формата (12, 0).
    /// </summary>
    private static string ArgToString(object arg)
    {
        if (arg is string s)
            return s;

        if (arg is Complex c)
        {
            if (Math.Abs(c.Imaginary) < 1e-12)
            {
                // Чистое вещественное число
                double d = c.Real;
                if (Math.Abs(d - Math.Round(d)) < 1e-10)
                    return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
                return d.ToString("G15", CultureInfo.InvariantCulture);
            }
            // Комплексное число
            string sign = c.Imaginary >= 0 ? "+" : "";
            return $"{c.Real.ToString("G15", CultureInfo.InvariantCulture)}{sign}{c.Imaginary.ToString("G15", CultureInfo.InvariantCulture)}i";
        }

        if (arg is ComplexVector vec)
            return $"[{string.Join(", ", vec.Select(v => ArgToString(v)))}]";

        if (arg is Vector dv)
            return $"[{string.Join(", ", dv.Select(v => ArgToString((Complex)v)))}]";

        if (arg is DateTime dt)
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        return arg?.ToString() ?? "";
    }

    private static FunctionDefinition CreateSubstrFunction()
    {
        const string name = "substr";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 3,
            Delegate = args =>
            {
                if (!(args[0] is string str))
                    throw new ArgumentException($"Функция '{name}' ожидает строку в качестве первого аргумента.");

                int start = CastsVar.CastToInt32(args[1], name);
                int length = CastsVar.CastToInt32(args[2], name);

                if (start < 0 || start >= str.Length)
                    throw new ArgumentException($"Индекс начала ({start}) выходит за пределы строки.");
                if (length < 0)
                    throw new ArgumentException($"Длина подстроки ({length}) не может быть отрицательной.");

                // ИСПРАВЛЕНИЕ: Обрезаем длину если она выходит за пределы
                if (start + length > str.Length)
                    length = str.Length - start;

                return str.Substring(start, length);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Строковые операции"],
                Description = "Извлекает подстроку из строки, начиная с указанного индекса и заданной длины.",
                Signature = "Вход: 1 строка, 2 целых числа (индекс, длина). Выход: 1 строка.",
                Example = "substr(\"Hello World\", 0, 5) // Результат: \"Hello\""
            }
        };
    }

    private static FunctionDefinition CreateJoinFunction()
    {
        const string name = "join";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                // Первый аргумент - массив строк
                if (!(args[0] is string[] strArray))
                    throw new ArgumentException($"Функция '{name}' ожидает массив строк в качестве первого аргумента.");

                // Второй аргумент - разделитель
                var separator = args[1]?.ToString() ?? "";

                return string.Join(separator, strArray);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Строковые операции"],
                Description = "Объединяет элементы массива строк в одну строку с указанным разделителем.",
                Signature = "Вход: 1 массив строк, 1 строка (разделитель). Выход: 1 строка.",
                Example = "join([\"Hello\", \"World\", \"!\"], \" \") // Результат: \"Hello World !\""
            }
        };
    }
}
