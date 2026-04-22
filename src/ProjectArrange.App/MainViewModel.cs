using ProjectArrange.App.ViewModels;

namespace ProjectArrange.App;

public sealed class MainViewModel
{
    public MainViewModel(
        TaskCenterViewModel tasks,
        ConfigViewModel config,
        AiViewModel ai,
        GitStatusViewModel git,
        GhStatusViewModel gh,
        GitHubAuthViewModel gitHubAuth,
        DatabaseViewModel database,
        PresenceViewModel presence,
        P2pViewModel p2p,
        FileTransferViewModel fileTransfer,
        DiagnosticsViewModel diagnostics,
        ProcessViewModel process,
        IdentityViewModel identity,
        GitParserViewModel gitParser,
        P2pInternalsViewModel p2pInternals,
        ViewModels.Repos.RepoBackofficeViewModel repos,
        LogsViewModel logs,
        SecretScanViewModel scan,
        UpdateViewModel update)
    {
        Tasks = tasks;
        Config = config;
        Ai = ai;
        Git = git;
        Gh = gh;
        GitHubAuth = gitHubAuth;
        Database = database;
        Presence = presence;
        P2p = p2p;
        FileTransfer = fileTransfer;
        Diagnostics = diagnostics;
        Process = process;
        Identity = identity;
        GitParser = gitParser;
        P2pInternals = p2pInternals;
        Repos = repos;
        Logs = logs;
        SecretScan = scan;
        Update = update;
    }

    public TaskCenterViewModel Tasks { get; }
    public ConfigViewModel Config { get; }
    public AiViewModel Ai { get; }
    public GitStatusViewModel Git { get; }
    public GhStatusViewModel Gh { get; }
    public GitHubAuthViewModel GitHubAuth { get; }
    public DatabaseViewModel Database { get; }
    public PresenceViewModel Presence { get; }
    public P2pViewModel P2p { get; }
    public FileTransferViewModel FileTransfer { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public ProcessViewModel Process { get; }
    public IdentityViewModel Identity { get; }
    public GitParserViewModel GitParser { get; }
    public P2pInternalsViewModel P2pInternals { get; }
    public ViewModels.Repos.RepoBackofficeViewModel Repos { get; }
    public LogsViewModel Logs { get; }
    public SecretScanViewModel SecretScan { get; }
    public UpdateViewModel Update { get; }
}
