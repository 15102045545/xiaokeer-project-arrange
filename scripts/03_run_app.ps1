$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
& "$PSScriptRoot\01_generate_icons.ps1"

dotnet run --project "$root\src\ProjectArrange.App\ProjectArrange.App.csproj" -c Release

