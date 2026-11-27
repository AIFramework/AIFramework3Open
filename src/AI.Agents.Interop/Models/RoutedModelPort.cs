using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Models;
using Serilog;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Порт модели по ролям: роль знает основную модель и запасные, переход по цепочке — правило
/// <see cref="ModelFallback"/>, итог говорит, какая модель ответила (<see cref="LlmAnswer.Model"/>).
/// </summary>
/// <remarks>
/// <para>
/// Это единственное место, где роль сопоставляется с именем модели: движки спрашивают порт по роли и имени модели
/// не знают. Расход итога — по всем попыткам цепочки.
/// </para>
/// <para>
/// Поток: следующая модель берется, только если прежняя не успела отдать ни одного кадра. Начатый показ не
/// переписывается чужим ответом: после первого кадра итог прежней модели — окончательный.
/// </para>
/// </remarks>
internal sealed class RoutedModelPort : IModelPort
{
    private readonly Dictionary<ModelRole, ModelSlot[]> _chains;
    private readonly ILogger? _log;

    public RoutedModelPort(IReadOnlyDictionary<ModelRole, IReadOnlyList<ModelSlot>> chains, ILogger? log)
    {
        ArgumentNullException.ThrowIfNull(chains);
        _chains = new Dictionary<ModelRole, ModelSlot[]>();
        foreach ((ModelRole role, IReadOnlyList<ModelSlot> slots) in chains)
        {
            if (role.Name == null)
                throw new ArgumentException("Роль модели не задана (default(ModelRole)).", nameof(chains));
            ModelSlot[] chain = [.. slots ?? throw new ArgumentException($"У роли {role} нет цепочки моделей.", nameof(chains))];
            if (chain.Length == 0 || chain.Any(s => s == null))
                throw new ArgumentException($"У роли {role} пустая цепочка или пустая модель в ней.", nameof(chains));
            _chains[role] = chain;
        }

        if (_chains.Count == 0)
            throw new ArgumentException("Не задано ни одной роли.", nameof(chains));
        _log = log;
    }

    private ILogger Logger => _log ?? Log.Logger;

    public async Task<LlmAnswer> AskAsync(ModelAsk ask, CancellationToken cancellationToken = default)
    {
        ModelSlot[] chain = ChainOf(ask);
        ModelUsage? spent = null;

        for (int i = 0; ; i++)
        {
            LlmAnswer answer = await chain[i].Port.AskAsync(ask, cancellationToken).ConfigureAwait(false);
            spent = Sum(spent, answer.Usage);
            if (i == chain.Length - 1 || !ModelFallback.TryNext(answer))
                return Final(answer, chain[i], spent);

            LogFallback(ask.Role, chain[i], chain[i + 1], answer);
        }
    }

    public async IAsyncEnumerable<LlmChunk> StreamAsync(ModelAsk ask, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ModelSlot[] chain = ChainOf(ask);
        ModelUsage? spent = null;

        for (int i = 0; i < chain.Length; i++)
        {
            bool shown = false;
            await foreach (LlmChunk frame in chain[i].Port.StreamAsync(ask, cancellationToken).ConfigureAwait(false))
            {
                if (frame is not LlmChunk.Final final)
                {
                    shown = true;
                    yield return frame;
                    continue;
                }

                spent = Sum(spent, final.Answer.Usage);
                if (shown || i == chain.Length - 1 || !ModelFallback.TryNext(final.Answer))
                {
                    yield return new LlmChunk.Final { Answer = Final(final.Answer, chain[i], spent) };
                    yield break;
                }

                LogFallback(ask.Role, chain[i], chain[i + 1], final.Answer);
                break;
            }
        }
    }

    public ModelCapabilities Describe(ModelRole role) =>
        _chains.TryGetValue(role, out ModelSlot[]? chain)
            ? chain[0].Port.Describe(role)
            : throw new ArgumentException($"Роль {role} не настроена в порте моделей.", nameof(role));

    private ModelSlot[] ChainOf(ModelAsk ask)
    {
        ArgumentNullException.ThrowIfNull(ask);
        return _chains.TryGetValue(ask.Role, out ModelSlot[]? chain)
            ? chain
            : throw new ArgumentException($"Роль {ask.Role} не настроена в порте моделей.", nameof(ask));
    }

    /// <summary>Итог цепочки: модель — что сообщил порт, иначе имя из цепочки; расход — по всем попыткам.</summary>
    private static LlmAnswer Final(LlmAnswer answer, ModelSlot slot, ModelUsage? spent) =>
        answer with { Model = answer.Model ?? slot.Model, Usage = spent };

    private void LogFallback(ModelRole role, ModelSlot failed, ModelSlot next, LlmAnswer answer) =>
        Logger.Warning("Порт моделей: роль {Role}, модель {Model} — сбой {Failure}, переход на {Next}: {Text}",
            role.Name, failed.Model, answer.Failure, next.Model, answer.FailureText);

    private static ModelUsage? Sum(ModelUsage? total, ModelUsage? add) =>
        add == null ? total
        : total == null ? add
        : new ModelUsage
        {
            InputTokens = total.InputTokens + add.InputTokens,
            OutputTokens = total.OutputTokens + add.OutputTokens,
            Cost = total.Cost == null && add.Cost == null ? null : (total.Cost ?? 0) + (add.Cost ?? 0),
        };
}
