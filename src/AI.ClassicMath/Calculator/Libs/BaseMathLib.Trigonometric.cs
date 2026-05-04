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
    //================== Тригонометрия ==================

    private static FunctionDefinition CreateSinFunction() => CreateUnaryComplexFunction("sin", Complex.Sin, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Геометрия", "Физика"],
        Description = "Вычисляет синус числа (аргумент в радианах).",
        Signature = "Вход: 1 число (угол в радианах). Выход: 1 число.",
        Example = "sin(pi/2) // Результат: 1"
    });
    private static FunctionDefinition CreateCosFunction() => CreateUnaryComplexFunction("cos", Complex.Cos, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Геометрия", "Физика"],
        Description = "Вычисляет косинус числа (аргумент в радианах).",
        Signature = "Вход: 1 число (угол в радианах). Выход: 1 число.",
        Example = "cos(pi) // Результат: -1"
    });
    private static FunctionDefinition CreateTanFunction() => CreateUnaryComplexFunction("tan", Complex.Tan, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Геометрия", "Физика"],
        Description = "Вычисляет тангенс числа (аргумент в радианах).",
        Signature = "Вход: 1 число (угол в радианах). Выход: 1 число.",
        Example = "tan(pi/4) // Результат: 1"
    });
    private static FunctionDefinition CreateAsinFunction() => CreateUnaryComplexFunction("asin", Complex.Asin, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Геометрия", "Физика"],
        Description = "Вычисляет арксинус числа. Результат в радианах.",
        Signature = "Вход: 1 число. Выход: 1 число (угол в радианах).",
        Example = "asin(1) // Результат: pi/2"
    });
    private static FunctionDefinition CreateAcosFunction() => CreateUnaryComplexFunction("acos", Complex.Acos, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Геометрия", "Физика"],
        Description = "Вычисляет арккосинус числа. Результат в радианах.",
        Signature = "Вход: 1 число. Выход: 1 число (угол в радианах).",
        Example = "acos(-1) // Результат: pi"
    });
    private static FunctionDefinition CreateAtanFunction() => CreateUnaryComplexFunction("atan", Complex.Atan, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Геометрия", "Физика"],
        Description = "Вычисляет арктангенс числа. Результат в радианах.",
        Signature = "Вход: 1 число. Выход: 1 число (угол в радианах).",
        Example = "atan(1) // Результат: pi/4"
    });
    private static FunctionDefinition CreateTanhFunction() => CreateUnaryComplexFunction("tanh", Complex.Tanh, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Физика", "Нейронные сети"],
        Description = "Вычисляет гиперболический тангенс числа.",
        Signature = "Вход: 1 число. Выход: 1 число.",
        Example = "tanh(1)"
    });

    //================== Логарифмы и экспонента ==================

    private static FunctionDefinition CreateLnFunction() => CreateUnaryComplexFunction("ln", Complex.Log, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Теория информации", "Физика"],
        Description = "Вычисляет натуральный логарифм (по основанию e) числа.",
        Signature = "Вход: 1 число. Выход: 1 число.",
        Example = "ln(e) // Результат: 1"
    });
    private static FunctionDefinition CreateLog10Function() => CreateUnaryComplexFunction("log10", Complex.Log10, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Химия", "Инженерия"],
        Description = "Вычисляет десятичный логарифм (по основанию 10) числа.",
        Signature = "Вход: 1 число. Выход: 1 число.",
        Example = "log10(100) // Результат: 2"
    });
    private static FunctionDefinition CreateExpFunction() => CreateUnaryComplexFunction("exp", Complex.Exp, new DescriptionFunction
    {
        AreaList = ["Алгебра", "Статистика", "Физика"],
        Description = "Вычисляет экспоненту числа (e в степени x).",
        Signature = "Вход: 1 число. Выход: 1 число.",
        Example = "exp(1) // Результат: 2.718..."
    });

    private static FunctionDefinition CreateLogFunction()
    {
        const string name = "log";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = -1, // ИСПРАВЛЕНИЕ: Поддержка 1 или 2 аргументов
            Delegate = args =>
            {
                if (args.Length == 1)
                {
                    // log(x) = ln(x) - натуральный логарифм
                    return Complex.Log(CastsVar.CastToComplex(args[0], name));
                }
                else if (args.Length == 2)
                {
                    // log(x, base) - логарифм по основанию
                    return Complex.Log(CastsVar.CastToComplex(args[0], name)) / Complex.Log(CastsVar.CastToComplex(args[1], name));
                }
                else
                {
                    throw new ArgumentException($"Функция '{name}' ожидает 1 или 2 аргумента, получила {args.Length}.");
                }
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Алгебра", "Информатика", "Физика"],
                Description = "Вычисляет логарифм числа. log(x) = ln(x), log(x, base) = логарифм по основанию.",
                Signature = "Вход: 1 число (ln) или 2 числа (значение, основание). Выход: 1 число.",
                Example = "log(e) // Результат: 1; log(8, 2) // Результат: 3"
            }
        };
    }

    //================== Угловые меры ==================

    private static FunctionDefinition CreateRadFunction()
    {
        const string name = "rad";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)FunctionsForEachElements.GradToRad(CastsVar.CastToDouble(args[0], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Геометрия", "Физика", "Инженерия"],
                Description = "Конвертирует градусы в радианы.",
                Signature = "Вход: 1 число (градусы). Выход: 1 число (радианы).",
                Example = "rad(180) // Результат: 3.14159..."
            }
        };
    }

    private static FunctionDefinition CreateDegFunction()
    {
        const string name = "deg";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)FunctionsForEachElements.RadToGrad(CastsVar.CastToDouble(args[0], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Геометрия", "Физика", "Инженерия"],
                Description = "Конвертирует радианы в градусы.",
                Signature = "Вход: 1 число (радианы). Выход: 1 число (градусы).",
                Example = "deg(pi) // Результат: 180"
            }
        };
    }
}
