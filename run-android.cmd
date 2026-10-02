@echo off
:: Deploys the app to the Android emulator (PawStash_Phone), starting it if needed. Start the server first with run-api.cmd.
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
set "ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk"
for /d %%J in ("%USERPROFILE%\.jdks\jdk-17*") do set "JAVA_HOME=%%~fJ"
set "ADB=%ANDROID_HOME%\platform-tools\adb.exe"

"%ADB%" get-state >nul 2>&1 || start "" "%ANDROID_HOME%\emulator\emulator.exe" -avd PawStash_Phone
"%ADB%" wait-for-device
:wait_for_boot
for /f "delims=" %%B in ('call "%ADB%" shell getprop sys.boot_completed 2^>nul') do if "%%B"=="1" goto booted
ping -n 3 127.0.0.1 >nul
goto wait_for_boot
:booted

"%DOTNET_ROOT%\dotnet.exe" build "%~dp0pawstash-app\PawStash.App\PawStash.App.csproj" -t:Run -f net10.0-android "-p:AndroidSdkDirectory=%ANDROID_HOME%" "-p:JavaSdkDirectory=%JAVA_HOME%" %*
