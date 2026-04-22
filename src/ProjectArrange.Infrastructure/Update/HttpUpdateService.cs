using System.Reflection;
using System.Text.Json;
using ProjectArrange.Core;
using ProjectArrange.Core.Abstractions;

namespace ProjectArrange.Infrastructure.Update;

public sealed class HttpUpdateService(HttpClient httpClient, Uri feedBaseUri) : IUpdateService
{
    public async Task<Result<UpdateInfo>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";
            var url = new Uri(feedBaseUri, "/api/update/latest");

            var json = await httpClient.GetStringAsync(url, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            var latest = doc.RootElement.GetProperty("latestVersion").GetString() ?? "0.0.0.0";
            var download = doc.RootElement.TryGetProperty("downloadUrl", out var du) ? du.GetString() : null;

            Uri? downloadUri = null;
            if (!string.IsNullOrWhiteSpace(download))
            {
                downloadUri = Uri.TryCreate(download, UriKind.Absolute, out var abs)
                    ? abs
                    : new Uri(feedBaseUri, download);
            }

            return Result<UpdateInfo>.Ok(new UpdateInfo(
                CurrentVersion: currentVersion,
                LatestVersion: latest,
                DownloadUrl: downloadUri,
                RawJson: json));
        }
        catch (Exception ex)
        {
            return Result<UpdateInfo>.Fail(ex.Message);
        }
    }
}
