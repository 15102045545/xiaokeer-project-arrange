namespace ProjectArrange.Infrastructure.Process;

public sealed record ProcessSpec(
    string FileName,
    string Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? Environment = null,
    int TimeoutMilliseconds = 60_000,
    string? StdInText = null);

public sealed record ProcessResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    bool TimedOut)
{
    public string CombinedOutput =>
        string.IsNullOrWhiteSpace(StdErr) ? StdOut : $"{StdOut}{Environment.NewLine}{StdErr}";
}
