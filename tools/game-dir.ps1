# Prints GameDir from Config.Build.user.props (the same file the build uses); nothing if missing
$props = Join-Path $PSScriptRoot '..\Config.Build.user.props'
if (Test-Path $props) {
    "$(([xml](Get-Content $props)).Project.PropertyGroup.GameDir)".Trim()
}
