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
    private List<Statement> ParseStatements(Queue<string> lines, bool isSilentContext, CancellationToken cancellationToken = default)
    {
        var statements = new List<Statement>();
        while (lines.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var rawLine = lines.Dequeue();

            // Находим комментарий (с учётом строковых литералов, чтобы "//" внутри строк не обрезалось).
            int commentIndex = FindCommentIndex(rawLine);
            // Обрезаем строку, если комментарий найден, иначе берем ее целиком.
            string line = commentIndex >= 0 ? rawLine.Substring(0, commentIndex) : rawLine;
            // Убираем пробелы.
            line = line.Trim();

            // Если после обрезки ничего не осталось, пропускаем строку.
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Проверка на break и continue
            if (line == "break")
            {
                statements.Add(new BreakStatement());
                continue;
            }
            if (line == "continue")
            {
                statements.Add(new ContinueStatement());
                continue;
            }

            var ifMatch = Regex.Match(line, @"^if\s*\((.*)\)\s*\{?$");
            var ifColonMatch = Regex.Match(line, @"^if\s+(.+):\s*$");  // Python-style: if condition:
            var elifMatch = Regex.Match(line, @"^elif\s+(.+):\s*$");  // Python-style: elif condition:
            var whileMatch = Regex.Match(line, @"^while\s*\((.*)\)\s*\{?$");
            var whileColonMatch = Regex.Match(line, @"^while\s+(.+):\s*$");  // Python-style: while condition:
            // C-style for: for(init; condition; increment) - делаем части опциональными
            var forMatch = Regex.Match(line, @"^for\s*\(([^;]*);([^;]*);([^)]*)\)\s*\{?$");
            var forToMatch = Regex.Match(line, @"^for\s+(\w+)\s*=\s*(.+?)\s+to\s+(.+?)(?:\s+step\s+(.+?))?\s*:\s*$");

            if (ifMatch.Success)
            {
                var condition = ifMatch.Groups[1].Value;
                var trueBody = ParseBlock(lines, cancellationToken);
                List<Statement> falseBody = null;
                if (lines.Count > 0 && lines.Peek().Trim().StartsWith("else"))
                {
                    lines.Dequeue();
                    falseBody = ParseBlock(lines, cancellationToken);
                }
                statements.Add(new IfStatement(condition, trueBody, falseBody));
            }
            else if (ifColonMatch.Success)
            {
                // Python-style if с отступами
                var condition = ifColonMatch.Groups[1].Value;
                var trueBody = ParseIndentedBlock(lines, cancellationToken);
                List<Statement> falseBody = null;
                
                // Рекурсивная обработка elif/else
                falseBody = ParseElifElse(lines, cancellationToken);
                
                statements.Add(new IfStatement(condition, trueBody, falseBody));
            }
            else if (whileMatch.Success)
            {
                var condition = whileMatch.Groups[1].Value;
                var body = ParseBlock(lines, cancellationToken);
                statements.Add(new WhileStatement(condition, body));
            }
            else if (whileColonMatch.Success)
            {
                // Python-style while с отступами
                var condition = whileColonMatch.Groups[1].Value;
                var body = ParseIndentedBlock(lines, cancellationToken);
                statements.Add(new WhileStatement(condition, body));
            }
            else if (forToMatch.Success)
            {
                // Синтаксис: for i = 1 to 100: или for i = 1 to 100 step 2:
                var varName = forToMatch.Groups[1].Value;
                var startExpr = forToMatch.Groups[2].Value;
                var endExpr = forToMatch.Groups[3].Value;
                var stepExpr = forToMatch.Groups[4].Success ? forToMatch.Groups[4].Value : "1";
                
                // Преобразуем в стандартный for
                var initializer = $"{varName} = {startExpr}";
                
                // ИСПРАВЛЕНИЕ: Поддержка отрицательных шагов
                // Условие: (step > 0 && i <= end) || (step < 0 && i >= end)
                // Упрощённо: используем общее условие, которое работает для обоих направлений
                // Если step положительный: i <= end
                // Если step отрицательный: i >= end
                // Универсальное условие: ((i - end) * step) <= 0
                var stepVar = $"__step_{varName}";
                var endVar = $"__end_{varName}";
                var condition = $"(({varName} - {endVar}) * {stepVar}) <= 0";
                var increment = $"{varName} = {varName} + {stepVar}";
                
                // Добавляем инициализацию вспомогательных переменных
                statements.Add(new ExpressionStatement($"{stepVar} = {stepExpr}"));
                statements.Add(new ExpressionStatement($"{endVar} = {endExpr}"));
                
                var body = ParseIndentedBlock(lines, cancellationToken);
                statements.Add(new ForStatement(initializer, condition, increment, body));
            }
            else if (forMatch.Success)
            {
                var initializer = forMatch.Groups[1].Value.Trim();
                var condition = forMatch.Groups[2].Value.Trim();
                var increment = forMatch.Groups[3].Value.Trim();
                
                // Проверяем, есть ли { на той же строке
                if (line.EndsWith("{"))
                {
                    var body = ParseBlock(lines, cancellationToken);
                    statements.Add(new ForStatement(initializer, condition, increment, body));
                }
                else
                {
                    // Однострочное тело или блок на следующей строке
                    var body = ParseBlock(lines, cancellationToken);
                    statements.Add(new ForStatement(initializer, condition, increment, body));
                }
            }
            else if (line == "{" || line.StartsWith("else")) { throw new ArgumentException($"Неожиданный токен: '{line}'"); }
            else { statements.Add(new ExpressionStatement(line, isSilentContext)); }
        }
        return statements;
    }

    private List<Statement> ParseBlock(Queue<string> lines, CancellationToken cancellationToken = default)
    {
        var blockLines = new Queue<string>();
        int braceLevel = 1;
        if (lines.Count > 0 && lines.Peek().Trim() == "{") lines.Dequeue();
        while (lines.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var line = lines.Dequeue();

            // Игнорируем скобки внутри комментариев (с учётом строковых литералов)
            int commentIndex = FindCommentIndex(line);
            string codePart = commentIndex >= 0 ? line.Substring(0, commentIndex) : line;

            if (codePart.Contains("{")) braceLevel++;
            if (codePart.Contains("}")) braceLevel--;

            if (braceLevel == 0)
            {
                // Проверяем, есть ли else на той же строке после }
                var closingIndex = codePart.LastIndexOf('}');
                var afterClosing = codePart.Substring(closingIndex + 1).Trim();
                
                if (afterClosing.StartsWith("else"))
                {
                    // Возвращаем "else ..." обратно в очередь как отдельную строку
                    var remainingLine = line.Substring(line.LastIndexOf('}') + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(remainingLine))
                    {
                        var newQueue = new List<string> { remainingLine };
                        newQueue.AddRange(lines);
                        lines = new Queue<string>(newQueue);
                    }
                }
                
                // Добавляем всё что было до }
                var beforeClosing = line.Substring(0, line.LastIndexOf('}'));
                if (!string.IsNullOrWhiteSpace(beforeClosing.Trim()))
                {
                    blockLines.Enqueue(beforeClosing);
                }
                break;
            }
            blockLines.Enqueue(line);
        }
        if (braceLevel != 0) throw new ArgumentException("Нарушен баланс скобок '{}' в блоке кода.");
        return ParseStatements(blockLines, isSilentContext: true, cancellationToken);
    }

    private List<Statement> ParseIndentedBlock(Queue<string> lines, CancellationToken cancellationToken = default)
    {
        // Парсинг блока с отступами (Python-style) для циклов 'for i = 1 to N:'
        var blockLines = new Queue<string>();
        
        // Минимальный отступ для определения принадлежности к блоку
        int? blockIndent = null;
        
        while (lines.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var line = lines.Peek();
            
            // Убираем комментарии для проверки отступов (с учётом строковых литералов)
            int commentIndex = FindCommentIndex(line);
            string lineWithoutComment = commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
            
            // Если строка пустая или только комментарий - пропускаем
            if (string.IsNullOrWhiteSpace(lineWithoutComment))
            {
                lines.Dequeue();
                continue;
            }
            
            // Определяем отступ текущей строки
            int currentIndent = line.Length - line.TrimStart().Length;
            
            // Устанавливаем базовый отступ блока по первой строке
            if (blockIndent == null)
            {
                blockIndent = currentIndent;
                if (currentIndent == 0)
                {
                    // Если отступа нет, читаем только до первой строки без отступа (кроме текущей)
                    blockLines.Enqueue(lines.Dequeue());
                    break;
                }
            }
            
            // Если отступ меньше базового - блок закончился
            if (currentIndent < blockIndent.Value)
            {
                break;
            }
            
            // Добавляем строку в блок
            blockLines.Enqueue(lines.Dequeue());
        }
        
        return ParseStatements(blockLines, isSilentContext: true, cancellationToken);
    }
    
    private List<Statement> ParseElifElse(Queue<string> lines, CancellationToken cancellationToken = default)
    {
        // Рекурсивная обработка elif/else цепочки
        if (lines.Count == 0) return null;
        
        var nextLine = lines.Peek().Trim();
        
        if (nextLine.StartsWith("elif"))
        {
            lines.Dequeue();
            var elifCondMatch = Regex.Match(nextLine, @"^elif\s+(.+):\s*$");
            if (elifCondMatch.Success)
            {
                var elifCondition = elifCondMatch.Groups[1].Value;
                var elifBody = ParseIndentedBlock(lines, cancellationToken);
                // elif это просто if с возможным продолжением elif/else
                var elifElse = ParseElifElse(lines, cancellationToken);
                var elifStatement = new IfStatement(elifCondition, elifBody, elifElse);
                return new List<Statement> { elifStatement };
            }
        }
        else if (nextLine.StartsWith("else:"))
        {
            lines.Dequeue();
            return ParseIndentedBlock(lines, cancellationToken);
        }
        
        return null;
    }
}
