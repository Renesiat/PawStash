@echo off
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0pawstash-api\PawStash.API" || exit /b 1
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0pawstash-app\PawStash.App\PawStash.App.csproj" -f net10.0-windows10.0.19041.0 %*
