# TradeVault

Offline Windows software for shipment records, USD receivables and party reports, launched independently from Owner and Operator USB drives.

## Project status

Desktop interface prototype implemented in `Src/TradeVault.Desktop/` with synthetic in-memory services and dependency-free UI checks. Database, real authentication, accounting services and file handling are not implemented in this prototype. `Docs/` contains three reference workbooks.

## Documentation rule

Every development work session must update this README before handoff. Record the date, concrete changes, files affected, validation performed, limitations and next action. Distinguish completed work from proposed work. Never put passwords, encryption keys or company transaction details in the work log.

## Confirmed requirements

- Windows application launched from a USB; Owner and Operator have separate drives and independent datasets.
- Fully offline; no server, automatic sharing or database synchronisation.
- Predefined Owner and Operator accounts. Store password hashes rather than plaintext credentials.
- Both roles maintain records and generate Account Details and Debit/Credit reports.
- Owner additionally imports operator-exported report files, with preview, duplicate detection and preserved revisions.
- Imported submissions remain immutable and separate from the owner's editable records; owner edits do not return to the operator.
- Excel-style interface: editable grids, keyboard navigation, copy/paste, filters, frozen identifiers and protected calculated fields.
- USD only. Multiple receipts per invoice and allocations of one receipt across multiple invoices.
- Due date defaults to Malaysia-to-Pakistan BL date plus 90 calendar days; terms are editable per invoice.
- Shipping charge defaults to USD 3,800 and is editable. Its application unit and accounting trigger still need confirmation.
- Company data, reports, temporary business files and backups must stay on USB drives. No company data in application logs on the host disk.
- Application-controlled USB-only storage cannot guarantee absence of Windows paging, crash dumps or other OS artefacts; stronger guarantees require host configuration.

## Proposed technical design

- C#, WPF, a supported self-contained .NET release (planned: .NET 10), SQLite and ClosedXML.
- Layers: Desktop, Application, Domain, Infrastructure and Tests.
- Shared invoice/payment records generate two distinct report views; do not enter the same invoice independently for each report.
- Exact monetary arithmetic using decimal calculations and explicit persistence/rounding rules; proposed storage is integer USD cents pending precision confirmation.
- Database entities: app instance, users, settings, parties, accounts, proforma invoices, invoices, shipment legs, shipping charges, receipts, receipt allocations, adjustments, generated ledger entries, report exports/imports, review notes, audit events and schema migrations.
- Report package format proposed: `.tvreport`, containing structured data, Excel outputs and versioned submission metadata. Encryption and source-verification design remain to be specified.
- Resolve business paths relative to the USB; never depend on its drive letter. Use transactional saves, consistent backups and tested restore/migration procedures.

## Calculation decisions

- Use the same running-difference rule for every row: opening difference balance + cumulative net differences - allocated difference receipts + signed adjustments.
- Proposed net difference: original amount - bank contract amount - applicable shipping deduction. Accounting meaning requires confirmation before implementation.
- Party ledger balance: opening balance + debits - credits.
- Historical reports use an explicit as-of date and only transactions effective on or before it.
- Screen sorting must not change the defined accounting order used for running balances.
- Unknown dates/charges remain distinguishable from confirmed zero values.

## Open decisions before accounting implementation

1. Define original, Indian customs, Pakistan customs and bank contract amounts; identify any duplicate meanings.
2. Confirm shipping is per container, shipment or invoice, and when it reduces a party balance.
3. Confirm receipt components (bank-contract versus difference), unallocated advances and overpayment treatment.
4. Decide due-date handling for multiple final-leg BL dates and whether direct due-date overrides are allowed.
5. Confirm monetary precision, rounding and opening balances.
6. Confirm one-time versus recurring Excel imports and exact export layout requirements.
7. Define package encryption, trusted source identity, credential recovery and backup recovery.
8. Confirm target Windows versions/CPU architectures and acceptable host OS storage behaviour.

## Delivery phases

