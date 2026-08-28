using System.Collections.Generic;
using System.Threading;

namespace AI.ClassicMath.Calculator.ProcessorLogic;

/// <summary>
/// Инструкция 'return' - выход из функции скрипта с результатом.
/// </summary>
internal class ReturnStatement : Statement
{
    public string Expression { get; }

    public ReturnStatement(string expression) => Expression = (expression ?? "").Trim();

    public override void Execute(Processor processor, ExecutionContext context, List<string> output, CancellationToken cancellationToken = default)
    {
        var value = Expression.Length == 0
            ? null
            : processor.AdvancedCalculator.Evaluate(Expression, context, cancellationToken);

        throw new ReturnException(value);
    }
}
