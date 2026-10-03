@echo off
:: Runs the automated tests. They create a throwaway database in the pawstash-db container, so it is started first.
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
docker compose -f "%~dp0docker-compose.yml" up -d || exit /b 1
"%DOTNET_ROOT%\dotnet.exe" test "%~dp0pawstash-api\PawStash.Tests" %*
