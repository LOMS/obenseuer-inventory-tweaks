@echo off
setlocal

rem The game locks the plugin DLL and only loads plugins at startup
tasklist /FI "IMAGENAME eq Obenseuer.exe" | find /I "Obenseuer.exe" >nul
if not errorlevel 1 (
    echo Obenseuer is running. Close the game and run deploy again.
    pause
    exit /b 1
)

rem Builds first, then copies the DLL and PDB to BepInEx\plugins\InventoryTweaks
dotnet build "%~dp0..\InventoryTweaks.csproj" -t:Deploy %*
