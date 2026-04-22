using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class UpdateViewModel : ObservableObject
{
    private readonly IUpdateService _updates;

    private string _summary = "";
    private string _raw = "";

    public UpdateViewModel(IUpdateService updates)
    {
        _updates = updates;
        CheckCommand = new AsyncCommand(CheckAsync);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string Raw
    {
        get => _raw;
        set => SetProperty(ref _raw, value);
    }

    public AsyncCommand CheckCommand { get; }

    private async Task CheckAsync()
    {
        Summary = "Checking...";
        Raw = "";

        var res = await _updates.CheckForUpdatesAsync();
        if (!res.IsSuccess)
        {
            Summary = "Update check failed";
            Raw = res.Error ?? "";
            return;
        }

        var info = res.Value!;
        Summary = $"Current: {info.CurrentVersion}  Latest: {info.LatestVersion}";
        Raw = info.RawJson;
    }
}