1. Specification: field dictionary, business rules, schema, sample expected results and UI wireframe.
2. Technical prototype: clean-PC USB launch, spreadsheet grid, database save/reopen and sample export.
3. Core records: authentication, parties, invoices, shipments and shipping defaults.
4. Accounting: receipt allocation, adjustments, postings and calculation tests.
5. Reports: both report views, as-of filtering and Excel export reconciliation.
6. Owner import: package validation, preview, duplicate/revision handling and preserved submissions.
7. Release: historical migration, recovery, upgrades, USB failure tests, user guide and acceptance.

First end-to-end milestone: create one invoice on the Operator USB, allocate a partial receipt, generate both reports and import the exported report on the Owner USB. Unsettled accounting assumptions must be explicit in any prototype.

## Two-person work split

- Developer A: WPF shell, Excel-style grid, entry screens, report layouts, export presentation and owner inbox UI.
- Developer B: schema/migrations, storage services, calculations, receipt allocation, report data, package handling and backup/recovery.
- Joint: agree service contracts and expected calculations first; review each other's changes; integrate a working end-to-end flow regularly. Never maintain separate incompatible schema definitions.

## Work log

### 2026-10-26 — Pulled desktop prototype verification and launch guide

- Inspected Developer A's Desktop project, startup/self-test entry point, mock service composition and integration proposal. `MainWindow` currently constructs `MockPrototypeService`; no backend adapter is connected.
- Verification: built `Src/TradeVault.Desktop/TradeVault.Desktop.csproj` with the installed .NET 10 SDK: 0 warnings, 0 errors. Ran `dotnet Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.dll --self-test`: PASS. These are build and in-memory prototype checks, not an interactive desktop acceptance test.
- Launch from the root with `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`, or open `Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.exe` after building. This development build requires .NET 10 Desktop Runtime and is not a self-contained USB release.
- Manual check: select Operator (demo); inspect Invoices & shipments; edit terms/shipping; use Add invoice row, Paste sample rows, filtering, resizing, keyboard navigation and rectangular copy/paste. Enter invalid shipping text, then Validate / mock save, and confirm errors clear after correction. Calculated fixture values intentionally do not update. Check both read-only reports and receipt summary. Switch to Owner (demo) to inspect Operator Reports and Preview demo submission; verify that navigation is absent for Operator.
- Limitations: saves are memory-only; closing loses mock edits. No real login, database save, receipt posting, Excel export or report-file import is connected. No actual UI session or physical USB test was performed in this verification.
- Repository observation: the current checkout has no root `TradeVault.sln`, common backend build settings, build script or backend integration guide from the earlier foundation. Git reports backend project directories and development caches as untracked. No files were deleted or backend integration attempted during this inspection. Reconcile/track Developer B's foundation before connecting it to Desktop.
- Files changed in this session: `Readme.md` only; build outputs were generated under Desktop bin/obj. Next: manually review the prototype, restore/reconcile backend source control, then implement a Desktop adapter against the shared Application contracts.

### 2026-10-26 — Requirements and planning baseline

- Reviewed all three reference workbooks in `Docs/` during project analysis; identified shared invoice data, partial-payment needs, a first-row running-balance inconsistency and total ranges that could omit later entries.
- Captured user decisions on offline Windows USB operation, roles, owner imports, USD, editable defaults and independent owner edits.
- Inspected the project root, existing empty README and `Src/`/`Test/` placeholders.
- Populated `Readme.md` with the requirements, proposed design, open decisions, phases, team split and documentation rule.
- Validation: inspected existing files; documentation-only change, no application tests applicable. No workbooks were modified.
- Limitation: `rtk git status --short` failed because RTK could not determine its Claude configuration directory; Git state was not verified by that command.
- Next: produce the field dictionary and calculation acceptance examples, then scaffold the solution and USB/grid prototype. Application implementation has not started.

### 2026-10-26 — Developer A: desktop shell and invoice-grid milestone

