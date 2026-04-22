using ProjectArrange.App.Mvvm;
using ProjectArrange.App.Services;

namespace ProjectArrange.App.ViewModels;

public sealed class TaskCenterViewModel : ObservableObject
{
    private readonly TaskCenter _tasks;
    private IReadOnlyList<TaskRecord> _items = Array.Empty<TaskRecord>();
    private TaskRecord? _selected;
    private string _details = "";
    private string _queueConcurrency = "2";

    public TaskCenterViewModel(TaskCenter tasks)
    {
        _tasks = tasks;
        _tasks.Changed += OnChanged;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        PauseCommand = new AsyncCommand(PauseAsync);
        ResumeCommand = new AsyncCommand(ResumeAsync);
        RetryFailedCommand = new AsyncCommand(RetryFailedAsync);
        ClearCompletedCommand = new AsyncCommand(ClearCompletedAsync);
        ApplyConcurrencyCommand = new AsyncCommand(ApplyConcurrencyAsync);
        CancelSelectedCommand = new AsyncCommand(CancelSelectedAsync, () => Selected is not null);
    }

    public IReadOnlyList<TaskRecord> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public TaskRecord? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            RefreshDetails();
            CancelSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public string QueueConcurrency
    {
        get => _queueConcurrency;
        set => SetProperty(ref _queueConcurrency, value);
    }

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand PauseCommand { get; }
    public AsyncCommand ResumeCommand { get; }
    public AsyncCommand RetryFailedCommand { get; }
    public AsyncCommand ClearCompletedCommand { get; }
    public AsyncCommand ApplyConcurrencyCommand { get; }
    public AsyncCommand CancelSelectedCommand { get; }

    private Task RefreshAsync()
    {
        Items = _tasks.Snapshot();
        if (Selected is not null)
        {
            Selected = _tasks.GetById(Selected.Id);
        }
        RefreshDetails();
        QueueConcurrency = _tasks.Concurrency.ToString();
        return Task.CompletedTask;
    }

    private void RefreshDetails()
    {
        if (Selected is null)
        {
            Details = "";
            return;
        }

        var rec = _tasks.GetById(Selected.Id) ?? Selected;
        var logs = "";
        lock (rec.Logs)
        {
            logs = string.Join(Environment.NewLine, rec.Logs.Select(l => $"{l.Utc:O} {l.Message}"));
        }

        Details = string.Join(Environment.NewLine, new[]
        {
            $"Id: {rec.Id}",
            $"Kind: {rec.Kind}",
            $"State: {rec.State}",
            $"Attempts: {rec.Attempts}/{rec.MaxAttempts}",
            $"TargetKey: {rec.TargetKey}",
            $"GroupKey: {rec.GroupKey}",
            $"DedupeKey: {rec.DedupeKey}",
            $"StartedUtc: {rec.StartedUtc:O}",
            $"EndedUtc: {rec.EndedUtc:O}",
            $"LastError: {rec.LastError}",
            "",
            logs
        });
    }

    private async Task PauseAsync()
    {
        await _tasks.PauseAsync();
        await RefreshAsync();
    }

    private Task ResumeAsync()
    {
        _tasks.Resume();
        return RefreshAsync();
    }

    private Task RetryFailedAsync()
    {
        _tasks.RetryFailed();
        return RefreshAsync();
    }

    private Task ClearCompletedAsync()
    {
        _tasks.ClearCompleted();
        return RefreshAsync();
    }

    private async Task ApplyConcurrencyAsync()
    {
        if (!int.TryParse(QueueConcurrency, out var c)) c = _tasks.Concurrency;
        await _tasks.ApplyConcurrencyAsync(c);
        await RefreshAsync();
    }

    private Task CancelSelectedAsync()
    {
        if (Selected is null) return Task.CompletedTask;
        _tasks.Cancel(Selected.Id);
        return RefreshAsync();
    }

    private void OnChanged()
    {
        _ = RefreshAsync();
    }
}

