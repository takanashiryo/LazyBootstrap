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
set "sourceLauncherPath=%sourcePath%\launcher"
set "targetLauncherPath=%gamePath%\launcher"

if exist "%sourceLauncherPath%\" (
    if exist "%targetLauncherPath%\" (
        for %%F in ("%targetLauncherPath%\*") do (
            if /I not "%%~nxF"=="MediaUpdater.exe" if /I not "%%~nxF"=="config.toml" (
                del /F /Q "%%~fF"
                if errorlevel 1 exit /b 1
            )
        )

        for /D %%D in ("%targetLauncherPath%\*") do (
            rmdir /S /Q "%%~fD"
            if errorlevel 1 exit /b 1
        )
    )

    robocopy "%sourcePath%" "%gamePath%" /E /V /LOG:"%updaterLog%" /TEE /IS /IT /XF MediaUpdater.exe MediaUpdater.exe.pending config.toml
    if errorlevel 8 exit /b 1

    if exist "%sourceLauncherPath%\MediaUpdater.exe" (
        if not exist "%targetLauncherPath%\" (
            mkdir "%targetLauncherPath%"
            if errorlevel 1 exit /b 1
        )

        copy /Y "%sourceLauncherPath%\MediaUpdater.exe" "%targetLauncherPath%\MediaUpdater.exe.pending" >nul
        if errorlevel 1 exit /b 1
        echo MediaUpdater pending update staged: "%targetLauncherPath%\MediaUpdater.exe.pending"
    )
) else (
    robocopy "%sourcePath%" "%gamePath%" /E /V /LOG:"%updaterLog%" /TEE /IS /IT /XF config.toml
    if errorlevel 8 exit /b 1
)

if exist "%sourcePath%\contents\data_mods\omnimix" (
    robocopy "%sourcePath%\contents\data_mods\omnimix" "%gamePath%\contents\data_mods\omnimix" /E /V /LOG+:"%updaterLog%" /MIR /IS /IT
    if errorlevel 8 exit /b 1
)

exit /b 0
