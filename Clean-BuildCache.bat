@echo off
setlocal EnableExtensions

rem Clean build caches (bin and obj) only inside the project folder
set "ROOT=%~dp0"
set "PROJECT_DIR=%ROOT%LazyBootstrap\LazyBootstrap"

if not exist "%PROJECT_DIR%" (
  echo Project directory not found: "%PROJECT_DIR%"
  echo Please ensure the script is placed at the repository root.
  goto :end
)

echo Cleaning build caches under "%PROJECT_DIR%" ...

set "COUNT=0"

for /d /r "%PROJECT_DIR%" %%d in (bin) do (
  if /i "%%~nxd"=="bin" (
    echo Deleting "%%d"
    rmdir /s /q "%%d" 2>nul
    set /a COUNT+=1 >nul 2>&1
  )
)

for /d /r "%PROJECT_DIR%" %%d in (obj) do (
  if /i "%%~nxd"=="obj" (
    echo Deleting "%%d"
    rmdir /s /q "%%d" 2>nul
    set /a COUNT+=1 >nul 2>&1
  )
)

echo.
echo Done. Removed %COUNT% cache folders (bin/obj) inside project.

:end
endlocal
pause
