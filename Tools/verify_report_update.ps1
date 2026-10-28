param([string]$OutputFolder='Src/TradeVault.Desktop/bin/Debug/net10.0-windows')
$env:DOTNET_CLI_HOME=Join-Path (Get-Location) .build/cli
$env:APPDATA=Join-Path (Get-Location) .build/appdata
$env:LOCALAPPDATA=Join-Path (Get-Location) .build/local
$env:NUGET_PACKAGES=Join-Path (Get-Location) .build/packages
$env:MSBuildEnableWorkloadResolver='false'
dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --no-restore -p:CopyRetryCount=0 --nologo -o $OutputFolder
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
dotnet test Test/TradeVault.Tests/TradeVault.Tests.csproj --no-restore --verbosity minimal
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
dotnet (Join-Path $OutputFolder TradeVault.Desktop.dll) --self-test
