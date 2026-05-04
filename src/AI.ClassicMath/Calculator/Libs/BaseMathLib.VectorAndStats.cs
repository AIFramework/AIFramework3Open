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
    //================== Векторные операции ==================

    private static FunctionDefinition CreateMagFunction()
    {
        const string name = "mag";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)BaseDist.L2(CastsVar.CastToComplexVector(args[0], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Линейная алгебра", "Геометрия", "Физика"],
                Description = "Вычисляет длину (евклидову норму или L2-норму) вектора.",
                Signature = "Вход: 1 вектор. Выход: 1 вещественное число.",
                Example = "mag([3, 4]) // Результат: 5"
            }
        };
    }

    private static FunctionDefinition CreateSumFunction()
    {
        const string name = "sum";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => CastsVar.CastToComplexVector(args[0], name).Sum(),
            Description = new DescriptionFunction
            {
                AreaList = ["Статистика", "Линейная алгебра"],
                Description = "Вычисляет сумму всех элементов вектора.",
                Signature = "Вход: 1 вектор. Выход: 1 число.",
                Example = "sum([1, 2, 3, 4]) // Результат: 10"
            }
        };
    }

    private static FunctionDefinition CreateDotFunction()
    {
        const string name = "dot";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args => ComplexVector.Dot(CastsVar.CastToComplexVector(args[0], name), CastsVar.CastToComplexVector(args[1], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Линейная алгебра", "Геометрия", "Физика"],
                Description = "Вычисляет скалярное произведение двух векторов.",
                Signature = "Вход: 2 вектора. Выход: 1 число.",
                Example = "dot([1, 2, 3], [4, 5, 6]) // Результат: 32"
            }
        };
    }

    private static FunctionDefinition CreateCrossFunction()
    {
        const string name = "cross";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                var v1 = CastsVar.CastToRealVector(args[0], name);
                var v2 = CastsVar.CastToRealVector(args[1], name);
                return Vector.Cross(v1, v2);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Линейная алгебра", "Геометрия", "Физика"],
                Description = "Вычисляет векторное (косое) произведение двух 3D векторов.",
                Signature = "Вход: 2 двумерных или 2 трехмерных вектора. Выход: 1 вектор.",
                Example = "cross([1, 0, 0], [0, 1, 0]) // Результат: [0, 0, 1]"
            }
        };
    }

    private static FunctionDefinition CreateIndexFunction()
    {
        const string name = "index";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                var array = args[0];
                var indexArg = CastsVar.CastToInt32(args[1], name);

                // Поддержка массивов строк
                if (array is string[] stringArray)
                {
                    if (indexArg < 0 || indexArg >= stringArray.Length)
                        throw new IndexOutOfRangeException($"Индекс {indexArg} выходит за границы массива (длина: {stringArray.Length}).");
                    return stringArray[indexArg];
                }

                // Поддержка числовых массивов (ComplexVector)
                var vector = CastsVar.CastToComplexVector(array, name);
                if (indexArg < 0 || indexArg >= vector.Count)
                    throw new IndexOutOfRangeException($"Индекс {indexArg} выходит за границы массива (длина: {vector.Count}).");
                return vector[indexArg];
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Линейная алгебра"],
                Description = "Возвращает элемент вектора по заданному индексу (нумерация с 0).",
                Signature = "Вход: 1 вектор, 1 целое число (индекс). Выход: элемент вектора.",
                Example = "index([10, 20, 30], 1) // Результат: 20"
            }
        };
    }

    //================== Статистика ==================

    private static FunctionDefinition CreateMeanFunction()
    {
        const string name = "mean";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = -1,
            Delegate = args =>
            {
                if (!args.Any()) return Complex.Zero;

                // ИСПРАВЛЕНИЕ: Поддержка массивов - если передан ComplexVector, используем его элементы
                Complex[] complexArgs;
                if (args.Length == 1 && args[0] is ComplexVector vector)
                {
                    complexArgs = vector.ToArray();
                }
                else
                {
                    complexArgs = args.Select(a => CastsVar.CastToComplex(a, name)).ToArray();
                }

                return new Complex(complexArgs.Average(c => c.Real), complexArgs.Average(c => c.Imaginary));
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Статистика", "Анализ данных"],
                Description = "Вычисляет среднее арифметическое для набора чисел или массива.",
                Signature = "Вход: N чисел или 1 массив. Выход: 1 число.",
                Example = "mean(2, 4, 9) // Результат: 5; mean([1, 2, 3, 4, 5]) // Результат: 3"
            }
        };
    }

    private static FunctionDefinition CreateMinFunction()
    {
        const string name = "min";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = -1,
            Delegate = args =>
            {
                if (!args.Any()) throw new ArgumentException("Функция 'min' требует хотя бы один аргумент.");

                // Поддержка массивов - если передан ComplexVector, используем его элементы
                double[] values;
                if (args.Length == 1 && args[0] is ComplexVector vector)
                {
                    values = vector.ToArray().Select(c => c.Real).ToArray();
                }
                else
                {
                    values = args.Select(a => CastsVar.CastToDouble(a, name)).ToArray();
                }

                return new Complex(values.Min(), 0);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Статистика", "Анализ данных"],
                Description = "Находит минимальное значение среди набора чисел или массива.",
                Signature = "Вход: N чисел или 1 массив. Выход: 1 число.",
                Example = "min(2, -1, 5) // Результат: -1; min([5, 2, 8, 1]) // Результат: 1"
            }
        };
    }

    private static FunctionDefinition CreateMaxFunction()
    {
        const string name = "max";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = -1,
            Delegate = args =>
            {
                if (!args.Any()) throw new ArgumentException("Функция 'max' требует хотя бы один аргумент.");

                // Поддержка массивов - если передан ComplexVector, используем его элементы
                double[] values;
                if (args.Length == 1 && args[0] is ComplexVector vector)
                {
                    values = vector.ToArray().Select(c => c.Real).ToArray();
                }
                else
                {
                    values = args.Select(a => CastsVar.CastToDouble(a, name)).ToArray();
                }

                return new Complex(values.Max(), 0);
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Статистика", "Анализ данных"],
                Description = "Находит максимальное значение среди набора чисел или массива.",
                Signature = "Вход: N чисел или 1 массив. Выход: 1 число.",
                Example = "max(2, -1, 5) // Результат: 5; max([5, 2, 8, 1]) // Результат: 8"
            }
        };
    }
}
