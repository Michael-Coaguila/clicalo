#Requires -Version 5.1
<#
.SYNOPSIS
    cl: the single dictable entry point of the repository ("cl check", "cl fast"...).
.DESCRIPTION
    Builds the orchestrator (build/Build.csproj, Bullseye) and runs it with the given verbs.
    Run "cl" with no verb to list them. Same behaviour as cl.cmd.
    Keep this file ASCII: Windows PowerShell 5.1 reads scripts without a BOM with the ANSI code page.
#>
$ErrorActionPreference = 'Stop'

$variables = [ordered]@{
    DOTNET_NOLOGO                             = '1'
    DOTNET_CLI_TELEMETRY_OPTOUT               = '1'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
    TESTINGPLATFORM_TELEMETRY_OPTOUT          = '1'
}
$saved = @{}
foreach ($name in $variables.Keys) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $variables[$name], 'Process')
}

$exitCode = 1
try {
    $project = Join-Path $PSScriptRoot 'build/Build.csproj'
    & dotnet msbuild $project -restore -nologo -verbosity:quiet -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        Write-Output 'cl: no se pudo compilar build\Build.csproj; revisa los errores de arriba'
    }
    else {
        & dotnet run --project $project --no-build --no-launch-profile -- @args
        $exitCode = $LASTEXITCODE
    }
}
finally {
    # The variables only matter to this run; leave the caller's session as it was.
    foreach ($name in $saved.Keys) {
        [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
    }
}
exit $exitCode
