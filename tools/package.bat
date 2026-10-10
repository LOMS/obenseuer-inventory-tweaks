@echo off
setlocal

rem Release build + dist\InventoryTweaks-<version>.zip (plugins\InventoryTweaks\InventoryTweaks.dll)
dotnet build "%~dp0..\InventoryTweaks.csproj" -c Release -t:Package %*
