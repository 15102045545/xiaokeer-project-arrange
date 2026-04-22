using System.Text.Json;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.Configuration;
using ProjectArrange.Infrastructure.Process;

namespace ProjectArrange.Infrastructure.Security;

public sealed class GitleaksSecretScanner(IProcessRunner runner, IToolLocator tools) : ISecretScanner
{
    public async Task<Result<SecretScanResult>> ScanAsync(string repoPath, CancellationToken cancellationToken = default)
    {
        var exe = FindGitleaksExecutable(tools);
        if (exe is null)
        {
            return Result<SecretScanResult>.Ok(new SecretScanResult(
                ToolAvailable: false,
                ToolName: "gitleaks",
                Findings: Array.Empty<SecretScanFinding>(),
                RawOutput: "gitleaks not found (PATH or tools/gitleaks)."));
        }

        var reportPath = Path.Combine(Path.GetTempPath(), $"projectarrange-gitleaks-{Guid.NewGuid():N}.json");
        try
        {
            var args = $"detect --source \"{repoPath}\" --report-format json --report-path \"{reportPath}\" --redact --exit-code 0";
            var res = await runner.RunAsync(new ProcessSpec(exe, args, WorkingDirectory: repoPath, TimeoutMilliseconds: 120_000), cancellationToken);

            var raw = res.CombinedOutput;
            var findings = File.Exists(reportPath) ? ParseFindings(await File.ReadAllTextAsync(reportPath, cancellationToken)) : Array.Empty<SecretScanFinding>();

            return Result<SecretScanResult>.Ok(new SecretScanResult(
                ToolAvailable: true,
                ToolName: "gitleaks",
                Findings: findings,
                RawOutput: raw));
        }
        catch (Exception ex)
        {
            return Result<SecretScanResult>.Fail(ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(reportPath)) File.Delete(reportPath);
            }
            catch
            {
            }
        }
    }

    private static IReadOnlyList<SecretScanFinding> ParseFindings(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<SecretScanFinding>();

            var list = new List<SecretScanFinding>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var rule = el.TryGetProperty("RuleID", out var ruleId) ? ruleId.GetString() : null;
                var file = el.TryGetProperty("File", out var f) ? f.GetString() : null;
                var commit = el.TryGetProperty("Commit", out var c) ? c.GetString() : null;
                int? startLine = el.TryGetProperty("StartLine", out var sl) && sl.TryGetInt32(out var v) ? v : null;
                var match = el.TryGetProperty("Match", out var m) ? m.GetString() : null;

                if (!string.IsNullOrWhiteSpace(file) && !string.IsNullOrWhiteSpace(rule))
                {
                    list.Add(new SecretScanFinding(rule!, file!, commit, startLine, match));
                }
            }

            return list;
        }
        catch
        {
            return Array.Empty<SecretScanFinding>();
        }
    }

    private static string? FindGitleaksExecutable(IToolLocator tools)
    {
        var cfg = tools.Resolve("gitleaks");
        if (!string.IsNullOrWhiteSpace(cfg) && !cfg.Equals("gitleaks", StringComparison.OrdinalIgnoreCase)) return cfg;

        var local = Path.Combine(AppContext.BaseDirectory, "tools", "gitleaks", OperatingSystem.IsWindows() ? "gitleaks.exe" : "gitleaks");
        if (File.Exists(local)) return local;
        return "gitleaks";
    }
}
