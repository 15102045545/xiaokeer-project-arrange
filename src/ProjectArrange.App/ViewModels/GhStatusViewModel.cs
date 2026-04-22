using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class GhStatusViewModel : ObservableObject
{
    private readonly IGhCliService _gh;

    private string _summary = "";
    private string _raw = "";

    public GhStatusViewModel(IGhCliService gh)
    {
        _gh = gh;
        RefreshCommand = new AsyncCommand(RefreshAsync);
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

    public AsyncCommand RefreshCommand { get; }

    private async Task RefreshAsync()
    {
        Summary = "Checking...";
        Raw = "";

        var res = await _gh.GetAuthStatusAsync();
        if (!res.IsSuccess)
        {
            Summary = "gh error";
            Raw = res.Error ?? "";
            return;
        }

        var s = res.Value!;
        Summary = s.IsInstalled
            ? (s.IsLoggedIn ? $"gh logged in as {s.User ?? "unknown"}" : "gh installed but not logged in")
            : "gh not installed";
        Raw = s.RawOutput;
    }
}
