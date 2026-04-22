using ProjectArrange.App.Mvvm;
using ProjectArrange.App.Services;

namespace ProjectArrange.App.ViewModels;

public sealed class LogsViewModel : ObservableObject
{
    private readonly OperationLog _log;
    private string _text = "";

    public LogsViewModel(OperationLog log)
    {
        _log = log;
        _log.Changed += OnChanged;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public AsyncCommand RefreshCommand { get; }

    private Task RefreshAsync()
    {
        var entries = _log.Snapshot();
        Text = string.Join(Environment.NewLine, entries.Select(e => $"{e.Utc:O} [{e.Level}] {e.Message}"));
        return Task.CompletedTask;
    }

    private void OnChanged()
    {
        _ = RefreshAsync();
    }
}

