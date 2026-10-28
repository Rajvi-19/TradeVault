param([string]$OutputFolder='Src/TradeVault.Desktop/bin/Debug/net10.0-windows',[ValidateSet('Debug','Release')][string]$Configuration='Debug',[switch]$AuditRunner)
$env:DOTNET_CLI_HOME=Join-Path (Get-Location) .build/cli
$env:APPDATA=Join-Path (Get-Location) .build/appdata
$env:LOCALAPPDATA=Join-Path (Get-Location) .build/local
$env:NUGET_PACKAGES=Join-Path (Get-Location) .build/packages
$env:MSBuildEnableWorkloadResolver='false'
dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --no-restore -p:CopyRetryCount=0 --nologo -c $Configuration -o $OutputFolder
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
dotnet test Test/TradeVault.Tests/TradeVault.Tests.csproj --no-restore --verbosity minimal
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
dotnet (Join-Path $OutputFolder TradeVault.Desktop.dll) --self-test
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
if($AuditRunner){
    dotnet restore Test/TradeVault.Desktop.Audit/TradeVault.Desktop.Audit.csproj --source .build/packages -p:NuGetAudit=false --nologo
    if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
    dotnet build Test/TradeVault.Desktop.Audit/TradeVault.Desktop.Audit.csproj --no-restore --nologo -o .build/client-audit/runner
    if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
    dotnet .build/client-audit/runner/TradeVault.Desktop.Audit.dll .build/client-audit/runner-evidence
}
exit $LASTEXITCODE