- Read the existing README and inspected all three reference workbooks, including field labels, report layouts and formulas. No reference files were modified or copied into prototype data.
- Added a standalone .NET 10 WPF project under `Src/TradeVault.Desktop/`. No shared solution, Application, Domain, Infrastructure, database or backend-test files were changed. Used built-in WPF controls only; no commercial or third-party dependencies.
- Files affected: `TradeVault.Desktop.csproj`, `App.xaml`, `App.xaml.cs`, `MainWindow.cs`, `ExcelGrid.cs`, `PrototypeModels.cs`, `MockPrototypeService.cs`, `PrototypeChecks.cs`, `INTEGRATION.md`, local `.gitignore`, and this README.
- Completed shell/navigation with explicit demo Owner/Operator selection. Operator Reports is only available in Owner navigation and guarded in its screen handler. This is a prototype role gate, not authentication/security enforcement.
- Completed invoice/shipment input grid with labelled USD amounts, editable shipping default 3800.00 and terms default 90, invoice/proforma dates, country, both transport legs and containers/BL references. Native WPF Enter/Tab/arrow navigation, extended cell selection, Ctrl+C copying, resizable/reorderable columns, two frozen identifiers and all-column text filtering are configured.
- Implemented Ctrl+V rectangular tab-separated paste from the current cell over visible rows. Paste respects displayed column order, is bounded to existing rows (Add invoice row first), rejects ragged/oversized/out-of-bounds ranges and rejects any range touching calculated columns before changing any cells. Limit: 1,000 rows / 1 MB. Quoted multiline Excel cells, fill handles and spreadsheet formulas are not supported.
- Locked calculated columns have blue backgrounds and explicit calculated headings. The service supplies validation through `INotifyDataErrorInfo`; invalid inputs remain editable, and a textual error summary appears after editing/paste or Validate / mock save. Save is blocked on mock validation errors and explicitly reports memory-only behavior.
- Added receipt allocation layout with service-provided receipt/allocated/unallocated totals, separate read-only Account Details and Debit/Credit snapshots, and an Owner-only synthetic submission preview/history/review-status view. Receipt editing/posting, real report generation/export, file chooser/import and review-status updates remain unimplemented.
- Mock boundary: `IPrototypeService` and all presentation models are Desktop-local. `MockPrototypeService` supplies synthetic fixtures, minimal demonstration validation, defaults and memory-only save results. All calculated/report/receipt totals are fixed fixtures, never UI accounting calculations; they do not refresh after edits. No SQL, password verification, package parsing, database persistence, host-disk settings, application logs or business temporary files were added. The application clipboard action explicitly copies selected cells to the Windows clipboard; clipboard history/sync are controlled by Windows.
- Coordination: `INTEGRATION.md` proposes DTO/adapter boundaries and pending questions for Developer B. No active Developer B session or shared contracts were available; agreement is still required before integration. Do not use Desktop presentation types as shared backend contracts.
- Verification: `dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --nologo` passed with zero warnings/errors. `dotnet Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.dll --self-test` passed. Checks exercise paste planning, malformed/bounds/locked-column rejection, actual grid rectangle application and atomic rejection, mock defaults/errors/recovery, column locking/freezing, report read-only state, Owner navigation gating, and all screen construction/layout/rendering on the WPF STA thread in memory. A frozen-column initialization issue found by the checks was fixed before the passing run.
- Git status checked using a command-local `safe.directory` override because sandbox ownership differs; no global Git configuration was changed. Build outputs are ignored inside Desktop.
- Limitations: no interactive visual/keyboard/OS-clipboard acceptance session, screen-reader/high-DPI checks, removable-drive test, clean-PC test, self-contained publish or production USB release has been performed. In-memory render checks are smoke tests, not a visual QA claim. Current build requires installed .NET 10 Desktop Runtime. Closing loses all mock edits; there is no unsaved-close prompt or undo. Security, persistence, allocation enforcement and accounting must come from Developer B.
- Demonstration: run `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`; select Operator (demo), open Invoices & shipments, click Paste sample rows, edit Shipping to `bad`, then Validate / mock save to see errors; restore `3800.00` and validate again. Select an input cell and Ctrl+V a two-row tab-separated rectangle, or shift-select cells and Ctrl+C. Try pasting across a calculated column to see rejection. Filter rows and scroll horizontally to inspect frozen identifiers. Switch to Owner (demo) for the synthetic Operator Reports preview.
- Next: Developer A/B agree typed asynchronous service contracts, IDs/error mapping and calculated response refresh; connect the adapter; then conduct interactive grid/clipboard/accessibility verification and clean-PC USB deployment testing. Receipt editing, accurate workbook report layouts and real owner import interactions follow backend availability.
