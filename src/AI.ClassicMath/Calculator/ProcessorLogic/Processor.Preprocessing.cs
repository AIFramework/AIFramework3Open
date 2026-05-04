using AI.ClassicMath.MatrixUtils.FindFraction;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Complex = System.Numerics.Complex;
using System.Text.RegularExpressions;
using System.Threading;

namespace AI.ClassicMath.Calculator.ProcessorLogic;

public partial class Processor
{
    /// <summary>
    /// Предобработка скрипта: разбивает точки с запятой на отдельные строки
    /// </summary>
    private string PreprocessScript(string script)
    {
        // Шаг 1: Удаляем комментарии в стиле Python (#)
        // ВАЖНО: Посимвольная обработка С отслеживанием строковых литералов!
        // Это защищает # внутри строк от удаления (например: "Test#123")
        var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            bool inString = false;
            
            // Идем по строке посимвольно, отслеживая вход/выход из строковых литералов
            for (int j = 0; j < line.Length; j++)
            {
                // Проверяем кавычки (учитываем escaped символы)
                if (line[j] == '"')
                {
                    // Подсчитываем количество backslash'ей ПЕРЕД кавычкой
                    int backslashCount = 0;
                    int k = j - 1;
                    while (k >= 0 && line[k] == '\\')
                    {
                        backslashCount++;
                        k--;
                    }
                    
                    // Если четное количество backslash'ей (включая 0), то кавычка НЕ escaped
                    // Нечетное количество - кавычка escaped
                    if (backslashCount % 2 == 0)
                    {
                        inString = !inString;  // Переключаем флаг "внутри строки"
                    }
                    // Если нечетное - это escaped кавычка, не меняем флаг
                }
                // Удаляем # ТОЛЬКО если он НЕ внутри строки
                else if (line[j] == '#' && !inString)
                {
                    lines[i] = line.Substring(0, j);  // Обрезаем от # до конца строки
                    break;
                }
            }
        }
        // Шаг 1.5: Склеиваем строки с незакрытыми скобками
        // Если строка содержит больше '(' чем ')', значит вызов функции продолжается
        // на следующей строке — склеиваем до баланса скобок.
        // Учитываем строковые литералы, чтобы скобки внутри "" не считались.
        var joinedLines = new List<string>();
        string accumulator = "";
        int parenDepth = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string ln = lines[i];

            if (parenDepth > 0)
            {
                // Продолжаем склейку — добавляем через пробел (вместо \n)
                accumulator += " " + ln.Trim();
            }
            else
            {
                accumulator = ln;
            }

            // Считаем скобки вне строковых литералов и вне // комментариев
            // (#-комментарии уже удалены в Шаге 1, но //-комментарии ещё нет)
            int commentStart = FindCommentIndex(ln);
            int scanEnd = commentStart >= 0 ? commentStart : ln.Length;
            bool inStr = false;
            for (int c = 0; c < scanEnd; c++)
            {
                if (ln[c] == '"')
                {
                    int bs = 0;
                    int k2 = c - 1;
                    while (k2 >= 0 && ln[k2] == '\\') { bs++; k2--; }
                    if (bs % 2 == 0) inStr = !inStr;
                }
                else if (!inStr)
                {
                    if (ln[c] == '(') parenDepth++;
                    else if (ln[c] == ')') parenDepth--;
                }
            }

            if (parenDepth <= 0)
            {
                joinedLines.Add(accumulator);
                parenDepth = 0;
                accumulator = "";
            }
        }
        // Если осталась незавершённая строка — добавляем как есть
        if (!string.IsNullOrEmpty(accumulator))
            joinedLines.Add(accumulator);

        script = string.Join("\n", joinedLines);
        
        // Шаг 2: Защищаем строковые литералы от изменения
        var strings = new List<string>();
        var stringPattern = @"""(?:[^""\\]|\\.)*""";
        script = Regex.Replace(script, stringPattern, m =>
        {
            strings.Add(m.Value);
            return $"__STRING_{strings.Count - 1}__";
        });
        
        // УЛУЧШЕНИЕ 6: Разбиваем inline-блоки if(...){...}else{...} на многострочные
        // Просто добавляем переводы строк перед/после { и }
        script = script.Replace("{", "\n{\n");
        script = script.Replace("}", "\n}\n");
        
        // Теперь заменяем ; на \n (только вне строк и вне for(...;...;...))
        // Защищаем for(...;...;...) от разбивки
        var forPattern = @"for\s*\([^)]+\)";
        var forLoops = new List<string>();
        script = Regex.Replace(script, forPattern, m =>
        {
            forLoops.Add(m.Value);
            return $"__FOR_{forLoops.Count - 1}__";
        });
        
        // Заменяем ; на \n
        script = script.Replace(";", "\n");
        
        // Восстанавливаем for-циклы
        for (int i = 0; i < forLoops.Count; i++)
        {
            script = script.Replace($"__FOR_{i}__", forLoops[i]);
        }
        
        // Восстанавливаем строковые литералы
        for (int i = 0; i < strings.Count; i++)
        {
            script = script.Replace($"__STRING_{i}__", strings[i]);
        }
        
        return script;
    }

    /// <summary>
    /// Находит индекс начала комментария "//" в строке, пропуская "//" внутри строковых литералов.
    /// Возвращает -1, если комментарий не найден.
    /// </summary>
    private static int FindCommentIndex(string line)
    {
        bool inString = false;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                // Подсчитываем количество backslash'ей ПЕРЕД кавычкой
                int backslashCount = 0;
                int k = i - 1;
                while (k >= 0 && line[k] == '\\')
                {
                    backslashCount++;
                    k--;
                }

                // Если четное количество backslash'ей (включая 0), то кавычка НЕ escaped
                if (backslashCount % 2 == 0)
                {
                    inString = !inString;
                }
            }
            else if (!inString && line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                return i;
            }
        }

        return -1;
    }
}
