<#
.SYNOPSIS
    Builds the SysGreen Windows installer (ADR-0009).

.DESCRIPTION
    Publishes the App, Helper, and Agent self-contained (win-x64) into one payload folder so the
    installed app needs no .NET runtime pre-installed, then compiles installer/SysGreen.iss with
    Inno Setup (ISCC) into a per-machine Setup.exe. The version is read from Directory.Build.props
    (the single source of truth, ADR-0015) and injected into the .iss.

    Outputs:
      artifacts/publish    - the self-contained payload
      artifacts/installer  - SysGreen-<version>-setup.exe

    Requires the .NET 10 SDK and Inno Setup 6 (ISCC.exe). On CI the installer.yml workflow installs
    Inno via choco; locally: choco install innosetup  (or https://jrsoftware.org/isdl.php).
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "artifacts\publish"
$iss = Join-Path $PSScriptRoot "SysGreen.iss"

function Invoke-Checked([string]$file, [string[]]$arguments) {
    & $file @arguments
    if ($LASTEXITCODE -ne 0) { throw "$file exited with $LASTEXITCODE" }
}

# --- version (single source of truth: Directory.Build.props) ---
[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = $props.SelectSingleNode("//Version").InnerText.Trim()
Write-Host "SysGreen installer: version $version, $Configuration/$Runtime" -ForegroundColor Cyan

# --- publish payload (self-contained; App last? no: App first, then overlay SCD Helper/Agent) ---
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

# --- locate ISCC ---
$iscc = @(
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source }
if (-not $iscc) {
    throw "Inno Setup (ISCC.exe) not found. Install it: 'choco install innosetup' or https://jrsoftware.org/isdl.php"
}

# --- compile the installer ---
Write-Host "compile $iss" -ForegroundColor DarkGray
Invoke-Checked $iscc @("/DMyAppVersion=$version", "/DPayloadDir=$publishDir", $iss)

$setup = Join-Path $root "artifacts\installer\SysGreen-$version-setup.exe"
Write-Host "OK -> $setup" -ForegroundColor Green
