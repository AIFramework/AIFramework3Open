namespace AI.LLM.Infrastructure.Http;

/// <summary>
/// Статистика по прокси для внешнего использования
/// </summary>
public class ProxyStatistics
{
    public string ProxyAddress { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastFailure { get; set; }
    public bool IsBlacklisted { get; set; }
    public string LastException { get; set; }
}

/// <summary>
/// Данные отладочного лога для события
/// </summary>
public class DebugLogEventArgs : EventArgs
{
    public string Message { get; }
    public DateTime Timestamp { get; }

    public DebugLogEventArgs(string message)
    {
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}
