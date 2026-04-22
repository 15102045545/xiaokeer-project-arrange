$ErrorActionPreference = "Stop"

param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$root = Split-Path -Parent $PSScriptRoot

[xml]$props = Get-Content -Raw -Path (Join-Path $root "Directory.Build.props")
$version = $props.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { $version = "0.0.0" }

$dist = Join-Path $root "dist\$version"
$sourceDir = Join-Path $dist "app-$Runtime"
if (-not (Test-Path $sourceDir)) {
    & "$PSScriptRoot\06_publish.ps1" -Runtime $Runtime -Configuration $Configuration
}

$iss = Join-Path $root "installer\inno\ProjectArrange.iss"
if (-not (Test-Path $iss)) { throw "Missing: $iss" }

$candidates = @(
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup not found. Install Inno Setup 6, then rerun."
}

$outputDir = Join-Path $dist "installer"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

& $iscc $iss "/DMyAppVersion=$version" "/DMySourceDir=$sourceDir" "/DMyOutputDir=$outputDir" "/DMyRuntime=$Runtime"

Write-Host "Installer output: $outputDir"

