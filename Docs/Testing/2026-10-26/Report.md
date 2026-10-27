# TradeVault UI and functional test report

**Build:** UI 2026.10.26.3  
**Scope:** Windows desktop layout, native window chrome, existing worksheet workflows and implemented backend services.  
**Data:** synthetic only. Production company database was not opened by the test harness.

## Outcome

- Desktop acceptance: **39 passed, 0 failed**.
- Backend automated tests: **9 passed, 0 failed, 0 skipped**.
- Existing live authentication/persistence/worksheet self-test: **passed**. Its overlapping assertions are not counted again in the 48 cases above.
- Desktop build: **passed, zero warnings/errors**.
- Actual Windows accessibility inspection: native **Minimize, Maximize and Close** controls present.
- Physical mouse clicks, minimize/maximize/restore transitions and close/discard dialogs: **not verified**. Desktop capture failed, and the user subsequently stopped Computer Use with Escape. No further UI automation was performed.
- **Full original product scope is not release-ready:** missing features are listed below. Passing checks establish the implemented scope, not completion of every requested business workflow.

## What changed

1. Replaced the custom in-content caption with Windows' native resizable title bar, globally through the shared window setup. Login and both roles' workspaces use SingleBorderWindow + CanResize and no custom WindowChrome. System message dialogs retain their native controls. Minimize/maximize/close are OS caption controls rather than decorative glyphs.
2. Added build label UI 2026.10.26.3 to the real window title and footer. A pre-existing running process was observed with title TradeVault | Owner, which predates this label. A running process does not reload updated UI code.
3. Removed the wide sidebar and redundant large header/card stack. Horizontal worksheet navigation, compact commands, a single options strip and a narrow status bar give the worksheet the main workspace.
4. Default rows are 28 px, with a 36 px comfortable option. Reduced excess outer spacing and header padding; column widths now respond to labels rather than uniformly reserving 240 px. Retained wrap for long workbook headings.
5. Kept real blank worksheet rows, input validation, locked calculated cells, date parsing, keyboard shortcuts, filtering and clipboard handling. Disabled Owner report import is explicitly labelled as unimplemented.
6. Added a repeatable acceptance harness with case-by-case results and deterministic synthetic datasets. A separate compile-time UI_ACCEPTANCE build opens only an isolated seeded QA workspace; the normal build still starts with real authentication.

## Isolation and test setup

- Test databases were created under the verification build's TestData/GUID folders, outside its normal company Data folder.
- Synthetic records: 12 invoice-register rows, 12 Account Details rows and 36 Debit/Credit rows. Names use QA Indigo Exports / QA Violet Trading; invoices use QA-INV-001 through QA-INV-012.
- Data includes BL dates, invoice/proforma references, shipping defaults, partial receipt inputs, opening balances and editable terms.
- Synthetic amounts per invoice: original USD 15,000, bank contract USD 10,000, bank received USD 3,000, shipping USD 3,800, difference received USD 1,200.
- Deterministic calculation date: 26 October 2026 in calculation checks.
- Retained isolated acceptance databases are QA evidence only. An initial cleanup attempt encountered a OneDrive-folder permission error; cleanup was removed from the new harness so reporting is not hidden by optional cleanup failure. A subsequent complete run passed.
- No passwords are included in this report, screenshots or README. No real company records were inserted, updated or deleted.
- The old company-data application instance was left running. The interactive QA window was separate and explicitly titled SYNTHETIC DATA ONLY.

## Desktop tests

The full executed 39-case matrix, including expected outcomes and evidence:

[Desktop acceptance cases](desktop-results.md)

[Machine-readable desktop results](desktop-results.json)

Coverage includes:

| Area | Executed coverage | Result |
|---|---|---|
| Authentication | Both roles, wrong password, owner-only rejection, logout invalidation | PASS |
| Persistence | Empty starting datasets, save/reload, reopen, revision conflicts, deletion persistence | PASS |
| Defaults | USD 3,800 shipping, edits retained, 90-day terms, editable terms | PASS |
| Validation | Negative USD, sub-cent amounts, missing required fields, invalid dates | PASS |
| Dates | Day-first/named month parsing, paste conversion, unknown BL, due/reminder/overdue | PASS |
| Account Details | Pending bank, difference after shipping, corrected first running balance, cumulative receipt inputs | PASS |
| Debit/Credit | Opening plus debits minus credits; 36-row ledger ending USD 98,500 | PASS |
| Worksheet | 100 spare rows, promotion on typing, no saved spare rows, filtering/reset, frozen identifiers, resizing | PASS |
| Paste protection | Rectangular input, malformed range rejection, calculated-column atomic rejection, read-only rejection | PASS |
| Native window setup | Login and workspace use native chrome; actual OS accessibility exposes caption controls | PASS for configuration/presence |
| Layout | All three worksheets begin above 190 px at 1000x600 content size | PASS |
| UI command handlers | MainWindow input, Save and Reload round trip | PASS |

USD 98,500 ledger check: opening 100 + 12 x (15,000 - 3,000 - 3,800).

## Backend tests

[Original TRX results](backend-results.trx)

