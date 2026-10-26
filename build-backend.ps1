param([switch]$NoRestore)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    # Build-time caches only. The deployed application does not use this script.
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.build/cli'
    $env:APPDATA = Join-Path $PSScriptRoot '.build/appdata'
    $env:LOCALAPPDATA = Join-Path $PSScriptRoot '.build/local'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.build/packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:MSBuildEnableWorkloadResolver = 'false'
    if (-not $NoRestore) {
        dotnet restore TradeVault.sln --configfile NuGet.Config --verbosity minimal
        if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    }
    dotnet test TradeVault.sln --no-restore --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Backend build or tests failed.' }
} finally {
    Pop-Location
}
