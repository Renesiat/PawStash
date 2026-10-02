@echo off
:: Launches the Windows app. Start the server first with run-api.cmd.
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
"%DOTNET_ROOT%\dotnet.exe" run --project "%~dp0pawstash-app\PawStash.App\PawStash.App.csproj" -f net10.0-windows10.0.19041.0 %*
