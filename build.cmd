@echo off
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0src\PawStash.Api" || exit /b 1
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0src\PawStash.App\PawStash.csproj" -f net10.0-windows10.0.19041.0 %*
