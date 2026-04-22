using ProjectArrange.Infrastructure.Configuration;

namespace ProjectArrange.Infrastructure.Presence;

public sealed class MachineIdentityProvider
{
    private readonly string _path;

    public MachineIdentityProvider(string? machineIdPath = null)
    {
        _path = machineIdPath ?? Path.Combine(AppPaths.GetAppDataRoot(), "machine_id.txt");
    }

    public string GetMachineId()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (File.Exists(_path))
        {
            var existing = File.ReadAllText(_path).Trim();
            if (!string.IsNullOrWhiteSpace(existing)) return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(_path, id);
        return id;
    }

    public string GetMachineName() => Environment.MachineName;
}
