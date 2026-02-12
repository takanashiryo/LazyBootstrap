@echo off
chcp 65001 >nul
setlocal

set "ROOT=%~dp0"
set "BUILD_DIR=%ROOT%build"

echo ========================================
echo  LazyBootstrap Build Script
echo ========================================
echo.

:: 清理旧构建
if exist "%BUILD_DIR%" (
    echo 清理旧构建目录...
    rmdir /s /q "%BUILD_DIR%"
)
mkdir "%BUILD_DIR%"
mkdir "%BUILD_DIR%\launcher"

:: 发布启动器
echo 编译启动器 (Native AOT)...
dotnet publish "%ROOT%LazyBootstrap.Launcher\LazyBootstrap.Launcher.csproj" -c Release -o "%BUILD_DIR%\_tmp_launcher" --nologo -v q
if errorlevel 1 (
    echo [错误] 启动器编译失败！
    pause
    exit /b 1
)

:: 发布主程序
echo 编译主程序 (Native AOT)...
dotnet publish "%ROOT%LazyBootstrap\LazyBootstrap.csproj" -c Release -o "%BUILD_DIR%\_tmp_main" --nologo -v q
if errorlevel 1 (
    echo [错误] 主程序编译失败！
    pause
    exit /b 1
)

:: 布局文件
echo 布局输出文件...

:: 启动器 exe → 根目录
copy "%BUILD_DIR%\_tmp_launcher\LazyBootstrap.exe" "%BUILD_DIR%\LazyBootstrap.exe" >nul

:: 主程序文件 → launcher 子目录（排除 pdb）
for %%f in ("%BUILD_DIR%\_tmp_main\*.*") do (
    if /i not "%%~xf"==".pdb" (
        copy "%%f" "%BUILD_DIR%\launcher\" >nul
    )
)

:: 清理临时目录
rmdir /s /q "%BUILD_DIR%\_tmp_launcher"
rmdir /s /q "%BUILD_DIR%\_tmp_main"

echo.
echo ========================================
echo  构建完成！
echo ========================================
echo.
echo  输出目录: %BUILD_DIR%
echo.
echo  build\
echo    LazyBootstrap.exe        (启动器)
echo    launcher\
for %%f in ("%BUILD_DIR%\launcher\*.*") do (
    echo      %%~nxf
)
echo.

:: 显示文件大小
for %%f in ("%BUILD_DIR%\LazyBootstrap.exe") do echo  启动器大小: %%~zf bytes
for %%f in ("%BUILD_DIR%\launcher\LazyBootstrap.exe") do echo  主程序大小: %%~zf bytes
echo.
pause
