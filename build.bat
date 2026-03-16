@echo off
setlocal

pwsh -NoLogo -ExecutionPolicy Bypass -File "%~dp0build.ps1"
exit /b %errorlevel%
