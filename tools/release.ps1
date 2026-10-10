<#
  Publishes a GitHub release: sets <Version> in the csproj, commits it, builds the
  release zip, tags v<Version>, pushes and creates the release with the zip attached.
  Usage: tools\release.bat 0.4.0 [-Draft]
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$Draft
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$csproj = Join-Path $root 'InventoryTweaks.csproj'
Set-Location $root

function Fail([string]$message) {
    Write-Host "ERROR: $message" -ForegroundColor Red
    exit 1
}

# Not named "Git": function names are case-insensitive and would shadow git itself
function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { Fail "git $args failed" }
}

$Version = $Version.TrimStart('v')
if ($Version -notmatch '^\d+\.\d+\.\d+$') { Fail "Version must look like 1.2.3, got '$Version'" }
$tag = "v$Version"

# Preconditions: tools, clean master, new tag
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail 'GitHub CLI (gh) not found. Install: winget install GitHub.cli' }
& gh auth status *> $null
if ($LASTEXITCODE -ne 0) { Fail 'gh is not logged in. Run: gh auth login' }

$branch = (& git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne 'master') { Fail "Releases are made from master (current: $branch)" }
if (& git status --porcelain) { Fail 'Working tree has uncommitted changes. Commit or stash them first.' }
if (& git tag --list $tag) { Fail "Tag $tag already exists" }

# Version -> csproj (Plugin.Version is generated from it at build time)
$content = [IO.File]::ReadAllText($csproj)
$updated = [regex]::Replace($content, '<Version>[^<]*</Version>', "<Version>$Version</Version>")
if ($updated -ne $content) {
    [IO.File]::WriteAllText($csproj, $updated, (New-Object Text.UTF8Encoding($false)))
}

# Build the zip before committing anything
& dotnet build $csproj -c Release -t:Package
if ($LASTEXITCODE -ne 0) {
    Invoke-Git checkout -- $csproj
    Fail 'Build failed; csproj version reverted'
}
$zip = Join-Path $root "dist\InventoryTweaks-$Version.zip"
if (-not (Test-Path $zip)) { Fail "$zip not found" }

if ($updated -ne $content) {
    Invoke-Git commit -m "Release $tag" -- $csproj
}
Invoke-Git tag -a $tag -m "Inventory Tweaks $Version"
Invoke-Git push origin master
Invoke-Git push origin $tag

$ghArgs = @('release', 'create', $tag, $zip, '--title', "Inventory Tweaks $Version", '--generate-notes', '--verify-tag')
if ($Draft) { $ghArgs += '--draft' }
& gh @ghArgs
if ($LASTEXITCODE -ne 0) { Fail "gh release create failed (tag $tag is already pushed; retry: gh $($ghArgs -join ' '))" }

Write-Host "Released $tag" -ForegroundColor Green
