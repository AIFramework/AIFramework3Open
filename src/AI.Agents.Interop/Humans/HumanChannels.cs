using AI.Agents.Contracts.Runs;
using AI.Agents.Contracts.Tools;
using Serilog;

namespace AI.Agents.Interop.Humans;

/// <summary>
/// Точка входа синхронного канала к человеку: <c>set.WithHumanChannel(channel)</c>.
/// </summary>
public static class HumanChannels
{
    /// <summary>
    /// Набор, который на вопрос инструмента (<see cref="ToolStatus.AwaitsUser"/>, <see cref="ToolStatus.NeedsInput"/>)
    /// спрашивает человека через <paramref name="channel"/> и один раз повторяет вызов с ответом в
    /// <see cref="AnsweredToolContext"/>.
    /// </summary>
    /// <param name="set">Набор инструментов; обычно уже обернутый гейтом эффектов, чтобы повтор с ответом шел через гейт.</param>
    /// <param name="channel">Канал к человеку.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Набор с каналом.</returns>
    public static IToolSet WithHumanChannel(this IToolSet set, IHumanChannel channel, ILogger? log = null) =>
        new HumanChannelToolSet(set, channel, log);
}
