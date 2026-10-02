@echo off
:: Starts PostgreSQL (Docker) and the API on http://localhost:5094. Migrations apply on startup.
setlocal
set "DOTNET_ROOT=%USERPROFILE%\.dotnet"
docker compose -f "%~dp0docker-compose.yml" up -d || exit /b 1
"%DOTNET_ROOT%\dotnet.exe" run --project "%~dp0pawstash-api\PawStash.API" --launch-profile http %*
