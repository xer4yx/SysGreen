<#
.SYNOPSIS
    Builds the SysGreen Velopack release (ADR-0009).

.DESCRIPTION
    Publishes the App, Helper, and Agent self-contained (win-x64) into one payload, then runs
    `vpk pack` (Velopack) to produce the installer + delta/full packages + release feed under
    artifacts/releases. Velopack installs per-user and powers in-app self-update (the app reads its
    GitHub Releases via VelopackUpdateService). Version comes from Directory.Build.props (ADR-0015).

    Requires the .NET 10 SDK and the Velopack CLI (`vpk`). If missing, it is installed as a global tool.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "artifacts\publish"
$releaseDir = Join-Path $root "artifacts\releases"

function Invoke-Checked([string]$file, [string[]]$arguments) {
    & $file @arguments
    if ($LASTEXITCODE -ne 0) { throw "$file exited with $LASTEXITCODE" }
}

# --- version (single source of truth: Directory.Build.props) ---
[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = $props.SelectSingleNode("//Version").InnerText.Trim()
Write-Host "SysGreen (Velopack): version $version, $Configuration/$Runtime" -ForegroundColor Cyan

# --- publish payload (self-contained: App, then overlay self-contained Helper + Agent) ---
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
$projects = @(
    "src\SysGreen.App\SysGreen.App.csproj",
    "src\SysGreen.Helper\SysGreen.Helper.csproj",
    "src\SysGreen.Agent\SysGreen.Agent.csproj"
)
foreach ($proj in $projects) {
    Write-Host "publish $proj" -ForegroundColor DarkGray
    Invoke-Checked "dotnet" @(
        "publish", (Join-Path $root $proj),
        "-c", $Configuration, "-r", $Runtime, "--self-contained", "true",
        "-o", $publishDir, "--nologo"
    )
}

# --- ensure the Velopack CLI is available ---
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "installing vpk (Velopack CLI)" -ForegroundColor DarkGray
    Invoke-Checked "dotnet" @("tool", "install", "-g", "vpk")
    $env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"
}

# --- pack the Velopack release (Setup.exe + full/delta packages + releases feed) ---
Invoke-Checked "vpk" @(
    "pack",
    "--packId", "SysGreen",
    "--packTitle", "SysGreen",
    "--packAuthors", "xer4yx and the SysGreen contributors",
    "--packVersion", $version,
    "--packDir", $publishDir,
    "--mainExe", "SysGreen.App.exe",
    "--icon", (Join-Path $root "assets\logo.ico"),
    "--outputDir", $releaseDir
)

Write-Host "OK -> $releaseDir" -ForegroundColor Green
Get-ChildItem $releaseDir | Select-Object Name, Length | Format-Table -AutoSize
