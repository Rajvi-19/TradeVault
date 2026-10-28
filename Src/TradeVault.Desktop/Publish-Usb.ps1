param([ValidateSet('win-x64','win-arm64')][string]$Runtime='win-x64')
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'TradeVault.Desktop.csproj'
$output=Join-Path $PSScriptRoot "bin/Portable/$Runtime"
# Folder publication avoids single-file runtime extraction of application libraries onto the host.
dotnet publish $project -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DefineConstants=TRACE -o $output
if($LASTEXITCODE -ne 0){throw 'Portable publication failed.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../Docs/Workflow-Guide.md') -Destination (Join-Path $output 'Workflow-Guide.md') -Force
@'
TradeVault portable application
Copy this ENTIRE folder onto the USB. Open TradeVault.Desktop.exe.
Data/, Reports/ and Backups/ are created beside the application on that USB.
Keep separate Owner/Operator folders/drives; do not synchronize live databases.
Save and close before ejecting. A USB backup on the same drive does not protect against loss.
Do not distribute test/QA databases or copy development Data folders into this package.
Login does not encrypt the SQLite database. Confirm confidentiality policy before company use.
Before updating a working USB, close the app and make a verified backup. Preserve its Data and Backups folders.
'@ | Set-Content -LiteralPath (Join-Path $output 'USB-READ-ME.txt') -Encoding utf8
Write-Output "Published runtime-contained folder: $output"
