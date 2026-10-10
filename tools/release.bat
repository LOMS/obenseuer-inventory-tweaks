@echo off
setlocal

rem Usage: tools\release.bat 0.4.0 [-Draft]
rem Sets the version, commits, tags, pushes and creates a GitHub release with the zip
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0release.ps1" %*
