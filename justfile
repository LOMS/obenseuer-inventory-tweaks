# Inventory Tweaks — task runner (https://github.com/casey/just). Run `just` for the list.
# Long commands are split with a trailing `\` (just joins the lines into one command).

set windows-shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command"]

# Evaluate variables (i.e. `game_dir`) only when a recipe uses them
set lazy := true

# GameDir from Config.Build.user.props (the same file the build uses); empty if missing
game_dir := `& ./tools/game-dir.ps1`

# Print the game folder from Config.Build.user.props
show-game-dir: _game-dir
    @echo '{{game_dir}}'

# List recipes
default:
    @just --list --unsorted

# Build (Debug by default; `just build Release`)
build config="Debug":
    dotnet build -c {{config}}

# Build and copy the DLL to BepInEx\plugins (the game must be closed)
deploy:
    @if (Get-Process Obenseuer -ErrorAction SilentlyContinue) { \
        Write-Host 'Obenseuer is running. Close the game and deploy again.' -ForegroundColor Red; \
        exit 1 \
    }
    dotnet build -t:Deploy

# Release build + dist\InventoryTweaks-<version>.zip
package:
    dotnet build -c Release -t:Package

# Publish a GitHub release: `just release 0.4.0` or `just release 0.4.0 -Draft`
release version *flags:
    & ./tools/release.ps1 {{version}} {{flags}}

# Decompile the game code into reference\decompiled (`just export -Force` to redo)
export *flags: _game-dir
    @$env:PATH += ";$env:USERPROFILE\.dotnet\tools"; \
    & ./tools/export-game-code.ps1 -GameDir '{{game_dir}}' -OutDir reference/decompiled {{flags}}

# Show the end of the BepInEx log (`just log 200`)
log lines="50": _game-dir
    @Get-Content '{{game_dir}}\BepInEx\LogOutput.log' -Tail {{lines}} -Encoding UTF8

# Fails with a hint when GameDir is not configured
[private]
_game-dir:
    @if (-not '{{game_dir}}') { \
        Write-Host 'GameDir is not set. Copy Config.Build.user.props.template to Config.Build.user.props and set the path.' -ForegroundColor Red; \
        exit 1 \
    }

# Remove build output and release archives
clean:
    dotnet clean -v q
    Remove-Item -Recurse -Force dist -ErrorAction SilentlyContinue
