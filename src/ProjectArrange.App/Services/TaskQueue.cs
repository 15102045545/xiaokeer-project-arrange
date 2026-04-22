using System.Collections.Concurrent;
using System.Threading.Channels;
using ProjectArrange.Core;

namespace ProjectArrange.App.Services;

public enum TaskRunState
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public sealed record TaskItem(
    string Id,
    string Name,
    Func<CancellationToken, Task<Result>> Work,
    int MaxAttempts = 3);

public sealed class TaskStatus
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public TaskRunState State { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; init; }
    public string? LastError { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? EndedUtc { get; set; }
}

public sealed class TaskQueue
{
    private readonly Channel<TaskItem> _channel = Channel.CreateUnbounded<TaskItem>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<string, TaskStatus> _statuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskItem> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningCts = new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _pauseGate = new(1, 1);
    private int _concurrency = 2;
    private CancellationTokenSource? _cts;
    private Task[] _workers = Array.Empty<Task>();

    public event Action? Changed;

    public int Concurrency
    {
        get => _concurrency;
        set => _concurrency = Math.Clamp(value, 1, 16);
    }

    public IReadOnlyList<TaskStatus> SnapshotStatuses() =>
        _statuses.Values.OrderByDescending(s => s.StartedUtc ?? DateTimeOffset.MinValue).ToArray();

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _workers = Enumerable.Range(0, Concurrency).Select(_ => Task.Run(() => WorkerLoopAsync(_cts.Token))).ToArray();
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        _cts.Cancel();
        try
        {
            await Task.WhenAll(_workers);
        }
        catch
        {
        }
        _cts.Dispose();
        _cts = null;
        _workers = Array.Empty<Task>();
    }

    public async Task PauseAsync()
    {
        await _pauseGate.WaitAsync();
        Changed?.Invoke();
    }

    public void Resume()
    {
        try
        {
            _pauseGate.Release();
        }
        catch
        {
        }
        Changed?.Invoke();
    }

    public string Enqueue(string name, Func<CancellationToken, Task<Result>> work, int maxAttempts = 3)
    {
        var id = Guid.NewGuid().ToString("N");
        EnqueueWithId(id, name, work, maxAttempts);
        return id;
    }

    public void EnqueueWithId(string id, string name, Func<CancellationToken, Task<Result>> work, int maxAttempts = 3)
    {
        Start();
        var item = new TaskItem(id, name, work, maxAttempts);
        _definitions[id] = item;
        _statuses[id] = new TaskStatus
        {
            Id = id,
            Name = name,
            State = TaskRunState.Queued,
            Attempts = 0,
            MaxAttempts = maxAttempts
        };
        _channel.Writer.TryWrite(item);
        Changed?.Invoke();
    }

    public void Cancel(string id)
    {
        if (_runningCts.TryRemove(id, out var cts))
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
        }

        if (_statuses.TryGetValue(id, out var st))
        {
            st.State = TaskRunState.Canceled;
            st.EndedUtc = DateTimeOffset.UtcNow;
        }
        Changed?.Invoke();
    }

    public void RetryFailed()
    {
        foreach (var st in _statuses.Values.Where(s => s.State == TaskRunState.Failed))
        {
            if (!_definitions.TryGetValue(st.Id, out var def)) continue;
            st.State = TaskRunState.Queued;
            st.LastError = null;
            st.EndedUtc = null;
            _channel.Writer.TryWrite(def);
        }
        Changed?.Invoke();
    }

    public void ClearCompleted()
    {
        var toRemove = _statuses.Values.Where(s => s.State is TaskRunState.Succeeded or TaskRunState.Canceled).Select(s => s.Id).ToArray();
        foreach (var id in toRemove)
        {
            _statuses.TryRemove(id, out _);
            _definitions.TryRemove(id, out _);
        }
        Changed?.Invoke();
    }

    private async Task WorkerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TaskItem item;
            try
            {
                item = await _channel.Reader.ReadAsync(cancellationToken);
            }
            catch
            {
                break;
            }

            await _pauseGate.WaitAsync(cancellationToken);
            _pauseGate.Release();

            if (!_statuses.TryGetValue(item.Id, out var status)) continue;
            if (status.State == TaskRunState.Canceled) continue;

            status.State = TaskRunState.Running;
            status.StartedUtc = DateTimeOffset.UtcNow;
            status.Attempts += 1;
            Changed?.Invoke();

            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runningCts[item.Id] = runCts;

            Result res;
            try
            {
                res = await item.Work(runCts.Token);
            }
            catch (OperationCanceledException)
            {
                res = Result.Fail("Canceled");
            }
            catch (Exception ex)
            {
                res = Result.Fail(ex.Message);
            }
            finally
            {
                _runningCts.TryRemove(item.Id, out _);
            }

            status.EndedUtc = DateTimeOffset.UtcNow;

            if (status.State == TaskRunState.Canceled)
            {
                Changed?.Invoke();
                continue;
            }

            if (res.IsSuccess)
            {
                status.State = TaskRunState.Succeeded;
                status.LastError = null;
            }
            else
            {
                status.State = TaskRunState.Failed;
                status.LastError = res.Error;

                if (status.Attempts < status.MaxAttempts)
                {
                    status.State = TaskRunState.Queued;
                    _channel.Writer.TryWrite(item);
                }
            }

            Changed?.Invoke();
        }
    }
}
