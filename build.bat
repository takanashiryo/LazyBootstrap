@echo off
setlocal

set "ROOT=%~dp0"
set "BUILD_DIR=%ROOT%build"
set "LAUNCHER_PUBLISH=%ROOT%LazyBootstrap.Launcher\bin\Release\net10.0\win-x64\publish"
set "MAIN_PUBLISH=%ROOT%LazyBootstrap\bin\Release\net10.0\win-x64\publish"

if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%" || exit /b 1
mkdir "%BUILD_DIR%\launcher" || exit /b 1

dotnet publish "%ROOT%LazyBootstrap.Launcher\LazyBootstrap.Launcher.csproj" -c Release -r win-x64
if errorlevel 1 exit /b 1

dotnet publish "%ROOT%LazyBootstrap\LazyBootstrap.csproj" -c Release -r win-x64
if errorlevel 1 exit /b 1

if not exist "%LAUNCHER_PUBLISH%" exit /b 1
if not exist "%MAIN_PUBLISH%" exit /b 1

copy /y "%LAUNCHER_PUBLISH%\LazyBootstrap.exe" "%BUILD_DIR%\LazyBootstrap.exe" >nul || exit /b 1

for %%f in ("%MAIN_PUBLISH%\*.*") do (
    if /i not "%%~xf"==".pdb" copy /y "%%f" "%BUILD_DIR%\launcher\" >nul
)

echo Build completed: "%BUILD_DIR%"
pause
