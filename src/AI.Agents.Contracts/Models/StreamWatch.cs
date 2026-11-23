namespace AI.Agents.Contracts.Models;

/// <summary>
/// Сторож потока модели: сколько ждать первого фрагмента и сколько терпеть простой посреди ответа. Срабатывание —
/// итог <see cref="LlmStatus.Failed"/> с кодом <see cref="LlmFailure.Stalled"/>, а не бесконечное ожидание.
/// </summary>
/// <remarks>
/// Общий срок вызова сюда не входит: длинный ответ идет минутами, и срок запуска живет только в
/// <c>IRunBudget.Deadline</c>. Сторож ловит другое — молчание: маршрут принял запрос и не начал отвечать, или поток
/// встал посреди ответа. Фрагмент рассуждения считается признаком жизни так же, как фрагмент текста.
/// </remarks>
public sealed record StreamWatch
{
    private readonly TimeSpan _firstChunk;
    private readonly TimeSpan? _idle;

    /// <summary>Сколько ждать первого фрагмента с начала вызова.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение не больше нуля.</exception>
    public required TimeSpan FirstChunk
    {
        get => _firstChunk;
        init => _firstChunk = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(FirstChunk), value, "Ожидание первого фрагмента больше нуля.");
    }

    /// <summary>
    /// Сколько терпеть тишину между фрагментами после первого; <see langword="null"/>: простой посреди ответа не
    /// отслеживается.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Значение не больше нуля.</exception>
    public TimeSpan? Idle
    {
        get => _idle;
        init => _idle = value is null || value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Idle), value, "Допустимый простой больше нуля.");
    }
}
