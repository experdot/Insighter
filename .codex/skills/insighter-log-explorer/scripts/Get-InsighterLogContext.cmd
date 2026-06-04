@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Get-InsighterLogContext.ps1" %*
