$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $workspace
try {
    dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --no-restore --nologo -p:OutputPath=bin/WorkflowVerification/
    if($LASTEXITCODE -ne 0){throw 'Desktop build failed.'}
    dotnet test Test/TradeVault.Tests/TradeVault.Tests.csproj --no-restore --nologo -p:OutputPath=bin/WorkflowVerification/
    if($LASTEXITCODE -ne 0){throw 'Backend/workflow tests failed.'}
    dotnet Src/TradeVault.Desktop/bin/WorkflowVerification/TradeVault.Desktop.dll --self-test
    if($LASTEXITCODE -ne 0){throw 'Desktop self-test failed.'}
    dotnet Src/TradeVault.Desktop/bin/WorkflowVerification/TradeVault.Desktop.dll --qa-report Docs/Testing/2026-10-27/Implementation
    if($LASTEXITCODE -ne 0){throw 'Acceptance checks failed.'}
} finally {Pop-Location}
