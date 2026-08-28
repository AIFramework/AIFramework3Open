using System.Collections.Generic;
using System.Threading;

namespace AI.ClassicMath.Calculator.ProcessorLogic;

/// <summary>
/// Инструкция 'for x in список' - обход коллекции.
/// </summary>
/// <remarks>
/// Отдельно от <see cref="ForStatement"/>: тот считает счётчик и требует знать длину заранее,
/// а обход по списку — это то, чем написана всякая проверка «пройди по позициям».
/// </remarks>
internal class ForEachStatement : Statement
{
    public string Variable { get; }
    public string Collection { get; }
    public List<Statement> Body { get; }

    public ForEachStatement(string variable, string collection, List<Statement> body)
    {
        Variable = variable;
        Collection = collection;
        Body = body;
    }

    public override void Execute(Processor processor, ExecutionContext context, List<string> output, CancellationToken cancellationToken = default)
    {
        var collection = processor.AdvancedCalculator.Evaluate(Collection, context, cancellationToken);
        var items = ListOps.Items(collection, $"for {Variable} in");

        try
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.CountStep();
                context.Memory[Variable] = item;

                try
                {
                    foreach (var statement in Body)
                    {
                        statement.Execute(processor, context, output, cancellationToken);
                    }
                }
                catch (ContinueException)
                {
                    // Continue - переходим к следующему элементу
                }
            }
        }
        catch (BreakException)
        {
            // Break - выходим из цикла
        }
    }
}
