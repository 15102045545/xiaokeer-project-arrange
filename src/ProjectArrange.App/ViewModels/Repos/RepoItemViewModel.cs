using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Git;
using ProjectArrange.Core.GitHub;

namespace ProjectArrange.App.ViewModels.Repos;

public sealed class RepoItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _path;
    private string _gitSummary;
    private string _remote;
    private string _gitHubRepo;
    private string _lastScan;

    public RepoItemViewModel(string path)
    {
        _path = path;
        _gitSummary = "";
        _remote = "";
        _gitHubRepo = "";
        _lastScan = "";
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public string GitSummary
    {
        get => _gitSummary;
        set => SetProperty(ref _gitSummary, value);
    }

    public string Remote
    {
        get => _remote;
        set => SetProperty(ref _remote, value);
    }

    public string GitHubRepo
    {
        get => _gitHubRepo;
        set => SetProperty(ref _gitHubRepo, value);
    }

    public string LastScan
    {
        get => _lastScan;
        set => SetProperty(ref _lastScan, value);
    }

    public GitRepositoryStatus? LastGitStatus { get; set; }
    public GitHubRepoRef? ParsedGitHubRepo { get; set; }
}

