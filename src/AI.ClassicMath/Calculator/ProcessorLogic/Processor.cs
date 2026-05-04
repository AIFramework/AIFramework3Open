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



[Serializable]
public partial class Processor
{
    public readonly AdvancedCalculator AdvancedCalculator = new AdvancedCalculator();

    public List<string> Run(string script, CancellationToken cancellationToken = default)
    {
        var context = new ExecutionContext();
        var output = new List<string>();
        try
        {
            // УЛУЧШЕНИЕ 1: Поддержка точки с запятой (;) как разделителя выражений
            // Сначала разбиваем по точке с запятой, сохраняя переносы строк
            script = PreprocessScript(script);
            
            var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var lineQueue = new Queue<string>(lines);
            var statements = ParseStatements(lineQueue, isSilentContext: false, cancellationToken);
            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                statement.Execute(this, context, output, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            output.Add("ВЫПОЛНЕНИЕ ПРЕРВАНО: Операция была отменена");
        }
        catch (Exception ex)
        {
            output.Add($"!!! КРИТИЧЕСКАЯ ОШИБКА: {ex.GetType().Name} -> {ex.Message}");
        }
        return output;
    }
}
