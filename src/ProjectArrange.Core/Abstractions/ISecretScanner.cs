namespace ProjectArrange.Core.Abstractions;

public sealed record SecretScanFinding(
    string RuleId,
    string File,
    string? Commit,
    int? StartLine,
    string? Match);

public sealed record SecretScanResult(
    bool ToolAvailable,
    string ToolName,
    IReadOnlyList<SecretScanFinding> Findings,
    string RawOutput);

public interface ISecretScanner
{
    Task<Result<SecretScanResult>> ScanAsync(string repoPath, CancellationToken cancellationToken = default);
}
