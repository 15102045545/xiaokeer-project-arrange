$ErrorActionPreference = "Stop"

param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$SelfContained = $true
)

$root = Split-Path -Parent $PSScriptRoot

[xml]$props = Get-Content -Raw -Path (Join-Path $root "Directory.Build.props")
$version = $props.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { $version = "0.0.0" }

& "$PSScriptRoot\01_generate_icons.ps1"

$dist = Join-Path $root "dist\$version"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$sc = $SelfContained.IsPresent
$scArg = $sc ? "true" : "false"

dotnet publish "$root\src\ProjectArrange.App\ProjectArrange.App.csproj" -c $Configuration -r $Runtime `
  /p:SelfContained=$scArg /p:PublishSingleFile=true /p:PublishTrimmed=false /p:DebugType=none `
  -o (Join-Path $dist "app-$Runtime")

dotnet publish "$root\src\ProjectArrange.Cli\ProjectArrange.Cli.csproj" -c $Configuration -r $Runtime `
  /p:SelfContained=$scArg /p:PublishSingleFile=true /p:PublishTrimmed=false /p:DebugType=none `
  -o (Join-Path $dist "cli-$Runtime")

dotnet publish "$root\tools\ProjectArrange.UpdateFeedServer\ProjectArrange.UpdateFeedServer.csproj" -c $Configuration -r $Runtime `
  /p:SelfContained=$scArg /p:PublishSingleFile=true /p:PublishTrimmed=false /p:DebugType=none `
  -o (Join-Path $dist "updatefeedserver-$Runtime")

Write-Host "Published to: $dist"

