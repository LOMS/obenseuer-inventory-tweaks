@echo off
setlocal

set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Obenseuer"
set "OUT_DIR=%~dp0..\reference\decompiled"

rem dotnet global tools (ilspycmd) live here; add explicitly in case PATH is stale
set "PATH=%PATH%;%USERPROFILE%\.dotnet\tools"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0export-game-code.ps1" -GameDir "%GAME_DIR%" -OutDir "%OUT_DIR%" %*

pause
