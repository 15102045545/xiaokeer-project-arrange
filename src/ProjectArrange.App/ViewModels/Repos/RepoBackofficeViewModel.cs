using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using ProjectArrange.App.Mvvm;
using ProjectArrange.App.Services;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Core.OpenSourceify;
using ProjectArrange.Core.Repos;
using ProjectArrange.Core.GitHub;
using ProjectArrange.Infrastructure.GitHub;
using ProjectArrange.Infrastructure.Repos;

namespace ProjectArrange.App.ViewModels.Repos;

public sealed class RepoBackofficeViewModel : ObservableObject
{
    private readonly IGitService _git;
    private readonly ISecretScanner _scanner;
    private readonly IGitHubRepositoryService _ghRepos;
    private readonly IRepoRegistryService _registry;
    private readonly IOpenSourceifyService _openSourceify;
    private readonly IConfiguration _configuration;
    private readonly OperationLog _log;
    private readonly TaskCenter _tasks;
    private readonly ConcurrentDictionary<string, string> _openSourceifyPlans = new(StringComparer.OrdinalIgnoreCase);

    private string _roots;
    private string _status;
    private string _batchDescription;
    private bool _batchSetVisibility;
    private bool _batchMakePrivate;
    private string _details;
    private RepoItemViewModel? _selected;
    private int _queueConcurrency;
    private string _queueText;

    public RepoBackofficeViewModel(
        IGitService git,
        ISecretScanner scanner,
        IGitHubRepositoryService ghRepos,
        IRepoRegistryService registry,
        IOpenSourceifyService openSourceify,
        IConfiguration configuration,
        TaskCenter tasks,
        OperationLog log)
    {
        _git = git;
        _scanner = scanner;
        _ghRepos = ghRepos;
        _registry = registry;
        _openSourceify = openSourceify;
        _configuration = configuration;
        _tasks = tasks;
        _log = log;

        Items = new ObservableCollection<RepoItemViewModel>();

        _roots = Environment.CurrentDirectory;
        _status = "";
        _batchDescription = "";
        _details = "";
        _queueConcurrency = 2;
        _queueText = "";
        _tasks.Changed += RefreshQueueText;

        DiscoverCommand = new AsyncCommand(DiscoverAsync);
        LoadRegistryCommand = new AsyncCommand(LoadRegistryAsync);
        RefreshSelectedGitStatusCommand = new AsyncCommand(RefreshSelectedGitStatusAsync);
        ScanSelectedSecretsCommand = new AsyncCommand(ScanSelectedSecretsAsync);
        OpenSourceifyDryRunCommand = new AsyncCommand(OpenSourceifyDryRunAsync);
        UpdateSelectedReposCommand = new AsyncCommand(UpdateSelectedReposAsync);
        SelectAllCommand = new AsyncCommand(SelectAllAsync);
        ClearSelectionCommand = new AsyncCommand(ClearSelectionAsync);

        PauseQueueCommand = new AsyncCommand(PauseQueueAsync);
        ResumeQueueCommand = new AsyncCommand(ResumeQueueAsync);
        RetryFailedCommand = new AsyncCommand(RetryFailedAsync);
        ClearCompletedCommand = new AsyncCommand(ClearCompletedAsync);
        ApplyConcurrencyCommand = new AsyncCommand(ApplyConcurrencyAsync);

        ExportSnapshotCommand = new AsyncCommand(ExportSnapshotAsync);
        ImportSnapshotCommand = new AsyncCommand(ImportSnapshotAsync);
    }

    public ObservableCollection<RepoItemViewModel> Items { get; }

