$ErrorActionPreference = "Stop"

param(
    [string]$Runtime = "win-x64"
)

$root = Split-Path -Parent $PSScriptRoot

[xml]$props = Get-Content -Raw -Path (Join-Path $root "Directory.Build.props")
$version = $props.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { $version = "0.0.0" }

$dist = Join-Path $root "dist\$version"
$src = Join-Path $dist "app-$Runtime"
if (-not (Test-Path $src)) { throw "Missing publish output: $src (run scripts/06_publish.ps1 first)" }

$zip = Join-Path $dist "ProjectArrange-$version-$Runtime.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $src "*") -DestinationPath $zip

Write-Host "Created: $zip"

