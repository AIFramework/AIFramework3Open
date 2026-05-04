using AI.ExplainitALL.Metrics;
using Serilog;

namespace AI.LLM.Agents.Guards;

/// <summary>
/// Обнаружение галлюцинаций через <see cref="CheckingForHallucinations"/>.
/// Сравнивает ответ агента с исходным запросом/контекстом.
/// </summary>
public sealed class HallucinationGuard : IAgentGuard
{
    private readonly CheckingForHallucinations _checker;
    private readonly double _threshold;

    /// <param name="checker">Настроенный экземпляр с <see cref="ISimMatrix"/>.</param>
    /// <param name="threshold">Максимально допустимая вероятность галлюцинации (0..1).</param>
    public HallucinationGuard(CheckingForHallucinations checker, double threshold = 0.5)
    {
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        _threshold = Math.Clamp(threshold, 0, 1);
    }

    /// <inheritdoc />
    public Task<GuardResult> CheckAsync(string query, string answer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(answer))
            return Task.FromResult(GuardResult.Pass());

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            double hallProb = _checker.GetHallucinationsProb(query, answer);
            double confidence = 1 - hallProb;

            var result = hallProb > _threshold
                ? GuardResult.Fail(
                    $"Высокая вероятность галлюцинации: {hallProb:P0} (порог: {_threshold:P0})",
                    confidence)
                : GuardResult.Pass(confidence);

            return Task.FromResult(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log.Error(ex, "HallucinationGuard: ошибка при проверке ответа");
            return Task.FromResult(GuardResult.Pass(0.5));
        }
    }
}
