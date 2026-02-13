@echo off
setlocal

set "ROOT=%~dp0"
set "BUILD_DIR=%ROOT%build"
set "TMP_LAUNCHER=%BUILD_DIR%\_tmp_launcher"
set "TMP_MAIN=%BUILD_DIR%\_tmp_main"

if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
mkdir "%BUILD_DIR%" || exit /b 1
mkdir "%BUILD_DIR%\launcher" || exit /b 1

dotnet publish "%ROOT%LazyBootstrap.Launcher\LazyBootstrap.Launcher.csproj" -c Release -o "%TMP_LAUNCHER%" --nologo -v minimal
if errorlevel 1 exit /b 1

dotnet publish "%ROOT%LazyBootstrap\LazyBootstrap.csproj" -c Release -o "%TMP_MAIN%" --nologo -v minimal
if errorlevel 1 exit /b 1

copy /y "%TMP_LAUNCHER%\LazyBootstrap.exe" "%BUILD_DIR%\LazyBootstrap.exe" >nul || exit /b 1

for %%f in ("%TMP_MAIN%\*.*") do (
    if /i not "%%~xf"==".pdb" copy /y "%%f" "%BUILD_DIR%\launcher\" >nul
)

rmdir /s /q "%TMP_LAUNCHER%"
rmdir /s /q "%TMP_MAIN%"

echo Build completed: "%BUILD_DIR%"
pause
