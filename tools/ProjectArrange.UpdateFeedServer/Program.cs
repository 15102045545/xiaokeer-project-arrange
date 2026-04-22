var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapGet("/", () => Results.Text("ProjectArrange.UpdateFeedServer"));

app.MapGet("/api/update/latest", (IConfiguration cfg) =>
{
    var latest = cfg["UpdateFeed:LatestVersion"] ?? "0.0.0.0";
    var download = cfg["UpdateFeed:DownloadUrl"] ?? "";
    return Results.Json(new
    {
        latestVersion = latest,
        downloadUrl = string.IsNullOrWhiteSpace(download) ? null : download
    });
});

app.Run();
