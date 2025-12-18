@echo off

set ROOT=%~dp0

cd %ROOT%
msbuild LazyBootstrap.sln /t:Clean
msbuild LazyBootstrap.sln /p:Configuration=Debug /p:Platform="Any CPU"

pause