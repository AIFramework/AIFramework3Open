using System.Net;

namespace AI.LLM.Infrastructure.Http;

/// <summary>
/// Внутренний класс для отслеживания состояния прокси
/// </summary>
internal class ProxyStatus
{
    private readonly object _lock = new object();
    
    public WebProxy Proxy { get; set; }
    
    private int _failureCount;
    public int FailureCount 
    { 
        get { lock (_lock) return _failureCount; }
        set { lock (_lock) _failureCount = value; }
    }
    
    private DateTime? _lastFailure;
    public DateTime? LastFailure 
    { 
        get { lock (_lock) return _lastFailure; }
        set { lock (_lock) _lastFailure = value; }
    }
    
    private DateTime? _lastSuccess;
    public DateTime? LastSuccess 
    { 
        get { lock (_lock) return _lastSuccess; }
        set { lock (_lock) _lastSuccess = value; }
    }
    
    private Exception _lastException;
    public Exception LastException 
    { 
        get { lock (_lock) return _lastException; }
        set { lock (_lock) _lastException = value; }
    }
}

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
