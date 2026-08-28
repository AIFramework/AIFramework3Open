using System;
using System.Collections.Generic;
using System.Threading;

namespace AI.ClassicMath.Calculator.ProcessorLogic;

/// <summary>
/// Инструкция 'def имя(аргументы):' - объявление функции скрипта.
/// </summary>
/// <remarks>
/// Объявление кладётся в контекст прогона, а не в набор калькулятора: набор — это то, что дал
/// хост, и он переживает прогон, а объявленное скриптом обязано умереть вместе с ним.
/// <para>
/// Ради этого и затевался этап: проверка документа — это функция, которую пишут один раз и
/// зовут для каждой позиции. Пока функций не было, проверку приходилось разворачивать в
/// линейный скрипт, и она переставала быть переиспользуемой.
/// </para>
/// </remarks>
internal class FunctionStatement : Statement
{
    public string Name { get; }
    public List<string> Parameters { get; }
    public List<Statement> Body { get; }

    public FunctionStatement(string name, List<string> parameters, List<Statement> body)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
    }

    public override void Execute(Processor processor, ExecutionContext context, List<string> output, CancellationToken cancellationToken = default)
    {
        // Имя из набора не перекрываем: скрипт, объявивший свой sum, молча получал бы чужой —
        // разбираться в таком отказе дороже, чем переименовать функцию.
        if (processor.AdvancedCalculator.Functions.ContainsKey(Name))
            throw new InvalidOperationException(
                $"Функция '{Name}' уже есть в наборе вычислителя — назовите свою иначе.");

        context.UserFunctions[Name] = new FunctionDefinition
        {
            Name = Name,
            ArgumentCount = Parameters.Count,
            ContextDelegate = (args, caller) => Invoke(processor, caller, args),
        };
    }

    /// <summary>
    /// Вызов функции: свой контекст, общий с прогоном счётчик шагов.
    /// </summary>
    /// <remarks>
    /// Результат — значение из <c>return</c>, а если его не было, последнее вычисленное
    /// выражение тела. Требовать <c>return</c> нельзя: функция, которая только пишет в
    /// <c>emit</c>, тоже осмысленна.
    /// </remarks>
    private object Invoke(Processor processor, ExecutionContext caller, object[] args)
    {
        var local = new ExecutionContext(caller);

        if (local.Depth > ExecutionContext.MaxCallDepth)
            throw new InvalidOperationException(
                $"Функция '{Name}': вложенность вызовов больше {ExecutionContext.MaxCallDepth} — похоже на бесконечную рекурсию.");

        for (int i = 0; i < Parameters.Count && i < args.Length; i++)
            local.Memory[Parameters[i]] = args[i];

        try
        {
            foreach (var statement in Body)
            {
                statement.Execute(processor, local, new List<string>(), local.Cancellation);
            }
        }
        catch (ReturnException returned)
        {
            return returned.Value;
        }

        return local.LastValue;
    }
}
