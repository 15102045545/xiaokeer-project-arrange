using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class GitStatusViewModel : ObservableObject
{
    private readonly IGitService _git;

    private string _repoPath = Environment.CurrentDirectory;
    private string _summary = "";
    private string _details = "";

    public GitStatusViewModel(IGitService git)
    {
        _git = git;
        RefreshCommand = new AsyncCommand(RefreshAsync);
    }

    public string RepoPath
    {
        get => _repoPath;
        set => SetProperty(ref _repoPath, value);
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

    public AsyncCommand RefreshCommand { get; }

    private async Task RefreshAsync()
    {
        Summary = "Checking...";
        Details = "";

        var isRepo = await _git.IsGitRepositoryAsync(RepoPath);
        if (!isRepo.IsSuccess)
        {
            Summary = "Git error";
            Details = isRepo.Error ?? "";
            return;
        }

        if (!isRepo.Value)
        {
            Summary = "Not a git repository";
            Details = "";
            return;
        }

        var status = await _git.GetRepositoryStatusAsync(RepoPath);
        if (!status.IsSuccess)
        {
            Summary = "Git status error";
            Details = status.Error ?? "";
            return;
        }

        var s = status.Value!;
        Summary = $"{s.Branch.Branch}  ahead {s.Branch.Ahead}  behind {s.Branch.Behind}  clean {s.WorkingTree.IsClean}";
        Details =
            $"Root: {s.RootPath}{Environment.NewLine}" +
            $"Branch: {s.Branch.Branch}{Environment.NewLine}" +
            $"Upstream: {s.Branch.HasUpstream}{Environment.NewLine}" +
            $"Ahead/Behind: {s.Branch.Ahead}/{s.Branch.Behind}{Environment.NewLine}" +
            $"Staged/Unstaged/Untracked: {s.WorkingTree.StagedChanges}/{s.WorkingTree.UnstagedChanges}/{s.WorkingTree.UntrackedFiles}{Environment.NewLine}" +
            $"Remotes:{Environment.NewLine}" +
            string.Join(Environment.NewLine, s.Remotes.Select(r => $"  {r.Name}  fetch={r.FetchUrl}  push={r.PushUrl}"));
    }
}
