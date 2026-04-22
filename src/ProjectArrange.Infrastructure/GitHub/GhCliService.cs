using System.Text.RegularExpressions;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.Process;

namespace ProjectArrange.Infrastructure.GitHub;

public sealed class GhCliService(IProcessRunner runner, IToolLocator tools) : IGhCliService
{
    public async Task<Result<GhAuthStatus>> GetAuthStatusAsync(CancellationToken cancellationToken = default)
    {
        var version = await runner.RunAsync(new ProcessSpec(tools.Resolve("gh"), "--version", TimeoutMilliseconds: 5_000), cancellationToken);
        if (version.ExitCode != 0)
        {
            return Result<GhAuthStatus>.Ok(new GhAuthStatus(
                IsInstalled: false,
                IsLoggedIn: false,
                User: null,
                RawOutput: version.CombinedOutput));
        }

        var res = await runner.RunAsync(new ProcessSpec(tools.Resolve("gh"), "auth status -h github.com", TimeoutMilliseconds: 10_000), cancellationToken);
        var raw = res.CombinedOutput;

        if (res.ExitCode != 0)
        {
            return Result<GhAuthStatus>.Ok(new GhAuthStatus(
                IsInstalled: true,
                IsLoggedIn: false,
                User: null,
                RawOutput: raw));
        }

        var user = TryExtractUser(raw);
        var isLoggedIn = raw.Contains("Logged in to github.com", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Logged in", StringComparison.OrdinalIgnoreCase);

        return Result<GhAuthStatus>.Ok(new GhAuthStatus(
            IsInstalled: true,
            IsLoggedIn: isLoggedIn,
            User: user,
            RawOutput: raw));
    }

    private static string? TryExtractUser(string raw)
    {
        var m = Regex.Match(raw, @"account\s+(\S+)", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value.Trim();
        return null;
    }
}
