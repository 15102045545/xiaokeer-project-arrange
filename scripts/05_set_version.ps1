$ErrorActionPreference = "Stop"

param(
    [Parameter(Mandatory=$true)][string]$Version
)

$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root "Directory.Build.props"

if (-not (Test-Path $propsPath)) { throw "Missing: $propsPath" }

[xml]$xml = Get-Content -Raw -Path $propsPath
$pg = $xml.Project.PropertyGroup
if ($null -eq $pg) {
    $pg = $xml.CreateElement("PropertyGroup")
    $xml.Project.AppendChild($pg) | Out-Null
}

$versionNode = $pg.Version
if ($null -eq $versionNode) {
    $versionNode = $xml.CreateElement("Version")
    $pg.AppendChild($versionNode) | Out-Null
}
$versionNode.InnerText = $Version

$parts = $Version.Split(".")
while ($parts.Length -lt 3) { $parts += "0" }
$asm = "$($parts[0]).$($parts[1]).$($parts[2]).0"

foreach ($name in @("AssemblyVersion","FileVersion")) {
    $node = $pg.$name
    if ($null -eq $node) {
        $node = $xml.CreateElement($name)
        $pg.AppendChild($node) | Out-Null
    }
    $node.InnerText = $asm
}

$xml.Save($propsPath)
Write-Host "Updated version: $Version"

