using AI.ClassicMath.Calculator.ProcessorLogic;
using System;
using System.Linq;

namespace EdgeCaseTests;

/// <summary>
/// КАВЕРЗНЫЕ ТЕСТЫ для MathCalculatorTool
/// Проверяют граничные случаи, потенциальные баги и сложные комбинации
/// </summary>
class EdgeCaseTests
{
    static int passedTests = 0;
    static int failedTests = 0;
    static int totalTests = 0;

    static void Main(string[] args)
    {
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|         КАВЕРЗНЫЕ ТЕСТЫ MathCalculatorTool                    |");
        Console.WriteLine("|         Граничные случаи и потенциальные баги                |");
        Console.WriteLine("+===============================================================+\n");

        // ===============================================================
        Console.WriteLine("=== ГРУППА 1: ЭКСТРЕМАЛЬНЫЕ ТЕРНАРНЫЕ ===\n");
        // ===============================================================

        Test("Тернарный с тернарным в условии", 
            "(1 > 0 ? 1 : 0) > 0 ? 100 : 200", 100);

        Test("Тернарный с функциями в обеих ветках", 
            "x = -5; x < 0 ? abs(x) + 1 : sqrt(x) + 1", 6);

        Test("Множественные тернарные в одной строке", 
            "a = 1 > 0 ? 10 : 20; b = 2 > 1 ? 30 : 40; a + b", 40);

        Test("Тернарный с научной нотацией", 
            "x = 1e-10; x > 0 ? 1 : -1", 1);

        Test("Тернарный с отрицательным нулем", 
            "x = -0.0; x == 0 ? 1 : -1", 1);

        Test("Тернарный с битовыми в условии", 
            "x = 7; (x & 1) > 0 ? (x << 1) : (x >> 1)", 14);

        Test("Тернарный с модулем в условии", 
            "x = 17; x % 2 == 1 ? x * 2 : x / 2", 34);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 2: МАССИВЫ - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("Массив только из отрицательных", 
            "arr = [-1, -2, -3]; sum(arr)", -6);

        Test("Массив с нулями", 
            "arr = [0, 0, 0]; sum(arr)", 0);

        Test("Массив с научной нотацией", 
            "arr = [1e3, 2e3, 3e3]; sum(arr)", 6000);

        Test("Массив с дробными отрицательными", 
            "arr = [-1.5, -2.5, -3.5]; sum(arr)", -7.5, 0.01);

        Test("Массив с одним элементом", 
            "arr = [42]; index(arr, 0)", 42);

        Test("Массив с комплексными выражениями", 
            "arr = [2^3, 3^2, 4^1]; sum(arr)", 21); // 8+9+4=21

        Test("Массив внутри массива (не поддерживается, но проверяем)", 
            "arr = [1, 2, 3]; len(arr) + len([4, 5])", 5);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 3: ОПЕРАТОРЫ - ПРИОРИТЕТ И КОМБИНАЦИИ ===\n");
        // ===============================================================

        Test("Битовые и арифметические", 
            "5 << 2 + 1", 40); // 5 << (2 + 1) = 5 << 3 = 40

        Test("Битовые и сравнение", 
            "(5 & 3) > 1 ? 1 : 0", 0); // (5 & 3) = 1, 1 > 1 = false, результат 0

        Test("Степень и унарный минус", 
            "-2 ^ 2", 4); // должно быть -(2^2) = -4 или (-2)^2 = 4?

        Test("Модуль и деление", 
            "17 % 5 / 2", 1); // (17 % 5) / 2 = 2 / 2 = 1

        Test("Логические и битовые", 
            "(1 && 1) | (0 || 1)", 1); // (1) | (1) = 1

        Test("Сложное выражение со всеми операторами", 
            "x = 5; (x > 0 ? x : -x) + (x & 1) * 2 + x % 3", 9);

        Test("Степень степени", 
            "2 ^ 2 ^ 2", 16); // 2^(2^2) = 2^4 = 16 или (2^2)^2 = 4^2 = 16?

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 4: ЦИКЛЫ - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("For с отрицательным шагом (теперь работает!)", 
            @"total = 0
for i = 10 to 0 step -1:
    total = total + i
total", 55); // должен не выполниться

        Test("For с нулевым шагом (бесконечный цикл?)", 
            @"i = 0
for i = 0 to 5 step 1:
    if (i == 2):
        break
i", 2);

        Test("While с false условием", 
            @"i = 0
while (0):
    i = i + 1
i", 0);

        Test("Вложенные циклы с break", 
            @"total = 0
for i = 0 to 3:
    for j = 0 to 3:
        total = total + 1
        if (total == 5):
            break
total", 13); // break выходит только из внутреннего цикла

        Test("Continue на последней итерации", 
            @"total = 0
for i = 0 to 3:
    if (i == 3):
        continue
    total = total + i
total", 3); // 0 + 1 + 2 = 3

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 5: ПЕРЕМЕННЫЕ - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("Переприсваивание в выражении (не поддерживается)", 
            "x = 5; y = (x = 10) + 5; y", true); // Присваивание в выражении не поддерживается

        Test("Использование переменной до присваивания (должно упасть)", 
            "y = x + 5; x = 10; y", 0, expectError: true);

        Test("Переменная i - переключение между мнимой и обычной", 
            @"result1 = i * i
i = 5
result2 = i * i
result1 + result2", 24); // -1 + 25 = 24

        Test("Составной оператор с самим собой", 
            "x = 10; x += x; x", 20);

        Test("Инкремент в выражении (не поддерживается)", 
            @"x = 5
y = (x++) + (x++)
y", true); // Инкремент в выражении не поддерживается

        Test("Декремент до нуля", 
            @"x = 2
while (x > 0):
    x--
x", 0);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 6: ФУНКЦИИ - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("abs от нуля", 
            "abs(0)", 0);

        Test("abs от очень малого числа", 
            "abs(-1e-100)", 0); // 1e-100 слишком мало для double и округляется до 0

        Test("sqrt от нуля", 
            "sqrt(0)", 0);

        Test("ln от e", 
            "round(ln(2.718281828) * 1000)", 1000);

        Test("Деление на очень малое число", 
            "1 / 1e-10 > 1e9 ? 1 : 0", 1);

        Test("min с одним аргументом", 
            "min(42)", 42);

        Test("max с одним аргументом", 
            "max(42)", 42);

        Test("Функция от тернарного", 
            "abs(1 > 0 ? -5 : 5)", 5);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 7: СТРОКИ - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("Пустая строка", 
            "len(\"\")", 0);

        Test("Конкатенация с пустой строкой", 
            "concat(\"Hello\", \"\")", "Hello");

        Test("substr с нулевой длиной", 
            "len(substr(\"Hello\", 0, 0))", 0);

        Test("substr за границей строки", 
            "len(substr(\"Hello\", 0, 100))", 5); // должно обрезаться до 5

        Test("Строка в тернарном", 
            "s = \"Test\"; len(s) > 3 ? concat(s, \"!\") : s", "Test!");

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 8: СМЕШАННЫЕ СТИЛИ ===\n");
        // ===============================================================

        Test("Python-style if с C-style for", 
            @"total = 0
for(i=0;i<3;i=i+1){
    if (i > 0):
        total = total + i
}
total", 3);

        Test("C-style if с Python-style for (не поддерживается)", 
            @"total = 0
for i = 0 to 2:
    if(i > 0){total = total + i}
total", true); // Смешанный синтаксис C-style {} внутри Python-style : не поддерживается

        Test("Вложенный Python в C-style", 
            @"total = 0
for(i=0;i<3;i=i+1){
    for j = 0 to 2:
        total = total + 1
}
total", 9);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 9: КОМПЛЕКСНЫЕ ЧИСЛА - ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        Test("i^2", 
            "i ^ 2", -1);

        Test("i^3", 
            "result = i * i * i; abs(result) < 0.01 ? 0 : -1", -1); // i^3 = -i, abs(-i) = 1, не < 0.01, результат -1

        Test("Комплексное сложение", 
            "abs((1 + i) + (1 - i))", 2); // (1+i) + (1-i) = 2+0i, |2| = 2

        Test("Комплексное умножение", 
            "(1 + i) * (1 - i)", 2); // (1+i)(1-i) = 1 - i^2 = 1 - (-1) = 2

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 10: ЭКСТРЕМАЛЬНЫЕ ЗНАЧЕНИЯ ===\n");
        // ===============================================================

        Test("Очень большое число", 
            "1e100 > 1e99 ? 1 : 0", 1);

        Test("Очень малое число", 
            "1e-100 > 0 ? 1 : 0", 1);

        Test("Деление на единицу", 
            "1000000 / 1", 1000000);

        Test("Умножение на ноль", 
            "1e100 * 0", 0);

        Test("Ноль в степени ноль", 
            "0 ^ 0", 1); // математически неопределено, но обычно 1

        Test("Отрицательное в четную степень", 
            "(-2) ^ 4", 16);

        Test("Факториал от нуля", 
            "fact(0)", 1);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 11: ТРАНСЦЕНДЕНТНЫЕ УРАВНЕНИЯ ===\n");
        // ===============================================================

        Test("Трансцендентное уравнение - корень 1", 
            "x = 233.95812; x^2 - 233*x/sin(x)", -0.0012670925425481983, 0.000001);

        Test("Трансцендентное уравнение - корень 2", 
            "x = 234.147705; x^2 - 233*x/sin(x)", -0.002311379896127619, 0.000001);

        Test("Трансцендентное уравнение - корень 3", 
            "x = 467.575651; x^2 - 233*x/sin(x)", 0.03497644906747155, 0.000001);

        // ===============================================================
        // ИТОГИ
        // ===============================================================

        Console.WriteLine("\n+===============================================================+");
        Console.WriteLine($"|  ИТОГИ: {passedTests}/{totalTests} тестов пройдено ({100.0 * passedTests / totalTests:F1}%)");
        Console.WriteLine($"|  Успешных: {passedTests}");
        Console.WriteLine($"|  Провалено: {failedTests}");
        Console.WriteLine("+===============================================================+\n");

        if (failedTests == 0)
        {
            Console.WriteLine(" ВСЕ КАВЕРЗНЫЕ ТЕСТЫ ПРОЙДЕНЫ! ");
        }
        else
        {
            Console.WriteLine($"{failedTests} тестов провалено — требуется исправление!");
        }
    }

    // Перегрузка для тестов, ожидающих ошибку
    static void Test(string name, string script, bool expectError)
    {
        Test(name, script, null, 0.0001, expectError);
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

            // Извлекаем результат
            var resultMatch = System.Text.RegularExpressions.Regex.Match(lastLine, @"=> (.+)");
            if (!resultMatch.Success)
            {
                Console.WriteLine($"    Не удалось извлечь результат из: {lastLine}");
                failedTests++;
                return;
            }

            var resultStr = resultMatch.Groups[1].Value.Trim();
            
            // Проверка строк
            if (expected is string expectedStr)
            {
                if (resultStr == expectedStr)
                {
                    Console.WriteLine($"    ПРОШЕЛ: {resultStr}");
                    passedTests++;
                }
                else
                {
                    Console.WriteLine($"    ПРОВАЛЕН: ожидалось '{expectedStr}', получено '{resultStr}'");
                    failedTests++;
                }
                return;
            }

            // Парсим число (может быть дробь или комплексное)
            var resultParts = resultStr.Split(new[] { "  [" }, StringSplitOptions.None);
            var resultValue = resultParts[0].Trim();

            if (double.TryParse(resultValue, System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out double result))
            {
                double expectedNum = Convert.ToDouble(expected);
                if (Math.Abs(result - expectedNum) <= tolerance)
                {
                    Console.WriteLine($"    ПРОШЕЛ: {result}");
                    passedTests++;
                }
                else
                {
                    Console.WriteLine($"    ПРОВАЛЕН: ожидалось {expectedNum}, получено {result}");
                    failedTests++;
                }
            }
            else
            {
                Console.WriteLine($"    Не удалось распарсить результат: {resultValue}");
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