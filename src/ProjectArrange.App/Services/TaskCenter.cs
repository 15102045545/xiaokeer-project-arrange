using System.Collections.Concurrent;
using ProjectArrange.Core;

namespace ProjectArrange.App.Services;

public enum TaskKind
{
    GitStatus,
    SecretScan,
    GitHubUpdate,
    OpenSourceifyDryRun,
    P2pRegistrySyncSend,
    P2pRegistrySyncImport
}

public sealed record TaskLogLine(DateTimeOffset Utc, string Message);

public sealed class TaskRecord
{
    public required string Id { get; init; }
    public required TaskKind Kind { get; init; }
    public required string Name { get; init; }
    public required string TargetKey { get; init; }
    public required string GroupKey { get; init; }
    public required string? DedupeKey { get; init; }

    public TaskRunState State { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; init; }
    public string? LastError { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? EndedUtc { get; set; }

    public List<TaskLogLine> Logs { get; } = new();
}

public sealed class TaskCenter
{
    private readonly TaskQueue _queue;
    private readonly ConcurrentDictionary<string, TaskRecord> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _activeDedupe = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _groupLocks = new(StringComparer.OrdinalIgnoreCase);

    public TaskCenter(TaskQueue queue)
    {
        _queue = queue;
        _queue.Changed += SyncFromQueue;
    }

    public event Action? Changed;

    public int Concurrency
    {
        get => _queue.Concurrency;
        set => _queue.Concurrency = value;
    }

    public IReadOnlyList<TaskRecord> Snapshot()
    {
        SyncFromQueue();
        return _records.Values
            .OrderByDescending(r => r.StartedUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(r => r.EndedUtc ?? DateTimeOffset.MinValue)
            .ToArray();
    }

    public TaskRecord? GetById(string id)
    {
        SyncFromQueue();
        return _records.TryGetValue(id, out var rec) ? rec : null;
    }

    public string Enqueue(
        TaskKind kind,
        string name,
        string targetKey,
        string groupKey,
        Func<TaskContext, CancellationToken, Task<Result>> work,
        int maxAttempts = 3,
        string? dedupeKey = null)
    {
        var dk = string.IsNullOrWhiteSpace(dedupeKey) ? null : dedupeKey.Trim();
        if (dk is not null && _activeDedupe.TryGetValue(dk, out var existingId))
        {
            if (_records.TryGetValue(existingId, out var existing) &&
                (existing.State == TaskRunState.Queued || existing.State == TaskRunState.Running))
            {
                return existingId;
            }
        }

        var id = Guid.NewGuid().ToString("N");
        var rec = new TaskRecord
        {
            Id = id,
            Kind = kind,
            Name = name,
            TargetKey = targetKey,
            GroupKey = groupKey,
            DedupeKey = dk,
            State = TaskRunState.Queued,
            Attempts = 0,
            MaxAttempts = maxAttempts
        };
        _records[id] = rec;
        if (dk is not null) _activeDedupe[dk] = id;

        _queue.EnqueueWithId(id, name, async ct =>
        {
            var gate = _groupLocks.GetOrAdd(groupKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                var ctx = new TaskContext(rec, this);
                return await work(ctx, ct);
            }
            finally
            {
                gate.Release();
            }
        }, maxAttempts);

        Changed?.Invoke();
        return id;
    }

    public async Task PauseAsync() => await _queue.PauseAsync();
    public void Resume() => _queue.Resume();
    public void Cancel(string id) => _queue.Cancel(id);
    public void RetryFailed() => _queue.RetryFailed();
    public void ClearCompleted() => _queue.ClearCompleted();

    public async Task ApplyConcurrencyAsync(int concurrency)
    {
        await _queue.StopAsync();
        _queue.Concurrency = concurrency;
        _queue.Start();
        Changed?.Invoke();
    }

    internal void AppendLog(string taskId, string message)
    {
        if (_records.TryGetValue(taskId, out var rec))
        {
            lock (rec.Logs)
            {
                rec.Logs.Add(new TaskLogLine(DateTimeOffset.UtcNow, message));
                if (rec.Logs.Count > 4000) rec.Logs.RemoveRange(0, rec.Logs.Count - 4000);
            }
        }
        Changed?.Invoke();
    }

    private void SyncFromQueue()
    {
        var statuses = _queue.SnapshotStatuses();
        foreach (var s in statuses)
        {
            if (!_records.TryGetValue(s.Id, out var rec))
            {
                rec = new TaskRecord
                {
                    Id = s.Id,
                    Kind = TaskKind.GitStatus,
                    Name = s.Name,
                    TargetKey = "",
                    GroupKey = "",
                    DedupeKey = null,
                    State = s.State,
                    Attempts = s.Attempts,
                    MaxAttempts = s.MaxAttempts
                };
                _records[s.Id] = rec;
            }

            rec.State = s.State;
            rec.Attempts = s.Attempts;
            rec.LastError = s.LastError;
            rec.StartedUtc = s.StartedUtc;
            rec.EndedUtc = s.EndedUtc;

            if (rec.DedupeKey is not null && rec.State is TaskRunState.Succeeded or TaskRunState.Canceled)
            {
                _activeDedupe.TryRemove(rec.DedupeKey, out _);
            }

            if (rec.DedupeKey is not null && rec.State == TaskRunState.Failed && rec.Attempts >= rec.MaxAttempts)
            {
                _activeDedupe.TryRemove(rec.DedupeKey, out _);
            }
        }

        Changed?.Invoke();
    }
}

public sealed class TaskContext
{
    private readonly TaskRecord _rec;
    private readonly TaskCenter _center;

    public TaskContext(TaskRecord rec, TaskCenter center)
    {
        _rec = rec;
        _center = center;
    }

    public string TaskId => _rec.Id;
    public TaskKind Kind => _rec.Kind;
    public string TargetKey => _rec.TargetKey;
    public string GroupKey => _rec.GroupKey;

    public void Log(string message) => _center.AppendLog(_rec.Id, message);
}
