using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;

namespace AI.Agents.Interop.Engines;

/// <summary>
/// Запоминает вызов (с ключом эффекта) и итог по идентификатору вызова, чтобы событие
/// <c>EngineEvent.ToolFinished</c> несло итог контракта, а не перевод наблюдения старого движка.
/// </summary>
internal sealed class CallRecorder(IToolSet inner) : IToolSet
{
    private readonly ConcurrentDictionary<string, (ToolCall Call, ToolResult Result)> _finished = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Available => inner.Available;

    /// <summary>Вызов и итог по идентификатору; забирается один раз.</summary>
    public bool TryTake(string callId, out ToolCall call, out ToolResult result)
    {
        if (_finished.TryRemove(callId, out (ToolCall Call, ToolResult Result) entry))
        {
            (call, result) = entry;
            return true;
        }

        call = null!;
        result = null!;
        return false;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolUpdate> CallAsync(
        ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Тот же ключ, что назначит гейт ниже: одно место создания, EffectKeys.
        ToolCall keyed = call with { EffectKey = EffectKeys.Create(context.RunId, call.Name, call.Arguments) };

        await foreach (ToolUpdate update in inner.CallAsync(call, context, cancellationToken).ConfigureAwait(false))
        {
            if (update is ToolUpdate.Final final)
                _finished[call.Id] = (keyed, final.Result);

            yield return update;
        }
    }
}
