# USB deployment — 2 November 2026

Build: UI 2026.11.02.3, Release, self-contained Windows x64, .NET 10 runtime included.

Destination: E:\TradeVault
Executable: E:\TradeVault\TradeVault.Desktop.exe

Keep the complete TradeVault folder on the USB. Start the EXE directly. No installer, bootable USB or separate .NET installation is used. Data/tradevault.db beside the executable contains company records; exports default to Exports beside it. The deployment starts with an empty company database and includes no demo/test records. Close the app before ejecting the USB. During future updates preserve the existing Data folder.

Rebuild with:

    dotnet publish Src/TradeVault.Desktop/TradeVault.Desktop.csproj -p:PublishProfile=UsbPortable -o Release/TradeVault-USB-20261102

Self-contained folder publishing deliberately keeps runtime/native DLLs alongside the executable rather than bundling them for extraction to host temporary storage. This folder must be copied as a unit; the EXE alone is insufficient.

Release source: Release/TradeVault-USB-20261102. START HERE.txt provides user instructions; SHA256SUMS.txt lists immutable program-file hashes. No source-code checkout, prior company backups, exports or synthetic test databases are copied into this deployment.

Local checks: native EXE empty-database initialization exit code 0, coreclr.dll/SQLite present, runtimeconfig includes bundled .NET and Windows Desktop 10.0.14, empty data tables and SQLite integrity ok. USB copy/startup verification results are recorded in the README deployment session. Running on a second Windows machine remains a separate physical acceptance check.
