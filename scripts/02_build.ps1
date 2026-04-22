$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\01_generate_icons.ps1"

dotnet build "$root\src\ProjectArrange.App\ProjectArrange.App.csproj" -c Release
dotnet build "$root\src\ProjectArrange.Cli\ProjectArrange.Cli.csproj" -c Release
dotnet build "$root\tools\ProjectArrange.UpdateFeedServer\ProjectArrange.UpdateFeedServer.csproj" -c Release
dotnet test "$root\tests\ProjectArrange.Tests\ProjectArrange.Tests.csproj" -c Release