| Test | Result |
|---|---|
| DefaultsAndChangesPersistAcrossReopen | PASS |
| MultiplePartialReceiptsAndOneReceiptAcrossInvoices | PASS |
| OverAllocationRollsBackEntireReceipt | PASS |
| CrossPartyAndDuplicateAllocationsAreRejected | PASS |
| HistoricalBalanceExcludesFutureReceiptsAndInvoices | PASS |
| OverdueStopsWhenFullyPaidAndUnknownBlHasNoDueDate | PASS |
| StaleEditsDuplicatesAndReducingAllocatedAmountsAreRejected | PASS |
| InvalidPrecisionDatesAndNegativeValuesAreRejected | PASS |
| BackupCanBeReopenedAndSecondInstanceIsBlocked | PASS |

These backend checks verify the normalized services. They do not mean the separate worksheet UI has been integrated with receipt allocation or automatic report generation.

## Visual evidence

Generated from actual WPF controls against isolated synthetic data:

- [Invoice register](Screenshots/form.png)
- [Account Details](Screenshots/details.png)
- [Debit/Credit](Screenshots/ledger.png)
- [Operator workspace](Screenshots/operator.png)
- [Login with synthetic username](Screenshots/login.png)
- [Minimum login layout](Screenshots/login-minimum.png)
- [Minimum invoice layout](Screenshots/form-minimum.png)

These are WPF client-area renders. Windows draws the native caption outside the client area, so these PNGs intentionally do not contain OS minimize/maximize/close buttons. They must not be treated as evidence that native controls were clicked.

Actual Windows accessibility observation of the separate live QA window reported:

    window TradeVault QA — SYNTHETIC DATA ONLY | UI 2026.10.26.3
      title bar
        menu bar System
        button Minimize
        button Maximize
        button Close

Desktop capture errors were FrameArrived timed out, window capture timed out, and coordinate input geometry is unavailable. A click could not be established as delivered. Computer Use was then stopped by the user's physical Escape key. Native click/state-transition checks remain pending.

## Unimplemented or not verified

| Feature | Current result / boundary |
|---|---|
| Operator submission export/import, revisions and review | NOT IMPLEMENTED. Owner command is disabled. |
| Excel/PDF export and print layout parity | NOT IMPLEMENTED. No generated business report export claimed. |
| Integrated receipt allocation screen | NOT IMPLEMENTED. Backend allocation tests pass; worksheet receipt totals are manual cumulative inputs. |
| One authoritative dataset feeding both report formats | NOT INTEGRATED. The three worksheets persist independently. No automatic cross-sheet reconciliation claimed. |
| Native caption click/state transitions across login and both roles | PRESENCE VERIFIED; INTERACTIVE ACTIONS NOT VERIFIED. Computer Use stopped. |
| Physical keyboard arrows/Tab, clipboard round-trip through Windows clipboard, drag-resize, date-calendar popup | Code/settings and relevant paste/date paths covered; complete physical interaction acceptance NOT VERIFIED. |
| Delete confirmation, unsaved-discard prompt and sign-out click | Handlers inspected; underlying deletion/logout tested. Actual dialog click paths NOT VERIFIED. |
| Sorting amounts numerically, multi-column sort, performance at large production volumes | NOT VERIFIED. Current sort comparer uses text comparison; numeric ordering is a known limitation. |
| Password change/recovery and database encryption | NOT IMPLEMENTED. Current authentication is not database encryption. |
| USB unplug/recovery, clean-PC self-contained launch, no-host-artefact policy | NOT VERIFIED. Current build requires installed .NET 10 Desktop Runtime. |
| High-DPI, multi-monitor caption/taskbar behavior, screen readers | NOT VERIFIED. Normal/minimum client layout renders are available. |
| README history | Pre-existing unresolved merge markers remain; preserved other developers' text rather than silently resolving history. |

## Reproduction

From the repository root:

    dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --no-restore --nologo -p:OutputPath=bin/UIVerification/
    dotnet Src/TradeVault.Desktop/bin/UIVerification/TradeVault.Desktop.dll --qa-report Docs/Testing/2026-10-26
    dotnet Src/TradeVault.Desktop/bin/UIVerification/TradeVault.Desktop.dll --self-test
    dotnet Src/TradeVault.Desktop/bin/UIVerification/TradeVault.Desktop.dll --render-ui Docs/Testing/2026-10-26/Screenshots
    dotnet test Test/TradeVault.Tests/TradeVault.Tests.csproj --no-restore --nologo -p:OutputPath=bin/UIVerification/ --logger "trx;LogFileName=backend-results.trx" --results-directory Docs/Testing/2026-10-26

For the normal application and its existing company database: save and close the running old copy, then run:

    dotnet run --no-restore --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj

Verify the window title/footer shows UI 2026.10.26.3. Do not use the InteractiveQA executable for company work; it contains only synthetic test data.

## Next acceptance work

1. Verify actual caption clicks, keyboard/clipboard, calendar popup and confirmation dialogs on the user's desktop after interactive testing is resumed.
2. Connect the existing allocation backend and unified report data before claiming complete accounting workflow coverage.
3. Implement and test operator submission handling and report export.
4. Complete USB, clean-PC, DPI and recovery acceptance before release.
