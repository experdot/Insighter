@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Invoke-InsighterAiQuery.ps1" %*
