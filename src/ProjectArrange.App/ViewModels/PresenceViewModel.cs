using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class PresenceViewModel : ObservableObject
{
    private readonly IPresenceService _presence;

    private string _note = "";
    private string _summary = "";
    private string _peersText = "";

    public PresenceViewModel(IPresenceService presence)
    {
        _presence = presence;
        PublishCommand = new AsyncCommand(PublishAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string PeersText
    {
        get => _peersText;
        set => SetProperty(ref _peersText, value);
    }

    public AsyncCommand PublishCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    private async Task PublishAsync()
    {
        Summary = "Publishing...";
        var r = await _presence.PublishHeartbeatAsync(Note);
        if (!r.IsSuccess)
        {
            Summary = "Publish failed";
            PeersText = r.Error ?? "";
            return;
        }

        Summary = $"Last publish: {r.Value!.LastSeenUtc:O}";
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var res = await _presence.GetPeersAsync();
        if (!res.IsSuccess)
        {
            PeersText = res.Error ?? "";
            return;
        }

        PeersText = string.Join(Environment.NewLine, res.Value!.Select(p =>
            $"{p.MachineName} ({p.MachineId})  {p.LastSeenUtc:O}  {p.Note ?? ""}".TrimEnd()));
    }
}
