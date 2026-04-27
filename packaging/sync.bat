@echo off
chcp 65001 >nul
setlocal EnableExtensions

if not defined KFC_SYNC_FROM_LAUNCHER (
    echo Do not run sync.bat directly. Use LazyBootstrap and MediaUpdater.
    exit /b 1
)

if not defined LAZY_KFC_UPDATE_GAME_PATH (
    echo Missing LAZY_KFC_UPDATE_GAME_PATH.
    exit /b 1
)

set "gamePath=%LAZY_KFC_UPDATE_GAME_PATH%"
set "updaterLog=%gamePath%\updater_log.txt"
set "sourcePath=%~dp0source"

robocopy "%sourcePath%" "%gamePath%" /E /V /LOG:"%updaterLog%" /TEE /IS /IT
if errorlevel 8 exit /b 1

if exist "%sourcePath%\contents\data_mods\omnimix" (
    robocopy "%sourcePath%\contents\data_mods\omnimix" "%gamePath%\contents\data_mods\omnimix" /E /V /LOG+:"%updaterLog%" /MIR /IS /IT
    if errorlevel 8 exit /b 1
)

exit /b 0
