@echo off
rem cl: the single dictable entry point of the repository ("cl check", "cl fast"...).
rem Builds the orchestrator (build\Build.csproj, Bullseye) and runs it with the given verbs.
rem Run "cl" with no verb to list them. Same behaviour as cl.ps1.
rem Keep this file ASCII: cmd.exe reads it with the OEM code page.
setlocal
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1"
set "TESTINGPLATFORM_TELEMETRY_OPTOUT=1"
dotnet msbuild "%~dp0build\Build.csproj" -restore -nologo -verbosity:quiet -nodeReuse:false
if errorlevel 1 (
  echo cl: no se pudo compilar build\Build.csproj; revisa los errores de arriba
  exit /b 1
)
dotnet run --project "%~dp0build\Build.csproj" --no-build --no-launch-profile -- %*
exit /b %ERRORLEVEL%
