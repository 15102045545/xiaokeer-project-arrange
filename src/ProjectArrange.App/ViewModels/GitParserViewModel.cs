using ProjectArrange.App.Mvvm;
using ProjectArrange.Infrastructure.Git;

namespace ProjectArrange.App.ViewModels;

public sealed class GitParserViewModel : ObservableObject
{
    private string _raw = "";
    private string _result = "";

    public GitParserViewModel()
    {
        ParseCommand = new AsyncCommand(ParseAsync);
    }

    public string Raw
    {
        get => _raw;
        set => SetProperty(ref _raw, value);
    }

    public string Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    public AsyncCommand ParseCommand { get; }

    private Task ParseAsync()
    {
        var (branch, tree) = GitStatusParser.ParsePorcelainV1(Raw ?? "");
        Result =
            $"Branch={branch.Branch}{Environment.NewLine}" +
            $"HasUpstream={branch.HasUpstream}{Environment.NewLine}" +
            $"Ahead={branch.Ahead}{Environment.NewLine}" +
            $"Behind={branch.Behind}{Environment.NewLine}" +
            $"Detached={branch.IsDetachedHead}{Environment.NewLine}" +
            $"{Environment.NewLine}" +
            $"IsClean={tree.IsClean}{Environment.NewLine}" +
            $"Staged={tree.StagedChanges}{Environment.NewLine}" +
            $"Unstaged={tree.UnstagedChanges}{Environment.NewLine}" +
            $"Untracked={tree.UntrackedFiles}";
        return Task.CompletedTask;
    }
}

