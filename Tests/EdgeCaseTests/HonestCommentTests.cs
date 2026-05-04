using AI.ClassicMath.Calculator.ProcessorLogic;
using System;
using System.Linq;

namespace EdgeCaseTests;

/// <summary>
/// ЧЕСТНЫЕ и ОБЪЕКТИВНЫЕ тесты комментариев
/// Проверяем РЕАЛЬНОЕ содержимое строк, а не только длину!
/// </summary>
class HonestCommentTests
{
    static int passedTests = 0;
    static int failedTests = 0;
    static int totalTests = 0;

    /// <summary>
    /// Честный тест: проверяет РЕАЛЬНОЕ содержимое строки
    /// </summary>
    static void TestString(string testName, string script, string expectedString)
    {
        totalTests++;
        try
        {
            var processor = new Processor();
            var output = processor.Run(script);
            var hasError = output.Any(line => line.Contains("КРИТИЧЕСКАЯ ОШИБКА") || line.Contains("ОШИБКА"));

            if (hasError)
            {
                failedTests++;
                Console.WriteLine($"{testName}");
                Console.WriteLine($"   Ошибка выполнения:");
                foreach (var line in output)
                {
                    Console.WriteLine($"   {line}");
                }
                return;
            }

            // Ищем результат (последнюю строку с =>)
            string? actualString = null;
            foreach (var line in output)
            {
                if (line.Contains("=>"))
                {
                    var parts = line.Split(new[] { "=>" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        actualString = parts[1].Trim();
                        // Убираем дополнительную информацию в скобках если есть
                        var bracketIndex = actualString.IndexOf('[');
                        if (bracketIndex > 0)
                        {
                            actualString = actualString.Substring(0, bracketIndex).Trim();
                        }
                    }
                }
            }

            bool passed = actualString == expectedString;

            if (passed)
            {
                passedTests++;
                Console.WriteLine($"{testName}");
                Console.WriteLine($"   Результат: \"{actualString}\"");
            }
            else
            {
                failedTests++;
                Console.WriteLine($"{testName}");
                Console.WriteLine($"   Ожидалось: \"{expectedString}\"");
                Console.WriteLine($"   Получено:  \"{actualString}\"");
            }
        }
        catch (Exception ex)
        {
            failedTests++;
            Console.WriteLine($"{testName}");
            Console.WriteLine($"   Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Честный тест: проверяет числовое значение
    /// </summary>
    static void TestNumber(string testName, string script, double expected, double precision = 1e-8)
    {
        totalTests++;
        try
        {
            var processor = new Processor();
            var output = processor.Run(script);
            var hasError = output.Any(line => line.Contains("КРИТИЧЕСКАЯ ОШИБКА") || line.Contains("ОШИБКА"));

            if (hasError)
            {
                failedTests++;
                Console.WriteLine($"{testName}");
                Console.WriteLine($"   Ошибка выполнения:");
                foreach (var line in output)
                {
                    Console.WriteLine($"   {line}");
                }
                return;
            }

            // Ищем результат
            double? actual = null;
            foreach (var line in output)
            {
                if (line.Contains("=>"))
                {
                    var parts = line.Split(new[] { "=>" }, StringSplitOptions.None);
                    if (parts.Length >= 2)
                    {
                        var valueStr = parts[1].Trim().Split(new[] { ' ', '[' })[0];
                        if (double.TryParse(valueStr, System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, out var val))
                        {
                            actual = val;
                        }
                    }
                }
            }

            bool passed = actual.HasValue && Math.Abs(actual.Value - expected) < precision;

            if (passed)
            {
                passedTests++;
                Console.WriteLine($"{testName}");
                Console.WriteLine($"   Результат: {actual}");
            }
            else
            {
                failedTests++;
                Console.WriteLine($"{testName}");
                Console.WriteLine($"   Ожидалось: {expected}");
                Console.WriteLine($"   Получено:  {actual}");
            }
        }
        catch (Exception ex)
        {
            failedTests++;
            Console.WriteLine($"{testName}");
            Console.WriteLine($"   Exception: {ex.Message}");
        }
    }

    static void Main(string[] args)
    {
        Console.WriteLine("+===============================================================+");
        Console.WriteLine("|  ЧЕСТНЫЕ И ОБЪЕКТИВНЫЕ ТЕСТЫ КОММЕНТАРИЕВ                   |");
        Console.WriteLine("|  Проверяем РЕАЛЬНОЕ содержимое, а не только длину!          |");
        Console.WriteLine("+===============================================================+\n");

        // ===============================================================
        Console.WriteLine("=== ГРУППА 1: # ВНУТРИ СТРОК (КРИТИЧЕСКИЕ ТЕСТЫ) ===\n");
        // ===============================================================

        TestString("КРИТИЧЕСКИЙ: '#Привет!'",
            @"a = ""#Привет!""
a",
            "#Привет!");

        TestString("КРИТИЧЕСКИЙ: 'Test#123'",
            @"s = ""Test#123""
s",
            "Test#123");

        TestString("КРИТИЧЕСКИЙ: '#' в начале",
            @"s = ""#начало текста""
s",
            "#начало текста");

        TestString("КРИТИЧЕСКИЙ: '#' в середине",
            @"s = ""текст#середина""
s",
            "текст#середина");

        TestString("КРИТИЧЕСКИЙ: '#' в конце",
            @"s = ""текст в конце#""
s",
            "текст в конце#");

        TestString("КРИТИЧЕСКИЙ: Несколько '#'",
            @"s = ""#один#два#три#""
s",
            "#один#два#три#");

        TestString("КРИТИЧЕСКИЙ: Только '#'",
            @"s = ""#""
s",
            "#");

        TestString("КРИТИЧЕСКИЙ: Хэштеги",
            @"s = ""#hashtag #тег #123""
s",
            "#hashtag #тег #123");

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 2: КОММЕНТАРИИ ВНЕ СТРОК (ДОЛЖНЫ УДАЛЯТЬСЯ) ===\n");
        // ===============================================================

        TestString("Комментарий после строки",
            @"s = ""Привет""  # Это комментарий
s",
            "Привет");

        TestString("Комментарий НЕ влияет на переменную",
            @"x = ""Значение""  # Комментарий с #хэштегом
x",
            "Значение");

        TestNumber("Комментарий после числа",
            @"x = 42  # Ответ на всё
x",
            42.0);

        TestNumber("Комментарий НЕ обрезает код",
            @"a = 10  # Первое
b = 20  # Второе
a + b",
            30.0);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 3: СМЕШАННЫЕ СЛУЧАИ (СТРОКИ + КОММЕНТАРИИ) ===\n");
        // ===============================================================

        TestString("Строка с # + комментарий с #",
            @"s = ""#Привет""  # Комментарий с #хэштегом
s",
            "#Привет");

        TestString("Две строки с # + комментарии",
            @"s1 = ""#один""  # Первый
s2 = ""два#""  # Второй
concat(s1, s2)",
            "#одиндва#");

        TestString("Пустая строка + комментарий",
            @"s = """"  # Пустая строка
s",
            "");

        TestNumber("Строка с # внутри выражения",
            @"s1 = ""Test#123""
s2 = ""#Start""
len(s1) + len(s2)",
            14.0);  // "Test#123" (8) + "#Start" (6) = 14

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 4: ESCAPED СИМВОЛЫ (\\) + КОММЕНТАРИИ ===\n");
        // ===============================================================

        TestString("Строка с \\\\ + комментарий",
            @"s = ""C:\\Users""  # Путь Windows
s",
            "C:\\\\Users");  // Калькулятор НЕ обрабатывает escape, хранит \\ как есть

        TestString("Строка с \\\\ и # внутри + комментарий",
            @"s = ""Path\\#folder""  # Комментарий
s",
            "Path\\\\#folder");

        TestString("Строка с \\\\ перед кавычкой + комментарий",
            @"s = ""Text\\""  # Комментарий НЕ должен удалиться после \\"" 
s",
            "Text\\\\");

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 5: ГРАНИЧНЫЕ СЛУЧАИ ===\n");
        // ===============================================================

        TestNumber("Только комментарий (пустой результат)",
            @"# Только комментарий
x = 0
x",
            0.0);

        TestString("# в начале строки кода",
            @"# Комментарий
s = ""Текст""
s",
            "Текст");

        TestNumber("Множественные # в комментарии",
            @"x = 10  ### Важный комментарий ###
x",
            10.0);

        TestNumber("# без пробела",
            @"x = 5#комментарий
x",
            5.0);

        // ===============================================================
        Console.WriteLine("\n=== ГРУППА 6: СЛОЖНЫЕ КОНСТРУКЦИИ ===\n");
        // ===============================================================

        TestString("МАССИВЫ СТРОК: Массив строк с # + индексация",
            @"arr = [""#один"", ""два#"", ""#три#""]  # Массив строк
index(arr, 1)",
            "два#");

        TestNumber("МАССИВЫ СТРОК: len() массива строк",
            @"arr = [""#test"", ""hello"", ""world""]
len(arr)",
            3.0);

        TestString("МАССИВЫ СТРОК: Конкатенация элементов",
            @"arr = [""#start"", ""middle#"", ""end#""]  # Массив с хэштегами
concat(index(arr, 0), index(arr, 2))",
            "#startend#");

        TestNumber("Работа с несколькими строками с #",
            @"s1 = ""#один""
s2 = ""два#три""
len(s1) + len(s2)",
            12.0);  // 5 + 7 = 12

        TestString("Условие if с комментарием",
            @"x = 5  # Значение
if x > 3:  # Проверка
    result = ""Больше""  # Результат
else:
    result = ""Меньше""
result",
            "Больше");

        TestString("Цикл с комментариями",
            @"# Цикл
s = """"  # Пустая
for i = 0 to 2:  # От 0 до 2
    s = ""Done""  # Устанавливаем
s",
            "Done");

        // Финальная статистика
        Console.WriteLine("\n+===============================================================+");
        Console.WriteLine($"|  ИТОГИ: {passedTests}/{totalTests} тестов пройдено ({(passedTests * 100.0 / totalTests):F1}%)");
        Console.WriteLine($"|  Успешных: {passedTests}");
        Console.WriteLine($"|  Провалено: {failedTests}");
        Console.WriteLine("+===============================================================+\n");

        if (failedTests > 0)
        {
            Console.WriteLine("Есть непрошедшие тесты!");
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine(" ВСЕ ЧЕСТНЫЕ ТЕСТЫ ПРОЙДЕНЫ!");
            Environment.Exit(0);
        }
    }
}