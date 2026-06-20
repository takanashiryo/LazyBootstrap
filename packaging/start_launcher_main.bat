@echo off
setlocal EnableExtensions

pushd "%~dp0" >nul 2>nul
if errorlevel 1 (
    echo Failed to enter the script directory.
    exit /b 1
)

set "launcherDir=%CD%\launcher"
set "targetExe=%launcherDir%\LazyBootstrap.exe"

if not exist "%targetExe%" (
    echo Main executable was not found: %targetExe%
    popd
    exit /b 1
)

start "" /D "%launcherDir%" "%targetExe%" %*
set "startExitCode=%ERRORLEVEL%"

popd
exit /b %startExitCode%
