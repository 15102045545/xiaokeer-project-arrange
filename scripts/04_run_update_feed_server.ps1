$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$proj = "$root\tools\ProjectArrange.UpdateFeedServer\ProjectArrange.UpdateFeedServer.csproj"

$env:ASPNETCORE_URLS = "http://0.0.0.0:5123"
dotnet run --project $proj -c Release

