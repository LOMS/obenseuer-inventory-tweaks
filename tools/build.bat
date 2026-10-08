@echo off
setlocal

dotnet build "%~dp0..\InventoryTweaks.csproj" %*
