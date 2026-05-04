namespace AI.LLM.Agents.Guards;

/// <summary>
/// Результат проверки ответа защитным механизмом.
/// </summary>
public sealed class GuardResult
{
    /// <summary>
    /// Проверка пройдена.
    /// </summary>
    public bool Passed { get; }

    /// <summary>
    /// Причина отклонения (если не пройдена).
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Числовой показатель (0..1), специфичный для конкретного guard.
    /// </summary>
    public double Score { get; }

    private GuardResult(bool passed, string reason, double score)
    {
        Passed = passed;
        Reason = reason;
        Score = score;
    }

    /// <summary>
    /// Создаёт успешный результат.
    /// </summary>
    public static GuardResult Pass(double score = 1.0) => new(true, null, score);

    /// <summary>
    /// Создаёт неуспешный результат.
    /// </summary>
    public static GuardResult Fail(string reason, double score = 0.0) => new(false, reason, score);
}