    public string Roots
    {
        get => _roots;
        set => SetProperty(ref _roots, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string BatchDescription
    {
        get => _batchDescription;
        set => SetProperty(ref _batchDescription, value);
    }

    public bool BatchSetVisibility
    {
        get => _batchSetVisibility;
        set => SetProperty(ref _batchSetVisibility, value);
    }

    public bool BatchMakePrivate
    {
        get => _batchMakePrivate;
        set => SetProperty(ref _batchMakePrivate, value);
    }

    public RepoItemViewModel? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                _ = RefreshDetailsAsync();
            }
        }
    }

    public string Details
    {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    public int QueueConcurrency
    {
        get => _queueConcurrency;
        set => SetProperty(ref _queueConcurrency, Math.Clamp(value, 1, 16));
    }

    public string QueueText
    {
        get => _queueText;
        set => SetProperty(ref _queueText, value);
    }

    public AsyncCommand DiscoverCommand { get; }
    public AsyncCommand LoadRegistryCommand { get; }
    public AsyncCommand RefreshSelectedGitStatusCommand { get; }
    public AsyncCommand ScanSelectedSecretsCommand { get; }
    public AsyncCommand OpenSourceifyDryRunCommand { get; }
    public AsyncCommand UpdateSelectedReposCommand { get; }
    public AsyncCommand SelectAllCommand { get; }
    public AsyncCommand ClearSelectionCommand { get; }

    public AsyncCommand PauseQueueCommand { get; }
    public AsyncCommand ResumeQueueCommand { get; }
    public AsyncCommand RetryFailedCommand { get; }
    public AsyncCommand ClearCompletedCommand { get; }
    public AsyncCommand ApplyConcurrencyCommand { get; }

    public AsyncCommand ExportSnapshotCommand { get; }
    public AsyncCommand ImportSnapshotCommand { get; }

    private Task SelectAllAsync()
    {
        foreach (var i in Items) i.IsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelectionAsync()
    {
        foreach (var i in Items) i.IsSelected = false;
        return Task.CompletedTask;
    }

    private async Task DiscoverAsync()
    {
        Status = "Discovering...";
        _log.Info($"Discover start: {Roots}");

        var roots = Roots
            .Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        var repos = LocalRepoDiscovery.DiscoverGitRepositories(roots);
        Items.Clear();
        foreach (var r in repos.OrderBy(r => r))
        {
            Items.Add(new RepoItemViewModel(r));
            await _registry.UpsertAsync(new RepoRegistryEntry(
                Path: r,
                OriginUrl: null,
                GitHubFullName: null,
                AddedUtc: DateTimeOffset.UtcNow,
                LastSeenUtc: DateTimeOffset.UtcNow));
        }

        Status = $"Repos: {Items.Count}";
        _log.Info($"Discover done: {Items.Count}");
    }

    private async Task LoadRegistryAsync()
    {
        Status = "Loading registry...";
        var res = await _registry.GetAllAsync();
        if (!res.IsSuccess)
        {
            Status = "Registry load failed";
            _log.Error(res.Error ?? "");
            return;
        }

        Items.Clear();
        foreach (var r in res.Value!.OrderBy(r => r.Path))
        {
            var item = new RepoItemViewModel(r.Path)
            {
                Remote = r.OriginUrl ?? "",
                GitHubRepo = r.GitHubFullName ?? ""
            };
            Items.Add(item);

            var gitSnap = await _registry.GetLastGitSnapshotAsync(r.Path);
            if (gitSnap.IsSuccess && gitSnap.Value is not null)
            {
                var s = gitSnap.Value;
                item.GitSummary = $"{s!.Branch} a{s.Ahead} b{s.Behind} clean={s.IsClean} st={s.StagedChanges} us={s.UnstagedChanges} un={s.UntrackedFiles}";
            }

            var scanSnap = await _registry.GetLastSecretScanSnapshotAsync(r.Path);
            if (scanSnap.IsSuccess && scanSnap.Value is not null)
            {
                var ss = scanSnap.Value;
                item.LastScan = ss!.ToolAvailable ? $"Findings={ss.FindingsCount}" : "Scanner missing";
            }
        }

        Status = $"Loaded: {Items.Count}";
        _log.Info($"Registry loaded: {Items.Count}");
    }

    private async Task RefreshSelectedGitStatusAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            Status = "No selection";
            return;
        }

        Status = $"Enqueued git refresh: {selected.Length}";
        _log.Info($"Git status enqueue: {selected.Length}");

        foreach (var item in selected)
        {
            _tasks.Enqueue(
                TaskKind.GitStatus,
                $"git-status {item.Path}",
                targetKey: item.Path,
                groupKey: item.Path,
                work: async (ctx, ct) =>
            {
                ctx.Log("start");
                var res = await _git.GetRepositoryStatusAsync(item.Path, ct);
                if (!res.IsSuccess)
                {
                    Ui(() => item.GitSummary = $"ERR: {res.Error}");
                    ctx.Log($"fail {res.Error}");
                    return Result.Fail(res.Error ?? "git status failed");
                }

                var s = res.Value!;
                var summary = $"{s.Branch.Branch} a{s.Branch.Ahead} b{s.Branch.Behind} clean={s.WorkingTree.IsClean} st={s.WorkingTree.StagedChanges} us={s.WorkingTree.UnstagedChanges} un={s.WorkingTree.UntrackedFiles}";

                var origin = s.Remotes.FirstOrDefault(r => r.Name.Equals("origin", StringComparison.OrdinalIgnoreCase));
                var url = origin?.FetchUrl ?? s.Remotes.FirstOrDefault()?.FetchUrl ?? "";
                var ghFull = "";
                GitHubRepoRef? repoRef = null;
                if (GitHubRemoteParser.TryParseRepo(url, out var repo))
                {
                    repoRef = repo;
                    ghFull = repo.FullName;
                }

                Ui(() =>
                {
                    item.LastGitStatus = s;
                    item.GitSummary = summary;
                    item.Remote = url;
                    item.ParsedGitHubRepo = repoRef;
                    item.GitHubRepo = ghFull;
                });

                await _registry.UpsertAsync(new RepoRegistryEntry(
                    Path: item.Path,
                    OriginUrl: string.IsNullOrWhiteSpace(url) ? null : url,
                    GitHubFullName: string.IsNullOrWhiteSpace(ghFull) ? null : ghFull,
                    AddedUtc: DateTimeOffset.UtcNow,
                    LastSeenUtc: DateTimeOffset.UtcNow), ct);

                await _registry.SaveGitSnapshotAsync(new RepoGitSnapshot(
                    Path: item.Path,
                    Branch: s.Branch.Branch,
                    Ahead: s.Branch.Ahead,
                    Behind: s.Branch.Behind,
                    IsClean: s.WorkingTree.IsClean,
                    StagedChanges: s.WorkingTree.StagedChanges,
                    UnstagedChanges: s.WorkingTree.UnstagedChanges,
                    UntrackedFiles: s.WorkingTree.UntrackedFiles,
                    Utc: DateTimeOffset.UtcNow), ct);

                _log.Info($"git-status OK {item.Path}");
                ctx.Log("ok");
                return Result.Ok();
            },
                dedupeKey: $"git-status|{item.Path}");
        }
    }

    private async Task ScanSelectedSecretsAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            Status = "No selection";
            return;
        }

        Status = $"Enqueued secret scan: {selected.Length}";
        _log.Info($"Secret scan enqueue: {selected.Length}");

        foreach (var item in selected)
        {
            _tasks.Enqueue(
                TaskKind.SecretScan,
                $"scan {item.Path}",
                targetKey: item.Path,
                groupKey: item.Path,
                work: async (ctx, ct) =>
            {
                ctx.Log("start");
                var res = await _scanner.ScanAsync(item.Path, ct);
                if (!res.IsSuccess)
                {
                    Ui(() => item.LastScan = $"ERR: {res.Error}");
                    ctx.Log($"fail {res.Error}");
                    return Result.Fail(res.Error ?? "scan failed");
                }

                var r = res.Value!;
                var summary = r.ToolAvailable ? $"Findings={r.Findings.Count}" : "Scanner missing";
                Ui(() => item.LastScan = summary);

                await _registry.SaveSecretScanSnapshotAsync(new RepoSecretScanSnapshot(
                    Path: item.Path,
                    ToolAvailable: r.ToolAvailable,
                    FindingsCount: r.Findings.Count,
                    RawOutput: r.RawOutput ?? "",
                    Utc: DateTimeOffset.UtcNow), ct);

                _log.Info($"scan OK {item.Path} {summary}");
                ctx.Log(summary);
                return Result.Ok();
            },
                dedupeKey: $"scan|{item.Path}");
        }
    }

    private async Task OpenSourceifyDryRunAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            Status = "No repos selected";
            return;
        }

        var recipe = LoadRecipe();
        Status = $"Enqueued OpenSourceify dry-run: {selected.Length}";
        _log.Info($"OpenSourceify dry-run enqueue: {selected.Length}");

        foreach (var item in selected)
        {
            var repoPath = item.Path;
            _tasks.Enqueue(
                TaskKind.OpenSourceifyDryRun,
                $"opensourceify-dryrun {Path.GetFileName(repoPath)}",
                targetKey: repoPath,
                groupKey: repoPath,
                work: async (ctx, ct) =>
                {
                    ctx.Log("start");
                    var res = await _openSourceify.BuildPlanAsync(repoPath, recipe, dryRun: true, ct);
                    if (!res.IsSuccess)
                    {
                        ctx.Log($"fail {res.Error}");
                        return Result.Fail(res.Error ?? "OpenSourceify dry-run failed");
                    }

                    var planText = FormatPlan(res.Value!);
                    _openSourceifyPlans[repoPath] = planText;
                    ctx.Log("ok");

                    Ui(() =>
                    {
                        if (Selected?.Path.Equals(repoPath, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            _ = RefreshDetailsAsync();
                        }
                    });
                    return Result.Ok();
                },
                dedupeKey: $"opensourceify-dryrun|{repoPath}");
        }
    }

    private async Task UpdateSelectedReposAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            Status = "No selection";
            return;
        }

        var targets = selected.Where(s => s.ParsedGitHubRepo is not null).ToArray();
        if (targets.Length == 0)
        {
            Status = "No GitHub repos parsed from origin";
            return;
        }

        var update = new GitHubRepoUpdate(
            Description: string.IsNullOrWhiteSpace(BatchDescription) ? null : BatchDescription,
            IsPrivate: BatchSetVisibility ? BatchMakePrivate : null,
            Topics: null);

        Status = $"Enqueued GitHub update: {targets.Length}";
        _log.Info($"GitHub update enqueue: {targets.Length}");

        foreach (var item in targets)
        {
            var repo = item.ParsedGitHubRepo!;
            _tasks.Enqueue(
                TaskKind.GitHubUpdate,
                $"github-update {repo.FullName}",
                targetKey: repo.FullName,
                groupKey: item.Path,
                work: async (ctx, ct) =>
            {
                ctx.Log("start");
                var res = await _ghRepos.UpdateRepositoryAsync(repo, update, ct);
                if (!res.IsSuccess)
                {
                    _log.Error($"{repo.FullName} FAIL {res.Error}");
                    Ui(() => item.GitHubRepo = $"{repo.FullName} (FAIL)");
                    ctx.Log($"fail {res.Error}");
                    return Result.Fail(res.Error ?? "github update failed");
                }

                _log.Info($"{repo.FullName} OK");
                Ui(() => item.GitHubRepo = $"{repo.FullName} (OK)");
                ctx.Log("ok");
                return Result.Ok();
            },
                dedupeKey: $"github-update|{repo.FullName}");
        }
    }

    private async Task RefreshDetailsAsync()
    {
        if (Selected is null)
        {
            Details = "";
            return;
        }

        var path = Selected.Path;
        var git = await _registry.GetLastGitSnapshotAsync(path);
        var scan = await _registry.GetLastSecretScanSnapshotAsync(path);
        _openSourceifyPlans.TryGetValue(path, out var osPlan);

        var gitText = git.IsSuccess && git.Value is not null
            ? $"GitSnapshot:{Environment.NewLine}{JsonGit(git.Value!)}"
            : "GitSnapshot: (none)";

        var scanText = scan.IsSuccess && scan.Value is not null
            ? $"SecretScan:{Environment.NewLine}Utc={scan.Value!.Utc:O}{Environment.NewLine}ToolAvailable={scan.Value.ToolAvailable}{Environment.NewLine}Findings={scan.Value.FindingsCount}{Environment.NewLine}{Environment.NewLine}{scan.Value.RawOutput}"
            : "SecretScan: (none)";

        var osText = string.IsNullOrWhiteSpace(osPlan)
            ? "OpenSourceifyDryRun: (none)"
            : $"OpenSourceifyDryRun:{Environment.NewLine}{osPlan}";

        Details =
            $"Path: {path}{Environment.NewLine}" +
            $"Origin: {Selected.Remote}{Environment.NewLine}" +
            $"GitHub: {Selected.GitHubRepo}{Environment.NewLine}{Environment.NewLine}" +
            $"{gitText}{Environment.NewLine}{Environment.NewLine}" +
            $"{scanText}{Environment.NewLine}{Environment.NewLine}" +
            $"{osText}";
    }

    private OpenSourceifyRecipe LoadRecipe()
    {
        var fromCfg = _configuration.GetSection("OpenSourceify:Recipe").Get<OpenSourceifyRecipe>();
        if (fromCfg is not null &&
            fromCfg.EnsureFiles is not null &&
            fromCfg.GitignoreRules is not null)
        {
            return fromCfg;
        }

        return new OpenSourceifyRecipe(
            EnsureFiles: new[]
            {
                new EnsureFileRule("README.md", ExistingFilePolicy.Skip, "# README\n"),
                new EnsureFileRule("LICENSE", ExistingFilePolicy.Skip, "MIT License\n"),
                new EnsureFileRule(".gitignore", ExistingFilePolicy.Append, "")
            },
            GitignoreRules: new[]
            {
                ".vs/",
                "bin/",
                "obj/",
                "*.user",
                "*.suo",
                ".DS_Store"
            });
    }

    private static string FormatPlan(OpenSourceifyPlan plan)
    {
        var lines = new List<string>
        {
            $"RepoRoot={plan.RepoRoot}",
            $"DryRun={plan.DryRun}",
            $"Actions={plan.Actions.Count}",
            $"BadTrackedFiles={plan.BadTrackedFiles.Count}"
        };

        if (plan.Actions.Count > 0)
        {
            lines.Add("");
            lines.Add("Actions:");
            lines.AddRange(plan.Actions.Select(a => $"- {a.Kind} {a.Target} :: {a.Summary}"));
        }

        if (plan.BadTrackedFiles.Count > 0)
        {
            lines.Add("");
            lines.Add("BadTrackedFiles:");
            lines.AddRange(plan.BadTrackedFiles.Select(f => $"- {f}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string JsonGit(RepoGitSnapshot s) =>
        $"Utc={s.Utc:O}{Environment.NewLine}Branch={s.Branch}{Environment.NewLine}Ahead={s.Ahead} Behind={s.Behind}{Environment.NewLine}Clean={s.IsClean}{Environment.NewLine}Staged={s.StagedChanges} Unstaged={s.UnstagedChanges} Untracked={s.UntrackedFiles}";

    private async Task PauseQueueAsync()
    {
        await _tasks.PauseAsync();
        _log.Info("Queue paused");
        RefreshQueueText();
    }

    private Task ResumeQueueAsync()
    {
        _tasks.Resume();
        _log.Info("Queue resumed");
        RefreshQueueText();
        return Task.CompletedTask;
    }

    private Task RetryFailedAsync()
    {
        _tasks.RetryFailed();
        _log.Info("Queue retry failed");
        RefreshQueueText();
        return Task.CompletedTask;
    }

    private Task ClearCompletedAsync()
    {
        _tasks.ClearCompleted();
        _log.Info("Queue clear completed");
        RefreshQueueText();
        return Task.CompletedTask;
    }

    private async Task ApplyConcurrencyAsync()
    {
        await _tasks.ApplyConcurrencyAsync(QueueConcurrency);
        _log.Info($"Queue concurrency={QueueConcurrency}");
        RefreshQueueText();
    }

    private async Task ExportSnapshotAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export repo registry snapshot",
            Filter = "JSON (*.json)|*.json",
            FileName = $"repo-registry-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json"
        };

        if (dialog.ShowDialog() != true) return;

        var res = await _registry.ExportSnapshotAsync(dialog.FileName);
        Status = res.IsSuccess ? $"Exported: {dialog.FileName}" : "Export failed";
        _log.Info(Status);
    }

    private async Task ImportSnapshotAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import repo registry snapshot",
            Filter = "JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true) return;

        var res = await _registry.ImportSnapshotAsync(dialog.FileName);
        Status = res.IsSuccess ? $"Imported: {res.Value}" : "Import failed";
        _log.Info(Status);
        await LoadRegistryAsync();
    }

    private void RefreshQueueText()
    {
        var st = _tasks.Snapshot();
        QueueText = string.Join(Environment.NewLine, st.Select(s =>
            $"{s.Id} {s.State} {s.Attempts}/{s.MaxAttempts} {s.Name} {s.LastError ?? ""}".TrimEnd()));
    }

    private static void Ui(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) action();
        else d.Invoke(action);
    }
}
