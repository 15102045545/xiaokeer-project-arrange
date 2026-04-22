using ProjectArrange.App.Mvvm;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.App.ViewModels;

public sealed class SecretScanViewModel : ObservableObject
{
    private readonly ISecretScanner _scanner;

    private string _repoPath = Environment.CurrentDirectory;
    private string _summary = "";
    private string _raw = "";
    private string _findings = "";

    public SecretScanViewModel(ISecretScanner scanner)
    {
        _scanner = scanner;
        ScanCommand = new AsyncCommand(ScanAsync);
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

    public string Raw
    {
        get => _raw;
        set => SetProperty(ref _raw, value);
    }

    public string Findings
    {
        get => _findings;
        set => SetProperty(ref _findings, value);
    }

    public AsyncCommand ScanCommand { get; }

    private async Task ScanAsync()
    {
        Summary = "Scanning...";
        Raw = "";
        Findings = "";

        var res = await _scanner.ScanAsync(RepoPath);
        if (!res.IsSuccess)
        {
            Summary = "Scan failed";
            Raw = res.Error ?? "";
            return;
        }

        var r = res.Value!;
        if (!r.ToolAvailable)
        {
            Summary = "Scanner tool not available";
            Raw = r.RawOutput;
            return;
        }

        Summary = $"Findings: {r.Findings.Count}";
        Findings = string.Join(Environment.NewLine, r.Findings.Select(f =>
            $"{f.RuleId}  {f.File}:{f.StartLine?.ToString() ?? "?"}  {f.Commit ?? ""}".TrimEnd()));
        Raw = r.RawOutput;
    }
}
