@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Group-InsighterLogs.ps1" %*
