using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class GitHubAuthViewModel : ObservableObject
{
    private readonly IAppSecretsStore _secrets;
    private readonly IGitHubAccountService _account;

    private string _tokenInput = "";
    private string _summary = "";
    private string _details = "";

    public GitHubAuthViewModel(IAppSecretsStore secrets, IGitHubAccountService account)
    {
        _secrets = secrets;
        _account = account;
        SaveTokenCommand = new AsyncCommand(SaveTokenAsync);
        ClearTokenCommand = new AsyncCommand(ClearTokenAsync);
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string TokenInput
    {
        get => _tokenInput;
        set => SetProperty(ref _tokenInput, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public AsyncCommand SaveTokenCommand { get; }
    public AsyncCommand ClearTokenCommand { get; }
    public AsyncCommand RefreshCommand { get; }

    private async Task SaveTokenAsync()
    {
        Summary = "Saving...";
        Details = "";
        var res = await _secrets.SaveGitHubTokenAsync(TokenInput.Trim());
        if (!res.IsSuccess)
        {
            Summary = "Save failed";
            Details = res.Error ?? "";
            return;
        }

        TokenInput = "";
        await RefreshAsync();
    }

    private async Task ClearTokenAsync()
    {
        Summary = "Clearing...";
        Details = "";
        var res = await _secrets.ClearGitHubTokenAsync();
        if (!res.IsSuccess)
        {
            Summary = "Clear failed";
            Details = res.Error ?? "";
            return;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        Summary = "Checking...";
        Details = "";

        var tokenRes = await _secrets.GetGitHubTokenAsync();
        if (!tokenRes.IsSuccess)
        {
            Summary = "Token read error";
            Details = tokenRes.Error ?? "";
            return;
        }

        if (string.IsNullOrWhiteSpace(tokenRes.Value))
        {
            Summary = "Token not set";
            Details = "";
            return;
        }

        var userRes = await _account.GetCurrentUserAsync();
        if (!userRes.IsSuccess)
        {
            Summary = "Token set, but API check failed";
            Details = userRes.Error ?? "";
            return;
        }

        Summary = $"Logged in as {userRes.Value!.Login}";
        Details = $"UserId: {userRes.Value.Id}";
    }
}
