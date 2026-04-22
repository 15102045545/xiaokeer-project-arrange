namespace ProjectArrange.Infrastructure.Configuration;

public static class AppPaths
{
    public static string GetAppDataRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("PROJECTARRANGE_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot)) return overrideRoot;

        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidate = Path.Combine(baseDir, "ProjectArrange");
        try
        {
            Directory.CreateDirectory(candidate);
            return candidate;
        }
        catch
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "data");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public static string GetSecretsPath() => Path.Combine(GetAppDataRoot(), "secrets.bin");
    public static string GetDatabasePath() => Path.Combine(GetAppDataRoot(), "projectarrange.db");
    public static string GetLogsPath() => Path.Combine(GetAppDataRoot(), "logs");
    public static string GetMigrationsRoot() => Path.Combine(AppContext.BaseDirectory, "migrations", "sqlite");
    public static string GetPromptsRoot() => Path.Combine(AppContext.BaseDirectory, "prompts");
}
