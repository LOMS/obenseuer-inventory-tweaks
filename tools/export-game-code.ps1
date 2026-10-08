<#
.SYNOPSIS
  Decompiles the game's Assembly-CSharp.dll into a C# project so it can be
  searched and read with normal tools (grep, editor, Claude Code).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\export-game-code.ps1 `
      -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Obenseuer"

.NOTES
  Requires: dotnet tool install -g ilspycmd
  The export is skipped when the DLL has not changed since the last run
  (use -Force to re-export anyway).
#>
param(
    # Game install folder (the one that contains the game .exe)
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    # Where the decompiled project is written
    [string]$OutDir = (Join-Path $PSScriptRoot "..\reference\decompiled"),

    # Re-export even if the DLL hash matches the previous export
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Resolve paths to absolute form so the checks below are predictable
$GameDir = [System.IO.Path]::GetFullPath($GameDir)
$OutDir  = [System.IO.Path]::GetFullPath($OutDir)

# Make sure the decompiler is installed
if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    throw "ilspycmd not found. Install it with: dotnet tool install -g ilspycmd"
}

# Locate <GameName>_Data\Managed without hardcoding the game's folder name
$dataDir = Get-ChildItem -Path $GameDir -Directory -Filter "*_Data" | Select-Object -First 1
if (-not $dataDir) {
    throw "No '*_Data' folder found in '$GameDir'. Check the -GameDir path."
}

$managed = Join-Path $dataDir.FullName "Managed"
$dll     = Join-Path $managed "Assembly-CSharp.dll"
if (-not (Test-Path $dll)) {
    throw "Assembly-CSharp.dll not found at '$dll'."
}

# Skip the export when the DLL is unchanged since the last run
$stampFile = Join-Path $OutDir ".source-hash"
$hash = (Get-FileHash -Path $dll -Algorithm SHA256).Hash

if ((Test-Path $stampFile) -and -not $Force) {
    $previous = (Get-Content $stampFile -TotalCount 1).Trim()
    if ($previous -eq $hash) {
        Write-Host "Assembly-CSharp.dll is unchanged. Nothing to do (use -Force to re-export)."
        exit 0
    }
}

# Safety: only wipe the output folder if we created it earlier (stamp file present)
if (Test-Path $OutDir) {
    $hasContent = $null -ne (Get-ChildItem -Path $OutDir -Force | Select-Object -First 1)
    if ($hasContent -and -not (Test-Path $stampFile)) {
        throw "Output folder '$OutDir' is not empty and was not created by this script. Aborting."
    }
    Get-ChildItem -Path $OutDir -Force | Remove-Item -Recurse -Force
}
else {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

Write-Host "Decompiling $dll"
Write-Host "Output: $OutDir"

# -p: write a full project (one .cs file per type), -r: where to resolve dependencies
& ilspycmd -p -o $OutDir -r $managed $dll
if ($LASTEXITCODE -ne 0) {
    throw "ilspycmd failed with exit code $LASTEXITCODE."
}

# Record which DLL version this export came from
Set-Content -Path $stampFile -Value $hash

$count = (Get-ChildItem -Path $OutDir -Recurse -Filter *.cs | Measure-Object).Count
Write-Host "Done. Exported $count .cs files."