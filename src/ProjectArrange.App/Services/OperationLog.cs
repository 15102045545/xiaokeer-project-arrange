namespace ProjectArrange.App.Services;

public sealed record LogEntry(DateTimeOffset Utc, string Level, string Message);

public sealed class OperationLog
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = new();

    public event Action? Changed;

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate) return _entries.ToList();
    }

    public void Info(string message) => Add("INFO", message);
    public void Error(string message) => Add("ERROR", message);

    private void Add(string level, string message)
    {
        lock (_gate)
        {
            _entries.Add(new LogEntry(DateTimeOffset.UtcNow, level, message));
            if (_entries.Count > 2000) _entries.RemoveRange(0, _entries.Count - 2000);
        }
        Changed?.Invoke();
    }
}

