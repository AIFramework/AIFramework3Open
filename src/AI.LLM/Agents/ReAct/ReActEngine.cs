using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using AI.LLM.Agents.Guards;
using AI.LLM.Agents.Memory;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Agents.ReAct.Rendering;
using AI.LLM.Agents.ReAct.Synthesis;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Core.Models.Common.Messages;
using Serilog;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Цикл ReAct: рассуждение → действие → наблюдение, и так до ответа либо до исчерпания бюджета.
/// </summary>
/// <remarks>
/// <para>
/// Экземпляр не хранит состояния прогона, поэтому один движок обслуживает сколько угодно
/// одновременных запусков. По той же причине у него нет событий-членов: наблюдать за работой
/// следует через поток <see cref="ReActEvent"/>, который у каждого прогона свой.
/// </para>
/// <para>
/// Поток не бросает исключений, кроме отмены: любой сбой приходит терминальным событием
/// с <see cref="ReActStopReason.EngineFailure"/>. Так вызывающей стороне не приходится
/// оборачивать перечисление в try/catch — вокруг <c>yield return</c> это к тому же запрещено.
/// </para>
/// <para>
/// Создаётся через <see cref="ReActAgentBuilder"/>.
/// </para>
/// </remarks>
public sealed partial class ReActEngine
{
    /// <summary>Ключ фазы шага в событии <see cref="ReActEvent.Phase"/>.</summary>
    public const string PhaseStep = "react.step";

    /// <summary>Ключ фазы синтеза ответа в событии <see cref="ReActEvent.Phase"/>.</summary>
    public const string PhaseSynthesis = "react.synthesis";

    private readonly IReActPolicy _policy;
    private readonly IReadOnlyList<IReActToolSource> _toolSources;
    private readonly IReadOnlyList<IReActSkill> _skills;
    private readonly IReActTraceRenderer _renderer;
    private readonly IReActPromptTemplate _template;
    private readonly IReActSynthesizer _synthesizer;
    private readonly IAgentGuard _guard;
    private readonly IAgentMemory _memory;
    private readonly ReActConfig _config;
    private readonly string _basePrompt;

