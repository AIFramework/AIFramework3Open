using AI.ClassicMath.Calculator.Libs;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AI.ClassicMath.Calculator.ProcessorLogic;

[Serializable]
public partial class Processor
{
    /// <summary>Причина отказа при отмене — исторический текст, его читают прежние вызывающие.</summary>
    private const string CancelMessage = "ВЫПОЛНЕНИЕ ПРЕРВАНО: Операция была отменена";

    /// <summary>Префикс причины отказа при сорвавшемся скрипте.</summary>
    private const string CriticalPrefix = "!!! КРИТИЧЕСКАЯ ОШИБКА: ";

    public readonly AdvancedCalculator AdvancedCalculator = new AdvancedCalculator();

    /// <summary>
    /// Потолок шагов интерпретатора на прогон; ноль и меньше — без потолка.
    /// </summary>
    public int StepLimit { get; set; } = ExecutionContext.DefaultStepLimit;

    /// <summary>
    /// Вычислитель с дополнительными библиотеками функций поверх базовых.
    /// </summary>
    /// <remarks>
    /// Библиотеки принимаются здесь, а не собираются внутри: см. <see cref="AdvancedCalculator.Use"/>.
    /// Без аргументов — прежний базовый состав.
    /// </remarks>
    public Processor(params IMathLib[] libraries)
    {
        foreach (var library in libraries)
            AdvancedCalculator.Use(library);
    }

    /// <summary>
    /// Выполняет скрипт и возвращает исход: напечатанное, признак успеха и причину отказа.
    /// </summary>
    /// <remarks>
    /// Отказ — отдельное поле, а не строка в выводе. Пока причина лежала В выводе, вызывающий
    /// отличал сорвавшийся скрипт от удачного только разбором текста: инструмент чата докладывал
    /// об успехе, положив внутрь сообщение об ошибке.
    /// <para>
    /// Напечатанное до срыва остаётся в выводе: по нему видно, на каком шаге скрипт сломался.
    /// </para>
    /// </remarks>
    public ScriptResult Execute(string script, CancellationToken cancellationToken = default)
        => Execute(script, null, cancellationToken);

    /// <summary>
    /// Выполняет скрипт над данными, подготовленными вызывающим.
    /// </summary>
    /// <remarks>
    /// Данные приходят ПЕРЕМЕННЫМИ, а не текстом внутри скрипта: см.
    /// <see cref="ExecutionContext.Seed"/>. Имя, непригодное в переменные, пропускается.
    /// </remarks>
    public ScriptResult Execute(
        string script,
        IReadOnlyDictionary<string, object> seed,
        CancellationToken cancellationToken = default)
    {
        var context = new ExecutionContext { StepLimit = StepLimit, Cancellation = cancellationToken };
        context.Seed(seed);
        var output = new List<string>();

        try
        {
            // Поддержка точки с запятой (;) как разделителя выражений: разбиваем по ней,
            // сохраняя переносы строк.
            script = PreprocessScript(script);

            var lines = script.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var lineQueue = new Queue<string>(lines);
            var statements = ParseStatements(lineQueue, isSilentContext: false, cancellationToken);

            foreach (var statement in statements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                statement.Execute(this, context, output, cancellationToken);
            }

            return new ScriptResult(true, output, null, context.Emitted);
        }
        catch (OperationCanceledException)
        {
            return new ScriptResult(false, output, CancelMessage, context.Emitted);
        }
        catch (ReturnException)
        {
            return new ScriptResult(false, output,
                $"{CriticalPrefix}return вне функции: возвращать результат наружу можно только из def.",
                context.Emitted);
        }
        catch (Exception ex)
        {
            return new ScriptResult(false, output, $"{CriticalPrefix}{ex.GetType().Name} -> {ex.Message}", context.Emitted);
        }
    }

    /// <summary>
    /// Выполняет скрипт и отдаёт вывод строками; причина отказа — последней строкой.
    /// </summary>
    /// <remarks>
    /// Прежний контракт для тех, кому нужен транскрипт целиком. Новым вызывающим нужен
    /// <see cref="Execute"/>: по списку строк успех от отказа не отличается.
    /// </remarks>
    public List<string> Run(string script, CancellationToken cancellationToken = default)
    {
        var result = Execute(script, cancellationToken);
        var lines = new List<string>(result.Output);

        if (result.Error != null) lines.Add(result.Error);

        return lines;
    }
}
