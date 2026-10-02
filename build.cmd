@echo off
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
set "ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk"
for /d %%J in ("%USERPROFILE%\.jdks\jdk-17*") do set "JAVA_HOME=%%~fJ"
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0pawstash-api\PawStash.API" || exit /b 1
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0pawstash-app\PawStash.App\PawStash.App.csproj" -f net10.0-android "-p:AndroidSdkDirectory=%ANDROID_HOME%" "-p:JavaSdkDirectory=%JAVA_HOME%" || exit /b 1
"%DOTNET_ROOT%\dotnet.exe" build "%~dp0pawstash-app\PawStash.App\PawStash.App.csproj" -f net10.0-windows10.0.19041.0 %*
