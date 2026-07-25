using System.Text;

namespace AI.LLM.Agents.ReAct.Rendering;

/// <summary>
/// Рендерер следа с двумя лимитами: на одно наблюдение и на весь текст.
/// <para>
/// Ключевое свойство — при нехватке места выбрасываются САМЫЕ СТАРЫЕ шаги, а свежие
/// сохраняются целиком. Обратный порядок (обрезка «первые N символов») означает, что модель
/// перестаёт видеть результаты собственных последних действий и начинает их повторять;
/// правило «не повторяй уже сделанное» при этом становится физически невыполнимым.
/// </para>
/// </summary>
public sealed class TailBudgetTraceRenderer : IReActTraceRenderer
{
    private const string Ellipsis = "…";

    private readonly int _maxObservationChars;
    private readonly int _maxTotalChars;

    /// <summary>Создаёт рендерер.</summary>
    /// <param name="maxObservationChars">Предел длины одного наблюдения.</param>
    /// <param name="maxTotalChars">Предел длины всего текста.</param>
    public TailBudgetTraceRenderer(int maxObservationChars = 4000, int maxTotalChars = 12000)
    {
        if (maxObservationChars < 1)
            throw new ArgumentOutOfRangeException(nameof(maxObservationChars), "Лимит наблюдения должен быть положительным.");
        if (maxTotalChars < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTotalChars), "Общий лимит должен быть положительным.");

        _maxObservationChars = maxObservationChars;
        _maxTotalChars = maxTotalChars;
    }

    /// <inheritdoc />
    public string Render(ReActTrace trace)
    {
        if (trace == null || trace.Count == 0)
            return string.Empty;

        // Рендерим шаги от последнего к первому и набираем, пока хватает бюджета:
        // так свежие шаги попадают в текст гарантированно, а урезается хвост истории.
        var chunks = new List<string>(trace.Count);
        int used = 0;
        int included = 0;

        for (int i = trace.Steps.Count - 1; i >= 0; i--)
        {
            string chunk = RenderStep(trace.Steps[i]);
            if (chunk.Length == 0)
                continue;

            // Самый свежий шаг включаем всегда, даже если он один съедает весь бюджет:
            // остаться совсем без последнего наблюдения хуже, чем превысить лимит.
            bool first = included == 0;
            if (!first && used + chunk.Length > _maxTotalChars)
                break;

            chunks.Add(chunk);
            used += chunk.Length;
            included++;
        }

        chunks.Reverse();

        var sb = new StringBuilder();
        int omitted = trace.Count - included;
        if (omitted > 0)
            sb.Append("[…ранние шаги опущены: ").Append(omitted).Append("…]\n\n");

        foreach (string chunk in chunks)
            sb.Append(chunk);

        return sb.ToString();
    }

    private string RenderStep(ReActStep step)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(step.Thought))
            sb.Append("Мысль: ").Append(step.Thought.Trim()).Append('\n');

        foreach (ReActObservation observation in step.Observations)
        {
            string tool = observation.Action?.ToolName ?? "инструмент";
            string arguments = Trim(observation.Action?.Arguments, 200);

            sb.Append("Действие: ").Append(tool);
            if (arguments.Length > 0)
                sb.Append(" («").Append(arguments).Append("»)");
            sb.Append('\n');

            sb.Append(observation.Ok ? "Наблюдение: " : "Наблюдение (ошибка): ");
            sb.Append(Trim(observation.Text, _maxObservationChars)).Append('\n');

            if (observation.Citations.Count > 0)
            {
                sb.Append("Источники: ");
                for (int i = 0; i < observation.Citations.Count; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    sb.Append(observation.Citations[i].Url);
                }

                sb.Append('\n');
            }
        }

        if (!string.IsNullOrWhiteSpace(step.Note))
            sb.Append("Замечание: ").Append(step.Note.Trim()).Append('\n');

        if (sb.Length > 0)
            sb.Append('\n');

        return sb.ToString();
    }

    private static string Trim(string text, int max)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Length <= max ? text : text[..max] + Ellipsis;
    }
}
