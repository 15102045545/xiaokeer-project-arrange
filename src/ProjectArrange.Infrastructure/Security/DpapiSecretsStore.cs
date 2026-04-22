using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;
using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.Infrastructure.Security;

public sealed class DpapiSecretsStore : IAppSecretsStore
{
    private sealed record SecretsPayload(string? GitHubToken);

    public Task<Result> SaveGitHubTokenAsync(string token, CancellationToken cancellationToken = default) =>
        WriteAsync(new SecretsPayload(token), cancellationToken);

    public async Task<Result<string?>> GetGitHubTokenAsync(CancellationToken cancellationToken = default)
    {
        var read = await ReadAsync(cancellationToken);
        if (!read.IsSuccess) return Result<string?>.Fail(read.Error!);
        return Result<string?>.Ok(read.Value!.GitHubToken);
    }

    public Task<Result> ClearGitHubTokenAsync(CancellationToken cancellationToken = default) =>
        WriteAsync(new SecretsPayload(null), cancellationToken);

    private static async Task<Result<SecretsPayload>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var path = AppPaths.GetSecretsPath();
            if (!File.Exists(path)) return Result<SecretsPayload>.Ok(new SecretsPayload(null));

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var unprotected = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(unprotected);
            var payload = JsonSerializer.Deserialize<SecretsPayload>(json) ?? new SecretsPayload(null);
            return Result<SecretsPayload>.Ok(payload);
        }
        catch (Exception ex)
        {
            return Result<SecretsPayload>.Fail(ex.Message);
        }
    }

    private static async Task<Result> WriteAsync(SecretsPayload payload, CancellationToken cancellationToken)
    {
        try
        {
            var root = AppPaths.GetAppDataRoot();
            Directory.CreateDirectory(root);

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(AppPaths.GetSecretsPath(), protectedBytes, cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
