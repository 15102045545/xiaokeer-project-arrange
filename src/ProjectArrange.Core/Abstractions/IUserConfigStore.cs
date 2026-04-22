namespace ProjectArrange.Core.Abstractions;

public interface IUserConfigStore
{
    string UserConfigPath { get; }
    Task<Result<string>> ReadUserConfigJsonAsync(CancellationToken cancellationToken = default);
    Task<Result> WriteUserConfigJsonAsync(string json, CancellationToken cancellationToken = default);
}

