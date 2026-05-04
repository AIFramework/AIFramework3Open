using AI.Solvers.Math;
using System.Text;

namespace AiFrameworkDemo.Modules.SolversMath;

/// <summary>
/// Одна запись REPL-истории.
/// </summary>
public record HistoryEntry(string Input, string Output, bool IsError, DateTime Timestamp);

/// <summary>
/// Сервис интерактивного REPL-вычислителя на базе AI.Solvers.Math.
/// Инжектируется как Scoped (один экземпляр на Blazor-соединение).
/// </summary>
public sealed class SolversMathRunner
{
    private readonly MainFractalMathProcessor _processor = new();
    private readonly List<HistoryEntry> _history = [];

    public IReadOnlyList<HistoryEntry> History => _history;

    public HistoryEntry Evaluate(string input)
    {
        input = input.Trim();
        if (string.IsNullOrEmpty(input))
        {
            var empty = new HistoryEntry(input, "", false, DateTime.Now);
            return empty;
        }

        string output;
        bool isError = false;

        try
        {
            if (input.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("?",    StringComparison.Ordinal))
            {
                output = MainFractalMathProcessor.GetHelpText();
            }
            else if (input.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
                     input.Equals("cls",   StringComparison.OrdinalIgnoreCase))
            {
                _history.Clear();
                return new HistoryEntry(input, "__CLEARED__", false, DateTime.Now);
            }
            else
            {
                output = _processor.ProcessFractalMathCommand(input);
            }
        }
        catch (Exception ex)
        {
            output = $"Внутренняя ошибка: {ex.Message}";
            isError = true;
        }

        var entry = new HistoryEntry(input, output, isError, DateTime.Now);
        _history.Add(entry);
        return entry;
    }

    public void Clear() => _history.Clear();

    /// <summary>Подсказки для интерфейса — примеры команд по категориям.</summary>
    public static readonly IReadOnlyList<(string Category, string[] Examples)> QuickExamples =
    [
        ("Интегралы",
        [
            "integrate x^2",
            "integrate sin(x)*cos(x)",
            "integrate x^2 from 0 to 5",
            "integrate exp(-x^2) dx",
            "integrate integrate x*y dx dy",
        ]),
        ("Производные",
        [
            "derivative of x^3 + 2*x^2",
            "second derivative of sin(x)",
            "3rd derivative of x^5",
            "derivative of ln(sin(x^2))",
            "partial derivative of x^2*y^3 with respect to x",
        ]),
        ("ОДУ",
        [
            "solve y' + 2y = 0",
            "solve y'' + 4y = 0",
            "solve y' = 2x, y(0) = 1",
            "solve x' = y, y' = -x",
        ]),
        ("ЧУ (PDE)",
        [
            "solve u_t = u_xx",
            "solve u_tt = 4*u_xx",
            "solve u_xx + u_yy = 0",
            "solve u_t + 2*u_x = 0",
            "solve u_t + u*u_x = 0.01*u_xx",
        ]),
        ("Пределы и ряды",
        [
            "limit (sin(x)/x) as x->0",
            "limit (1 + 1/x)^x as x->inf",
            "Taylor series of sin(x) at x=0",
            "Taylor series of exp(x) at x=0",
        ]),
        ("Преобразования",
        [
            "Laplace transform of sin(t)",
            "Laplace transform of exp(-2t)",
            "Laplace table",
            "Fourier transform of exp(-x^2)",
        ]),
        ("Уравнения",
        [
            "solve x^2 + 5*x + 6 = 0",
            "solve x^3 - x = 0",
        ]),
    ];
}
