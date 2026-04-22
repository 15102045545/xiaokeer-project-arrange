using System.Text.Json;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.Infrastructure.Configuration;

public sealed class UserConfigStore : IUserConfigStore
{
    public string UserConfigPath { get; } = Path.Combine(AppPaths.GetAppDataRoot(), "appsettings.user.json");

    public async Task<Result<string>> ReadUserConfigJsonAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(UserConfigPath)) return Result<string>.Ok("{\n}\n");
            var json = await File.ReadAllTextAsync(UserConfigPath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json)) return Result<string>.Ok("{\n}\n");
            JsonDocument.Parse(json);
            return Result<string>.Ok(json);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex.Message);
        }
    }

    public async Task<Result> WriteUserConfigJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        try
        {
            JsonDocument.Parse(json);

            Directory.CreateDirectory(Path.GetDirectoryName(UserConfigPath)!);
            var tmp = UserConfigPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tmp, json, cancellationToken);
            File.Move(tmp, UserConfigPath, overwrite: true);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}

