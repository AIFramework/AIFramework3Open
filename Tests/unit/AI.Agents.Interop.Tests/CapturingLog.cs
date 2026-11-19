using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Журнал Serilog, который складывает записи в список: тесты считают предупреждения.
/// </summary>
internal sealed class CapturingLog : ILogEventSink
{
    private readonly List<LogEvent> _events = [];

    public CapturingLog()
    {
        Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(this).CreateLogger();
    }

    /// <summary>Журнал для адаптера.</summary>
    public ILogger Logger { get; }

    /// <summary>Тексты предупреждений.</summary>
    public IReadOnlyList<string> Warnings
    {
        get
        {
            lock (_events)
                return [.. _events.Where(e => e.Level == LogEventLevel.Warning).Select(e => e.RenderMessage())];
        }
    }

    /// <summary>Тексты всех записей.</summary>
    public IReadOnlyList<string> All
    {
        get
        {
            lock (_events)
                return [.. _events.Select(e => e.RenderMessage())];
        }
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        lock (_events)
            _events.Add(logEvent);
    }
}
