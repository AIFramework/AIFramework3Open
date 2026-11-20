namespace AI.Agents.Interop.Humans;

/// <summary>
/// Машинные коды набора с каналом к человеку (<see cref="HumanChannels.WithHumanChannel"/>).
/// </summary>
public static class HumanChannelCodes
{
    /// <summary>Ответ пришел не на тот вопрос, который задал инструмент.</summary>
    public const string AnswerMismatch = "answer_mismatch";

    /// <summary>Человек не дал согласия на действие.</summary>
    public const string Declined = "declined";
}
