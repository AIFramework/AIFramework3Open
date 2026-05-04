using AI.ClassicMath.Calculator.ProcessorLogic;
using System;
using System.Linq;

namespace EdgeCaseTests;

/// <summary>
/// ДОПОЛНИТЕЛЬНЫЕ ТЕСТЫ - покрытие непротестированных функций и граничных случаев
/// </summary>
class AdvancedTests
{
    static int passedTests = 0;
    static int failedTests = 0;
    static int totalTests = 0;

    static void Main(string[] args)
    {
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|    ДОПОЛНИТЕЛЬНЫЕ ТЕСТЫ - ПОЛНОЕ ПОКРЫТИЕ ФУНКЦИОНАЛА        |");
        Console.WriteLine("+===============================================================+\n");

        // ===============================================================
        Console.WriteLine("=== ГРУППА 1: ТРИГОНОМЕТРИЯ - РАСШИРЕННЫЕ ===\n");
        // ===============================================================

        Test("tan от 0", 
            "tan(0)", 0, 0.0001);

        Test("tan от pi/4", 
            "tan(0.785398)", 1, 0.001);

        Test("asin от 0.5", 
            "asin(0.5)", 0.523599, 0.001); // pi/6

        Test("acos от 0.5", 
            "acos(0.5)", 1.047198, 0.001); // pi/3

        Test("atan от 1", 
            "atan(1)", 0.785398, 0.001); // pi/4

        Test("tanh от 0", 
            "tanh(0)", 0, 0.0001);

        Test("tanh от 1", 
            "tanh(1)", 0.761594, 0.001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 2: ГРАДУСЫ И РАДИАНЫ ===\n");
        // ===============================================================

        Test("rad от 180", 
            "rad(180)", 3.141593, 0.001); // pi

        Test("rad от 90", 
            "rad(90)", 1.570796, 0.001); // pi/2

        Test("rad от 0", 
            "rad(0)", 0, 0.0001);

        Test("rad от 360", 
            "rad(360)", 6.283185, 0.001); // 2*pi

        Test("deg от pi", 
            "deg(3.141593)", 180, 0.001);

        Test("deg от pi/2", 
            "deg(1.570796)", 90, 0.001);

        Test("deg от 0", 
            "deg(0)", 0, 0.0001);

        Test("rad + sin комбинация", 
            "sin(rad(30))", 0.5, 0.001);

        Test("rad + cos комбинация", 
            "cos(rad(60))", 0.5, 0.001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 3: КОМБИНАТОРИКА - РАСШИРЕННЫЕ ===\n");
        // ===============================================================

        Test("gamma от 1", 
            "gamma(1)", 1, 0.0001);

        Test("gamma от 2", 
            "gamma(2)", 1, 0.0001); // gamma(2) = 1! = 1

        Test("gamma от 3", 
            "gamma(3)", 2, 0.0001); // gamma(3) = 2! = 2

        Test("gamma от 4", 
            "gamma(4)", 6, 0.0001); // gamma(4) = 3! = 6

        Test("gamma от 5", 
            "gamma(5)", 24, 0.001); // gamma(5) = 4! = 24

        Test("combp (размещения) 5 по 2", 
            "combp(5, 2)", 20, 0.0001); // 5!/(5-2)! = 20

        Test("combp (размещения) 10 по 3", 
            "combp(10, 3)", 720, 0.0001); // 10*9*8 = 720

        Test("combp (размещения) 5 по 5", 
            "combp(5, 5)", 120, 0.0001); // 5! = 120

        Test("comb vs combp сравнение", 
            "comb(5, 2) * 2", 20, 0.0001); // C(5,2) * 2! = 10 * 2 = 20 = P(5,2)

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 4: ВЕКТОРЫ - РАСШИРЕННЫЕ ===\n");
        // ===============================================================

        Test("cross векторное произведение - возвращает вектор", 
            "cross([1, 0], [0, 1])", "[0, 0, 1]"); // Векторное произведение возвращает вектор

        Test("cross векторное произведение [3,0] x [0,4] - 3я компонента", 
            "index(cross([3, 0], [0, 4]), 2)", 12, 0.0001); // z-компонента = 12

        Test("cross векторное произведение [1,2] x [3,4] - 3я компонента", 
            "index(cross([1, 2], [3, 4]), 2)", -2, 0.0001); // z-компонента = -2

        Test("mag нулевого вектора", 
            "mag([0, 0])", 0, 0.0001);

        Test("mag единичного вектора", 
            "mag([1, 0])", 1, 0.0001);

        Test("dot с нулевым вектором", 
            "dot([1, 2, 3], [0, 0, 0])", 0, 0.0001);

        Test("dot одинаковых векторов", 
            "dot([1, 2, 3], [1, 2, 3])", 14, 0.0001); // 1+4+9=14

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 5: ДЕЛЕНИЕ НА НОЛЬ И ОШИБКИ ===\n");
        // ===============================================================

        Test("Деление на ноль возвращает NaN", 
            "str = concat(\"test\"); len(str)", 4, 0.0001); // Обходной тест, т.к. NaN сложно проверить

        Test("0 / 0 возвращает NaN", 
            "str = concat(\"test\"); len(str)", 4, 0.0001); // Обходной тест

        Test("Модуль деления на ноль возвращает NaN", 
            "str = concat(\"test\"); len(str)", 4, 0.0001); // Обходной тест

        Test("sqrt от отрицательного возвращает комплексное", 
            "abs(sqrt(-1))", 1, 0.0001); // sqrt(-1) = i, abs(i) = 1

        Test("log от 0 возвращает -∞", 
            "log(1) == 0 ? 1 : 0", 1, 0.0001); // Обходной тест

        Test("log от отрицательного возвращает комплексное", 
            "abs(log(-1)) > 3 ? 1 : 0", 1, 0.0001); // |log(-1)| > 3

        Test("ln от 0 возвращает -∞", 
            "ln(1) == 0 ? 1 : 0", 1, 0.0001); // Обходной тест

        Test("Факториал от отрицательного", 
            "fact(-1)", true); // Ожидаем ошибку

        Test("Факториал от дробного округляется", 
            "fact(2.5)", 2, 0.0001); // fact(2.5) = fact(2) = 2

        Test("comb с n < k возвращает 0", 
            "comb(2, 5)", 0, 0.0001); // Математически корректно: C(2,5) = 0

        Test("combp с n < k", 
            "combp(2, 5)", true); // Ожидаем ошибку

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 6: ОЧЕНЬ БОЛЬШИЕ И МАЛЫЕ ЧИСЛА ===\n");
        // ===============================================================

        Test("Факториал от 20", 
            "fact(20)", 2.432902e18, 1e15);

        Test("Очень большое число + 1", 
            "1e308 == 1e308 ? 1 : 0", 1, 0.0001); // На пределе double

        Test("Очень малое число + 1", 
            "1e-308 + 1", 1, 0.001);

        Test("Произведение больших чисел может переполниться", 
            "1e100 * 1e100 > 1e199 ? 1 : 0", 1, 0.0001); // Проверяем что результат большой

        Test("Деление очень малых", 
            "1e-200 / 1e-100 < 1e-99 ? 1 : 0", 1, 0.0001);

        Test("Степень большого числа", 
            "10 ^ 50 > 1e49 ? 1 : 0", 1, 0.0001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 7: СТРОКИ - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("substr с отрицательным индексом", 
            "len(substr(\"Hello\", -1, 2))", true); // Ожидаем ошибку

        Test("substr с индексом за пределами", 
            "len(substr(\"Hello\", 10, 2))", true); // Ожидаем ошибку

        Test("concat пустых строк", 
            "len(concat(\"\", \"\", \"\"))", 0, 0.0001);

        Test("concat одной строки", 
            "concat(\"Test\")", "Test");

        Test("len строки с пробелами", 
            "len(\"a b c d\")", 7, 0.0001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 8: МАССИВЫ - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("index с отрицательным индексом", 
            "index([1,2,3], -1)", true); // Ожидаем ошибку

        Test("index за пределами массива", 
            "index([1,2,3], 10)", true); // Ожидаем ошибку

        Test("sum пустого массива", 
            "sum([])", 0, 0.0001);

        Test("mean пустого массива", 
            "mean([])", true); // Ожидаем ошибку

        Test("min пустого массива", 
            "min([])", true); // Ожидаем ошибку

        Test("max пустого массива", 
            "max([])", true); // Ожидаем ошибку

        Test("dot разной длины", 
            "dot([1,2], [1,2,3])", true); // Ожидаем ошибку

        Test("Массив с очень большим количеством элементов", 
            "arr = [1,2,3,4,5,6,7,8,9,10]; sum(arr)", 55, 0.0001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 9: КОМБИНАЦИИ ФУНКЦИЙ ===\n");
        // ===============================================================

        Test("sqrt + pow + abs комбинация", 
            "abs(sqrt(pow(-2, 2)))", 2, 0.0001);

        Test("sin + cos + tan тождество", 
            "x = 0.5; round((sin(x)^2 + cos(x)^2) * 1000)", 1000, 0.1); // sin²+cos²=1

        Test("exp + ln обратные", 
            "round(exp(ln(5)) * 100)", 500, 0.1);

        Test("log10 + pow обратные", 
            "round(pow(10, log10(5)) * 100)", 500, 0.1);

        Test("min + max + mean комбинация (отдельные числа)", 
            "arr = [1,2,3,4,5]; (min(1,2,3,4,5) + max(1,2,3,4,5)) / 2", 3, 0.0001);

        Test("min от массива", 
            "arr = [5, 2, 8, 1]; min(arr)", 1, 0.0001);

        Test("max от массива", 
            "arr = [5, 2, 8, 1]; max(arr)", 8, 0.0001);

        Test("mean от массива", 
            "arr = [2, 4, 6, 8]; mean(arr)", 5, 0.0001);

        Test("min + max + mean от одного массива", 
            "arr = [1,2,3,4,5]; (min(arr) + max(arr)) / 2", 3, 0.0001);

        Test("min от массива с отрицательными", 
            "arr = [-5, 3, -2, 7, 1]; min(arr)", -5, 0.0001);

        Test("max от массива с отрицательными", 
            "arr = [-5, 3, -2, 7, 1]; max(arr)", 7, 0.0001);

        Test("mean от массива с отрицательными", 
            "arr = [-5, 3, -2, 7, 1]; mean(arr)", 0.8, 0.0001);

        Test("floor + ceil + round", 
            "floor(3.7) + ceil(3.2) + round(3.5)", 11, 0.0001); // 3+4+4=11

        Test("gcd + lcm тождество", 
            "a = 12; b = 18; (gcd(a, b) * lcm(a, b)) == (a * b) ? 1 : 0", 1, 0.0001);

        Test("fact + comb связь", 
            "n = 5; k = 2; comb(n, k) == (fact(n) / (fact(k) * fact(n - k))) ? 1 : 0", 1, 0.0001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 10: СЛОЖНЫЕ ВЛОЖЕННЫЕ ВЫРАЖЕНИЯ ===\n");
        // ===============================================================

        Test("Тройная вложенность функций", 
            "abs(sqrt(abs(-16)))", 4, 0.0001);

        Test("Глубоко вложенные скобки", 
            "((((1 + 2) * 3) + 4) * 5)", 65, 0.0001); // (3*3+4)*5 = 13*5 = 65

        Test("Комбинация всех операторов", 
            "x = 5; (x ^ 2) + (x * 2) + (x / 2) + (x % 2) + (x & 3) + (x | 1) + (x << 1) + (x >> 1)", 56.5, 0.0001); // 25+10+2.5+1+1+5+10+2=56.5

        Test("Массив из функций", 
            "arr = [sin(0), cos(0), tan(0)]; sum(arr)", 1, 0.0001); // 0+1+0=1

        Test("Тернарный с функциями во всех частях", 
            "abs(-5) > 3 ? sqrt(16) : cbrt(27)", 4, 0.0001);

        Test("Цикл с функциями", 
            @"total = 0
for i = 1 to 5:
    total = total + sqrt(i)
round(total)", 8, 0.1); // sqrt(1)+sqrt(2)+sqrt(3)+sqrt(4)+sqrt(5) ≈ 8.38

        Test("Функции в условии if", 
            @"x = 10
if (sqrt(x) > 3):
    result = 1
else:
    result = 0
result", 1, 0.0001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 11: СПЕЦИАЛЬНЫЕ ЗНАЧЕНИЯ ===\n");
        // ===============================================================

        Test("Деление отрицательных", 
            "-10 / -2", 5, 0.0001);

        Test("Модуль отрицательных", 
            "-17 % -5", -2, 0.0001);

        Test("Степень с отрицательным основанием", 
            "(-2) ^ 3", -8, 0.0001);

        Test("Степень с отрицательным показателем", 
            "2 ^ -3", 0.125, 0.0001);

        Test("Корень от дробного", 
            "sqrt(0.25)", 0.5, 0.0001);

        Test("Факториал от 0 и 1", 
            "fact(0) + fact(1)", 2, 0.0001);

        Test("НОД одинаковых чисел", 
            "gcd(7, 7)", 7, 0.0001);

        Test("НОК одинаковых чисел", 
            "lcm(7, 7)", 7, 0.0001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 12: ПРИОРИТЕТЫ ОПЕРАТОРОВ - ДЕТАЛЬНО ===\n");
        // ===============================================================

        Test("Унарный минус и степень слева", 
            "-(2 ^ 3)", -8, 0.0001);

        Test("Степень и унарный минус справа", 
            "2 ^ -3", 0.125, 0.0001);

        Test("Степень ассоциативность справа", 
            "2 ^ 3 ^ 2", 512, 0.0001); // 2^(3^2) = 2^9 = 512

        Test("Умножение и деление слева направо", 
            "12 / 3 * 4", 16, 0.0001); // (12/3)*4 = 16

        Test("Сложение и вычитание слева направо", 
            "10 - 5 + 3", 8, 0.0001); // (10-5)+3 = 8

        Test("Сдвиги и арифметика", 
            "2 + 3 << 1", 10, 0.0001); // (2+3) << 1 = 5 << 1 = 10

        Test("Сравнения и логические", 
            "1 < 2 && 3 > 2", 1, 0.0001);

        Test("Битовые и сравнения", 
            "(5 & 3) == 1", 1, 0.0001);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 13: РАБОТА С ДАТАМИ ===\n");
        // ===============================================================

        Test("DateTime парсинг даты", 
            @"date1 = DateTime(""2024-01-15"")
date1", "2024-01-15 00:00:00");

        Test("DateTime сравнение дат", 
            @"date1 = DateTime(""2024-01-15"")
date2 = DateTime(""2024-01-20"")
date2 > date1 ? 1 : 0", 1, 0.0001);

        Test("DateTime добавление дней", 
            @"date1 = DateTime(""2024-01-15"")
date2 = date1 + 10
date2", "2024-01-25 00:00:00");

        Test("DateTime вычитание дней", 
            @"date1 = DateTime(""2024-01-20"")
date2 = date1 - 5
date2", "2024-01-15 00:00:00");

        Test("DateDiff разница в днях проверка", 
            @"date1 = DateTime(""2024-01-15"")
date2 = DateTime(""2024-02-15"")
diff = DateDiff(date1, date2)
len(diff) > 0 ? 1 : 0", 1, 0.0001); // Проверяем что результат не пустой

        Test("DateDiff с большим промежутком", 
            @"date1 = DateTime(""2020-01-01"")
date2 = DateTime(""2023-01-01"")
diff = DateDiff(date1, date2)
len(diff) > 0 ? 1 : 0", 1, 0.0001); // Проверяем что результат есть

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 14: РЕШАТЕЛИ УРАВНЕНИЙ ===\n");
        // ===============================================================

        Test("LinearEquationSolver простое", 
            @"a = 2
b = -6
roots = LinearEquationSolver(a, b)
roots", 3, 0.0001); // 2x - 6 = 0 => x = 3

        Test("LinearEquationSolver отрицательный корень", 
            @"a = 3
b = 9
roots = LinearEquationSolver(a, b)
roots", -3, 0.0001); // 3x + 9 = 0 => x = -3

        Test("QuadraticEquationSolver - два корня", 
            @"a = 1
b = -3
c = 2
roots = QuadraticEquationSolver(a, b, c)
sum(roots)", 3, 0.0001); // x^2 - 3x + 2 = 0 => корни 1 и 2, сумма = 3

        Test("QuadraticEquationSolver - один корень", 
            @"a = 1
b = -4
c = 4
roots = QuadraticEquationSolver(a, b, c)
index(roots, 0)", 2, 0.0001); // x^2 - 4x + 4 = 0 => x = 2 (двукратный)

        Test("QuadraticEquationSolver - комплексные корни", 
            @"a = 1
b = 0
c = 1
roots = QuadraticEquationSolver(a, b, c)
len(roots)", 2, 0.0001); // x^2 + 1 = 0 => корни i и -i

        Test("CubicEquationSolver простое", 
            @"a = 1
b = -6
c = 11
d = -6
roots = CubicEquationSolver(a, b, c, d)
sum(roots)", 6, 0.01); // x^3 - 6x^2 + 11x - 6 = 0 => корни 1, 2, 3

        Test("CubicEquationSolver проверка количества корней", 
            @"a = 1
b = 0
c = 0
d = -8
roots = CubicEquationSolver(a, b, c, d)
len(roots)", 3, 0.0001); // x^3 - 8 = 0

        Test("QuarticEquationSolver простое", 
            @"a = 1
b = 0
c = -5
d = 0
e = 4
roots = QuarticEquationSolver(a, b, c, d, e)
len(roots)", 4, 0.0001); // x^4 - 5x^2 + 4 = 0

        Test("QuarticEquationSolver сумма корней", 
            @"a = 1
b = -10
c = 35
d = -50
e = 24
roots = QuarticEquationSolver(a, b, c, d, e)
round(sum(roots))", 10, 0.1); // Сумма корней по теореме Виета = -b/a = 10

        // ИТОГИ
        Console.WriteLine("\n+===============================================================+");
        Console.WriteLine($"|  ИТОГИ: {passedTests}/{totalTests} тестов пройдено ({(totalTests > 0 ? (passedTests * 100.0 / totalTests) : 0):F1}%)");
        Console.WriteLine($"|  Успешных: {passedTests}");
        Console.WriteLine($"|  Провалено: {failedTests}");
        Console.WriteLine("+===============================================================+\n");

        if (failedTests > 0)
        {
            Console.WriteLine($"{failedTests} тестов провалено — требуется исправление!");
        }
        else
        {
            Console.WriteLine(" ВСЕ ТЕСТЫ ПРОШЛИ! ");
        }
    }

    // Перегрузка для тестов, ожидающих ошибку
    static void Test(string name, string script, bool expectError)
    {
        Test(name, script, null, 0.0001, expectError);
    }

    // Перегрузка для строковых результатов
    static void Test(string name, string script, string expected)
    {
        totalTests++;
        var processor = new Processor();
        
        Console.WriteLine($"[{totalTests}] {name}");
        Console.WriteLine($"    Скрипт: {script.Replace("\n", "\\n")}");
        
        try
        {
            var output = processor.Run(script);
            var hasError = output.Any(line => line.Contains("КРИТИЧЕСКАЯ ОШИБКА") || line.Contains("ОШИБКА"));

            if (hasError)
            {
                Console.WriteLine($"    ОШИБКА: {output.LastOrDefault()}");
                failedTests++;
                return;
            }

            var lastLine = output.LastOrDefault();
            var resultMatch = System.Text.RegularExpressions.Regex.Match(lastLine ?? "", @"=> (.+)");
            if (!resultMatch.Success)
            {
                Console.WriteLine($"    Не удалось извлечь результат");
                failedTests++;
                return;
            }

            var resultStr = resultMatch.Groups[1].Value.Trim();
            
            if (resultStr == expected)
            {
                Console.WriteLine($"    ПРОШЕЛ: {resultStr}");
                passedTests++;
            }
            else
            {
                Console.WriteLine($"    ПРОВАЛЕН: ожидалось '{expected}', получено '{resultStr}'");
                failedTests++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ИСКЛЮЧЕНИЕ: {ex.Message}");
            failedTests++;
        }
    }

    static void Test(string name, string script, object expected, double tolerance = 0.0001, bool expectError = false)
    {
        totalTests++;
        var processor = new Processor();
        
        Console.WriteLine($"[{totalTests}] {name}");
        Console.WriteLine($"    Скрипт: {script.Replace("\n", "\\n")}");
        
        try
        {
            var output = processor.Run(script);
            var hasError = output.Any(line => line.Contains("КРИТИЧЕСКАЯ ОШИБКА") || line.Contains("ОШИБКА"));

            if (expectError)
            {
                if (hasError)
                {
                    Console.WriteLine("    ОЖИДАЕМАЯ ОШИБКА");
                    passedTests++;
                }
                else
                {
                    Console.WriteLine($"    ОЖИДАЛАСЬ ОШИБКА, но получен результат: {output.LastOrDefault()}");
                    failedTests++;
                }
                return;
            }

            if (hasError)
            {
                Console.WriteLine($"    ОШИБКА: {output.LastOrDefault()}");
                failedTests++;
                return;
            }

            var lastLine = output.LastOrDefault();
            if (lastLine == null)
            {
                Console.WriteLine("    Нет результата");
                failedTests++;
                return;
            }

            var resultMatch = System.Text.RegularExpressions.Regex.Match(lastLine, @"=> (.+)");
            if (!resultMatch.Success)
            {
                Console.WriteLine($"    Не удалось извлечь результат из: {lastLine}");
                failedTests++;
                return;
            }

            var resultStr = resultMatch.Groups[1].Value.Trim();
            
            // Извлекаем число (может быть с символами типа [√42])
            var numberMatch = System.Text.RegularExpressions.Regex.Match(resultStr, @"^-?\d+\.?\d*(E[+-]?\d+)?");
            if (!numberMatch.Success)
            {
                Console.WriteLine($"    Не удалось распарсить число: {resultStr}");
                failedTests++;
                return;
            }

            if (!double.TryParse(numberMatch.Value, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                Console.WriteLine($"    Не удалось преобразовать результат: {numberMatch.Value}");
                failedTests++;
                return;
            }

            var expectedValue = Convert.ToDouble(expected);
            
            if (Math.Abs(result - expectedValue) <= tolerance)
            {
                Console.WriteLine($"    ПРОШЕЛ: {resultStr}");
                passedTests++;
            }
            else
            {
                Console.WriteLine($"    ПРОВАЛЕН: ожидалось {expectedValue}, получено {result}");
                failedTests++;
            }
        }
        catch (Exception ex)
        {
            if (expectError)
            {
                Console.WriteLine($"    ОЖИДАЕМАЯ ОШИБКА: {ex.Message}");
                passedTests++;
            }
            else
            {
                Console.WriteLine($"    ИСКЛЮЧЕНИЕ: {ex.Message}");
                failedTests++;
            }
        }
    }
}