    internal ReActEngine(
        IReActPolicy policy,
        IReadOnlyList<IReActToolSource> toolSources,
        IReadOnlyList<IReActSkill> skills,
        IReActTraceRenderer renderer,
        IReActPromptTemplate template,
        IReActSynthesizer synthesizer,
        IAgentGuard guard,
        IAgentMemory memory,
        ReActConfig config,
        string basePrompt)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _toolSources = toolSources ?? [];
        _skills = skills ?? [];
        _renderer = renderer ?? new TailBudgetTraceRenderer();
        _template = template ?? new DefaultReActPromptTemplate();
        _synthesizer = synthesizer;
        _guard = guard;
        _memory = memory;
        _config = config ?? new ReActConfig();
        _basePrompt = basePrompt;
    }

    /// <summary>Запускает цикл, отдавая события по мере работы.</summary>
    /// <param name="query">Запрос.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>
    /// Холодная последовательность: работа начинается при первом обращении к перечислителю.
    /// Последнее событие — всегда <see cref="ReActEvent.Completed"/>, в том числе при сбое.
    /// Если потребитель прекращает перечисление досрочно, выполняющиеся инструменты отменяются.
    /// </returns>
    public async IAsyncEnumerable<ReActEvent> StreamAsync(
        ReActQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var stopwatch = Stopwatch.StartNew();
        var run = new RunState
        {
            Query = query,
            Usage = new AgentUsage(),
            Trace = new ReActTrace(),
        };

        IAsyncEnumerator<ReActEvent> enumerator = StreamCoreAsync(run, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                ReActEvent current;
                Exception failure = null;

                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        break;

                    current = enumerator.Current;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Отмена пользователем — не сбой цикла, пробрасываем как есть.
                    throw;
                }
                catch (Exception ex)
                {
                    current = null;
                    failure = ex;
                }

                if (failure != null)
                {
                    Log.Error(failure, "ReAct: прогон прерван необработанной ошибкой");
                    stopwatch.Stop();
                    yield return new ReActEvent.Completed(
                        BuildResult(run, stopwatch.Elapsed, ReActStopReason.EngineFailure, failure.Message));
                    yield break;
                }

                yield return current;
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        stopwatch.Stop();
        yield return new ReActEvent.Completed(BuildResult(run, stopwatch.Elapsed));
    }

    /// <summary>Запускает цикл и дожидается результата.</summary>
    /// <param name="query">Запрос.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async Task<ReActResult> RunAsync(ReActQuery query, CancellationToken cancellationToken = default)
    {
        ReActResult result = null;
        await foreach (ReActEvent evt in StreamAsync(query, cancellationToken).ConfigureAwait(false))
        {
            if (evt is ReActEvent.Completed completed)
                result = completed.Result;
        }

        return result;
    }

    /// <summary>Работа прогона без обработки сбоев — ею занимается <see cref="StreamAsync"/>.</summary>
    private async IAsyncEnumerable<ReActEvent> StreamCoreAsync(
        RunState run, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_memory != null)
        {
            IReadOnlyList<LLMMessage> history = await ReActMemoryBridge
                .BuildHistoryAsync(_memory, _basePrompt, run.Query.Text)
                .ConfigureAwait(false);

            run.Query = new ReActQuery(
                run.Query.Text, history, run.Query.Images, run.Query.ForcedFirstAction, run.Query.Tag);
        }

        ReActRunContext toolContext = run.Query.ToRunContext();
        run.Tools = CollectTools(toolContext);
        run.Skills = SelectSkills(toolContext);
        run.SystemPrompt = _template.BuildSystemPrompt(_basePrompt, run.Tools, run.Skills, toolContext);

        if (run.Tools.Count == 0 && run.Query.ForcedFirstAction == null)
        {
            // Без инструментов цикл вырождается: сразу к ответу по тому, что модель знает сама.
            run.StopReason = ReActStopReason.NoTools;
        }
        else
        {
            await foreach (ReActEvent evt in RunLoopAsync(run, cancellationToken).ConfigureAwait(false))
                yield return evt;
        }

        if (ShouldSynthesize(run))
        {
            yield return new ReActEvent.Phase(PhaseSynthesis);

            var answer = new StringBuilder();
            ReActSynthesisContext synthesis = BuildSynthesisContext(run);

            await foreach (ReActTextChunk chunk in _synthesizer
                .SynthesizeAsync(synthesis, cancellationToken)
                .ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.Reasoning))
                    yield return new ReActEvent.ReasoningChunk(chunk.Reasoning);

                if (string.IsNullOrEmpty(chunk.Content))
                    continue;

                answer.Append(chunk.Content);
                yield return new ReActEvent.AnswerChunk(chunk.Content);
            }

            string text = answer.ToString().Trim();
            if (text.Length > 0)
                run.Answer = text;
        }

        await FinishAsync(run, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Проверка ответа guard'ами и сохранение в память — после того, как ответ известен.</summary>
    private async Task FinishAsync(RunState run, CancellationToken cancellationToken)
    {
        string answer = run.Answer ?? run.Draft ?? string.Empty;

        if (_guard != null)
        {
            try
            {
                GuardResult verdict = await _guard
                    .CheckAsync(run.Query.Text, answer, cancellationToken)
                    .ConfigureAwait(false);

                if (verdict is { Passed: false })
                    Log.Warning("ReAct: guard отклонил ответ: {Reason} (score {Score:F2})", verdict.Reason, verdict.Score);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ReAct: guard не отработал");
            }
        }

        if (_memory == null)
            return;

        try
        {
            await _memory.SaveInteractionAsync(run.Query.Text, answer, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Не сохранили в память — обидно, но ответ пользователю от этого не пропадает.
            Log.Warning(ex, "ReAct: не удалось сохранить взаимодействие в память");
        }
    }

    /// <summary>Состояние одного прогона. Живёт на стеке вызова, экземпляру движка не принадлежит.</summary>
    private sealed class RunState
    {
        /// <summary>
        /// Замок на запись общего состояния прогона. Нужен только потому, что инструменты
        /// одного шага могут исполняться параллельно: <see cref="AgentUsage"/> и словарь
        /// повторов внутри <see cref="ReActTrace"/> рассчитаны на одного писателя — до
        /// появления этого цикла других и не было.
        /// </summary>
        public object Sync { get; } = new();

        public ReActQuery Query { get; set; }

        public AgentUsage Usage { get; init; }

        public ReActTrace Trace { get; init; }

        public IReadOnlyList<IReActTool> Tools { get; set; } = [];

        public IReadOnlyList<IReActSkill> Skills { get; set; } = [];

        public string SystemPrompt { get; set; } = string.Empty;

        public ReActStopReason StopReason { get; set; } = ReActStopReason.IterationLimit;

        /// <summary>Текст, присланный моделью при завершении; при включённом синтезе — черновик.</summary>
        public string Draft { get; set; }

        /// <summary>Готовый ответ: от терминального инструмента либо от синтеза.</summary>
        public string Answer { get; set; }

        public object Payload { get; set; }
    }

    private bool ShouldSynthesize(RunState run)
    {
        if (_synthesizer == null || _config.SynthesisMode == ReActSynthesisMode.Never)
            return false;

        // Терминальный инструмент сам является ответом хода: пересказывать его синтезом
        // означает потерять то, ради чего он и вызывался.
        if (run.StopReason == ReActStopReason.TerminalTool)
            return false;

        if (_config.SynthesisMode == ReActSynthesisMode.Always)
            return true;

        return string.IsNullOrWhiteSpace(run.Answer) && string.IsNullOrWhiteSpace(run.Draft);
    }

    private ReActSynthesisContext BuildSynthesisContext(RunState run) =>
        new()
        {
            Query = run.Query,
            Trace = run.Trace,
            RenderedTrace = _renderer.Render(run.Trace),
            Draft = run.Draft,
            Citations = run.Trace.Citations,
            StopReason = run.StopReason,
        };

    private static ReActResult BuildResult(
        RunState run, TimeSpan elapsed, ReActStopReason? stopReason = null, string error = null)
    {
        string answer = run.Answer;
        if (string.IsNullOrWhiteSpace(answer))
            answer = run.Draft;

        return new ReActResult
        {
            Answer = answer ?? string.Empty,
            StopReason = stopReason ?? run.StopReason,
            Steps = run.Trace.Steps,
            Citations = run.Trace.Citations,
            Payload = run.Payload,
            Usage = run.Usage,
            Elapsed = elapsed,
            Error = error,
        };
    }
}
