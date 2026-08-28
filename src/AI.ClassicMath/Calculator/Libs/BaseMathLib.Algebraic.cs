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
    //================== Стандартная математика ==================

    private static FunctionDefinition CreateRoundFunction()
    {
        const string name = "round";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)Math.Round(CastsVar.CastToDouble(args[0], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Статистика", "Программирование"],
                Description = "Округляет вещественное число до ближайшего целого.",
                Signature = "Вход: 1 число. Выход: 1 округлённое число.",
                Example = "round(3.59) // Результат: 4"
            }
        };
    }

    private static FunctionDefinition CreateFloorFunction()
    {
        const string name = "floor";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)(int)CastsVar.CastToDouble(args[0], name),
            Description = new DescriptionFunction
            {
                AreaList = ["Статистика", "Программирование"],
                Description = "Округляет вещественное число вниз до ближайшего целого.",
                Signature = "Вход: 1 число. Выход: 1 округлённое число.",
                Example = "floor(3.59) // Результат: 3"
            }
        };
    }

    private static FunctionDefinition CreateCeilFunction()
    {
        const string name = "ceil";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)Math.Ceiling(CastsVar.CastToDouble(args[0], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Статистика", "Программирование"],
                Description = "Округляет вещественное число вверх до ближайшего целого.",
                Signature = "Вход: 1 число. Выход: 1 округлённое число.",
                Example = "ceil(3.2) // Результат: 4"
            }
        };
    }

    private static FunctionDefinition CreateAbsFunction() => CreateUnaryComplexFunction("abs", x => (Complex)Complex.Abs(x),
      new DescriptionFunction
      {
          AreaList = ["Алгебра", "Геометрия", "Физика"],
          Description = "Вычисляет абсолютное значение (модуль) числа.",
          Signature = "Вход: 1 число. Выход: 1 вещественное число.",
          Example = "abs(3 - 4i) // Результат: 5"
      });

    private static FunctionDefinition CreateSqrtFunction() => CreateUnaryComplexFunction("sqrt", Complex.Sqrt,
      new DescriptionFunction
      {
          AreaList = ["Алгебра", "Геометрия", "Физика"],
          Description = "Вычисляет квадратный корень из числа.",
          Signature = "Вход: 1 число. Выход: 1 комплексное число (корень).",
          Example = "sqrt(-4) // Результат: 2i"
      });

    private static FunctionDefinition CreateCbrtFunction()
    {
        const string name = "cbrt";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => Complex.Pow(CastsVar.CastToComplex(args[0], name), 1.0 / 3.0),
            Description = new DescriptionFunction
            {
                AreaList = ["Алгебра", "Инженерия", "Физика"],
                Description = "Вычисляет кубический корень из числа.",
                Signature = "Вход: 1 число. Выход: 1 комплексное число (главное значение корня).",
                Example = "cbrt(-8) // Результат: 1 + 1.732i"
            }
        };
    }

    private static FunctionDefinition CreatePowFunction() => CreateBinaryComplexFunction("pow", Complex.Pow,
      new DescriptionFunction
      {
          AreaList = ["Алгебра", "Финансы", "Физика"],
          Description = "Возводит число в указанную степень.",
          Signature = "Вход: 2 числа (основание, степень). Выход: 1 число.",
          Example = "pow(2, 10) // Результат: 1024"
      });

    //================== Работа с датами ==================

    private static FunctionDefinition CreateDateTimeFunction()
    {
        const string name = "DateTime";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args =>
            {
                var dateString = args[0]?.ToString() ?? throw new ArgumentException($"Функция '{name}' требует строку с датой.");

                if (!System.DateTime.TryParse(dateString, System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out var result))
                {
                    throw new ArgumentException($"Не удалось распарсить дату: '{dateString}'. Используйте формат: yyyy-MM-dd или yyyy-MM-dd HH:mm:ss");
                }

                return result;
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Календарь"],
                Description = "Парсит строку и возвращает объект DateTime. Поддерживает форматы: yyyy-MM-dd, yyyy-MM-dd HH:mm:ss",
                Signature = "Вход: 1 строка (дата). Выход: DateTime объект.",
                Example = "DateTime(\"2025-12-19\") // Парсит дату"
            }
        };
    }

    private static FunctionDefinition CreateDateDiffFunction()
    {
        const string name = "DateDiff";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                if (args[0] is not System.DateTime date1)
                    throw new ArgumentException($"Функция '{name}': первый аргумент должен быть DateTime");
                if (args[1] is not System.DateTime date2)
                    throw new ArgumentException($"Функция '{name}': второй аргумент должен быть DateTime");

                // Разница считается ОТ первой даты КО второй: DateDiff(начало, конец) даёт
                // положительный срок. Так пишут задачу словами («от 1 января до 28 августа»),
                // так же устроен DATEDIFF в SQL. Обратный порядок отдавал минус там, где ждут
                // длительность, и этот минус уезжал в документ вместе с числом дней.
                var isNegative = date2 < date1;
                var span = date2 - date1;

                // Работаем с абсолютными значениями для упрощения логики
                var start = isNegative ? date2 : date1;
                var end = isNegative ? date1 : date2;

                // Вычисляем компоненты разницы
                int years = end.Year - start.Year;
                int months = end.Month - start.Month;
                int days = end.Day - start.Day;
                int hours = end.Hour - start.Hour;
                int minutes = end.Minute - start.Minute;
                int seconds = end.Second - start.Second;

                // Корректируем отрицательные значения снизу вверх
                if (seconds < 0) { seconds += 60; minutes--; }
                if (minutes < 0) { minutes += 60; hours--; }
                if (hours < 0) { hours += 24; days--; }
                if (days < 0)
                {
                    months--;
                    days += System.DateTime.DaysInMonth(start.Year, start.Month);
                }
                if (months < 0) { months += 12; years--; }

                var sign = isNegative ? "-" : "";

                // Число печатается инвариантной культурой: на русской локали "F2" даёт
                // «239,00», и такая запятая уходит и в текст ответа, и обратно в вычислитель,
                // где уже не разбирается.
                var total = span.TotalDays.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

                return $"{sign}{years}y {months}m {days}d {hours}h {minutes}min {seconds}s (total: {total} days)";
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Программирование", "Календарь"],
                Description = "Календарная разница ОТ первой даты КО второй: годы, месяцы, дни, часы, минуты, секунды и всего дней. Если вторая дата раньше первой, результат отрицательный",
                Signature = "Вход: 2 DateTime объекта (начало, конец). Выход: строка с детальной разницей.",
                Example = "DateDiff(DateTime(\"2026-01-01\"), DateTime(\"2026-08-28\")) // 0y 7m 27d ... (total: 239.00 days)"
            }
        };
    }

    //================== Комбинаторика и спец. функции ==================

    private static FunctionDefinition CreateFactFunction()
    {
        const string name = "fact";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)FunctionsForEachElements.Factorial((int)CastsVar.CastToDouble(args[0], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Комбинаторика", "Статистика"],
                Description = "Вычисляет факториал целого неотрицательного числа n (n!).",
                Signature = "Вход: 1 целое число n. Выход: 1 число.",
                Example = "fact(5) // Результат: 120"
            }
        };
    }

    private static FunctionDefinition CreateGammaFunction()
    {
        const string name = "gamma";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 1,
            Delegate = args => (Complex)FunctionsForEachElements.Gamma(CastsVar.CastToDouble(args[0], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Математический анализ", "Статистика"],
                Description = "Вычисляет гамма-функцию, обобщение факториала. Gamma(n) = (n-1)!",
                Signature = "Вход: 1 вещественное число. Выход: 1 число.",
                Example = "gamma(6) // Результат: 120"
            }
        };
    }

    private static FunctionDefinition CreateCombFunction()
    {
        const string name = "Comb";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args => (Complex)CombinatoricsBaseFunction.Combinations((int)CastsVar.CastToDouble(args[0], name), (int)CastsVar.CastToDouble(args[1], name)),
            Description = new DescriptionFunction
            {
                AreaList = ["Комбинаторика", "Теория вероятностей"],
                Description = "Вычисляет число сочетаний из n по k (C n k).",
                Signature = "Вход: 2 целых числа (n, k). Выход: 1 число.",
                Example = "Comb(5, 2) // Результат: 10"
            }
        };
    }

    private static FunctionDefinition CreateCombPFunction()
    {
        const string name = "CombP";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                var n = (int)CastsVar.CastToDouble(args[0], name);
                var k = (int)CastsVar.CastToDouble(args[1], name);
                return (Complex)(FunctionsForEachElements.Factorial(n) / FunctionsForEachElements.Factorial(n - k));
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Комбинаторика", "Теория вероятностей"],
                Description = "Вычисляет число размещений из n по k (A n k).",
                Signature = "Вход: 2 целых числа (n, k). Выход: 1 число.",
                Example = "CombP(5, 2) // Результат: 20"
            }
        };
    }

    //================== Теория чисел ==================

    private static FunctionDefinition CreateGCDFunction()
    {
        const string name = "gcd";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                var a = CastsVar.CastToDouble(args[0], name);
                var b = CastsVar.CastToDouble(args[1], name);

                // Проверяем, являются ли числа целыми
                bool isAInteger = Math.Abs(a - Math.Round(a)) < 1e-10;
                bool isBInteger = Math.Abs(b - Math.Round(b)) < 1e-10;

                if (isAInteger && isBInteger)
                {
                    // Для целых чисел используем быстрый алгоритм
                    return (Complex)ProcessorLogic.Processor.GCDLong((long)a, (long)b);
                }
                else
                {
                    // Для дробных чисел используем алгоритм с преобразованием в дроби
                    return (Complex)ProcessorLogic.Processor.GCDDouble(a, b);
                }
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Теория чисел", "Алгебра"],
                Description = "Вычисляет наибольший общий делитель (НОД) двух чисел. Работает как с целыми, так и с дробными числами.",
                Signature = "Вход: 2 числа. Выход: 1 число (НОД).",
                Example = "gcd(48, 18) // Результат: 6\ngcd(2.5, 1.5) // Результат: 0.5"
            }
        };
    }

    private static FunctionDefinition CreateLCMFunction()
    {
        const string name = "lcm";
        return new FunctionDefinition
        {
            Name = name,
            ArgumentCount = 2,
            Delegate = args =>
            {
                var a = CastsVar.CastToDouble(args[0], name);
                var b = CastsVar.CastToDouble(args[1], name);

                // Проверяем, являются ли числа целыми
                bool isAInteger = Math.Abs(a - Math.Round(a)) < 1e-10;
                bool isBInteger = Math.Abs(b - Math.Round(b)) < 1e-10;

                if (isAInteger && isBInteger)
                {
                    // Для целых чисел используем быстрый алгоритм
                    return (Complex)ProcessorLogic.Processor.LCM((long)a, (long)b);
                }
                else
                {
                    // Для дробных чисел используем алгоритм с преобразованием в дроби
                    return (Complex)ProcessorLogic.Processor.LCMDouble(a, b);
                }
            },
            Description = new DescriptionFunction
            {
                AreaList = ["Теория чисел", "Алгебра"],
                Description = "Вычисляет наименьшее общее кратное (НОК) двух чисел. Работает как с целыми, так и с дробными числами.",
                Signature = "Вход: 2 числа. Выход: 1 число (НОК).",
                Example = "lcm(12, 18) // Результат: 36\nlcm(2.5, 1.5) // Результат: 7.5"
            }
        };
    }
}
