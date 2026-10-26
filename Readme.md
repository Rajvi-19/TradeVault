# TradeVault

Offline Windows software for shipment records, USD receivables and party reports, launched independently from Owner and Operator USB drives.

## Project status

<<<<<<< Updated upstream
<<<<<<< Updated upstream
Desktop interface prototype implemented in `Src/TradeVault.Desktop/` with synthetic in-memory services and dependency-free UI checks. Database, real authentication, accounting services and file handling are not implemented in this prototype. `Docs/` contains three reference workbooks.
=======
Backend foundation implemented and verified: Domain/Application/Infrastructure projects, SQLite migration, party/invoice services, receipt allocations, bank-contract balance queries, consistent backup and nine passing automated tests. There is no desktop executable or production-ready application yet. Authentication, full accounting reports, Excel exports and owner report import remain future work. Use synthetic data until encryption and security are implemented.

## Backend quick start

- Open `TradeVault.sln` with a .NET 10 SDK available.
- Run `powershell -NoProfile -ExecutionPolicy Bypass -File ./build-backend.ps1` from the project root. Execution-policy bypass applies only to that build process; no machine policy is changed.
- Initial dependency restore needs developer internet access. Subsequent local builds can use `-NoRestore`; deployed software will not need NuGet or internet.
- Read [Developer A integration guide](Docs/Backend-Integration.md) for service composition, field definitions, examples and implementation boundaries.
- SQLite schema source: `Src/TradeVault.Infrastructure/Migrations/001_Initial.sql`. Only this milestone's tables are implemented; the larger entity list below remains the proposed future design.
- Service contracts: `Src/TradeVault.Application/Contracts.cs`. The desktop project should consume these instead of writing its own persistence or financial calculations.
>>>>>>> Stashed changes
=======
Windows desktop interface implemented in `Src/TradeVault.Desktop/`, redesigned around a modern Excel-style workspace. It currently uses synthetic in-memory services while Developer B's integration is pending; authentication, accounting, persistence and file operations are not production-ready. `Docs/` contains three reference workbooks. Earlier work-log entries describe the previous UI and remain preserved as history.
>>>>>>> Stashed changes

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

<<<<<<< Updated upstream
### 2026-10-26 — Pulled desktop prototype verification and launch guide

- Inspected Developer A's Desktop project, startup/self-test entry point, mock service composition and integration proposal. `MainWindow` currently constructs `MockPrototypeService`; no backend adapter is connected.
- Verification: built `Src/TradeVault.Desktop/TradeVault.Desktop.csproj` with the installed .NET 10 SDK: 0 warnings, 0 errors. Ran `dotnet Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.dll --self-test`: PASS. These are build and in-memory prototype checks, not an interactive desktop acceptance test.
- Launch from the root with `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`, or open `Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.exe` after building. This development build requires .NET 10 Desktop Runtime and is not a self-contained USB release.
- Manual check: select Operator (demo); inspect Invoices & shipments; edit terms/shipping; use Add invoice row, Paste sample rows, filtering, resizing, keyboard navigation and rectangular copy/paste. Enter invalid shipping text, then Validate / mock save, and confirm errors clear after correction. Calculated fixture values intentionally do not update. Check both read-only reports and receipt summary. Switch to Owner (demo) to inspect Operator Reports and Preview demo submission; verify that navigation is absent for Operator.
- Limitations: saves are memory-only; closing loses mock edits. No real login, database save, receipt posting, Excel export or report-file import is connected. No actual UI session or physical USB test was performed in this verification.
- Repository observation: the current checkout has no root `TradeVault.sln`, common backend build settings, build script or backend integration guide from the earlier foundation. Git reports backend project directories and development caches as untracked. No files were deleted or backend integration attempted during this inspection. Reconcile/track Developer B's foundation before connecting it to Desktop.
- Files changed in this session: `Readme.md` only; build outputs were generated under Desktop bin/obj. Next: manually review the prototype, restore/reconcile backend source control, then implement a Desktop adapter against the shared Application contracts.
=======
### 2026-10-26 — Developer B backend foundation

- Added `TradeVault.sln`, common build settings, local NuGet configuration, ignored build/data patterns and `build-backend.ps1`.
- Added Domain models for USD, parties, invoices, due dates, receipts, allocations and bank-contract balances. Sub-cent amounts and invalid terms are rejected. Stored shipping defaults to USD 3,800 but is not deducted from balances yet.
- Added UI-facing create/list/update contracts in Application. Invoice updates use version checks to reject stale edits.
- Added Infrastructure with relative-to-application paths, a data-folder instance lock, SQLite foreign keys, DELETE journals, FULL synchronous writes, in-memory SQL temporary storage and transactional schema migration. Native SQLite dependencies are restored through Microsoft.Data.Sqlite 10.0.14.
- Implemented create/list parties; create/update/list invoices; transactional receipt creation with allocations; list receipts; bank-contract balance queries by as-of date; consistent integrity-checked backup.
- Allocation safeguards reject duplicate invoice rows, cross-party allocation, receipt over-allocation, invoice over-allocation and allocations dated before invoices. Unallocated funds remain visible. Invoice amount cannot be reduced below allocated receipts.
- Added `Docs/Backend-Integration.md` with Developer A handoff instructions, actual schema boundaries, field dictionary and synthetic acceptance examples.
- Verification: `rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File ./build-backend.ps1 -NoRestore` built all four projects and passed all 9 tests (0 failed, 0 skipped). Tests exercised persistence/reopen, defaults/edits, partial and split receipts, historical cutoffs, atomic rejection, cross-party/duplicate validation, overdue calculation, stale edits, duplicate invoices, data-folder locking, backup reopen, precision and date boundaries.
- Environment notes: SDK is installed. Initial restore encountered host NuGet configuration access and sandbox network restrictions; redirected development caches/config into `.build/` and successfully restored packages with network-enabled execution. The build script disables automatic ASP.NET certificate generation; the initial SDK first-run output reported certificate setup before that setting was added. No company data was used in builds/tests.
- Limitations: not tested on physical removable media or a clean deployment PC. No login/role enforcement, encryption, full audit before/after values, receipt correction/reallocation, difference/shipping postings, report generation/import, or production restore UI. Historical queries use current invoice values, not temporal invoice versions. Explicit development roots are allowed; production USB enforcement remains to be added. All those items are documented, not claimed complete.
- Next: Developer A connects its grid to the contracts; confirm amount/shipping/receipt-component policies before expanding the accounting model. Then add authentication/security, remaining accounting entities and both report outputs.
>>>>>>> Stashed changes

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

### 2026-10-26 — Developer A: Excel-inspired desktop UI redesign

- User rejected the earlier interface and requested the application UI directly, using the Microsoft Excel / Word 2024 Figma community file as visual direction. Replaced the previous UI rather than adding another mockup. Reference URL: https://www.figma.com/design/2AIgBpbZmpryh2KEhfY0Zr/Microsoft-Excel---Word-2024--Community-?node-id=3-3070 . Exact reference inspection was unavailable: web access failed and an in-app browser attempt timed out. This implementation is Excel-inspired, not a verified reproduction of that Figma frame.
- Files affected: `Src/TradeVault.Desktop/MainWindow.cs`, `App.xaml`, `App.xaml.cs`, `ExcelGrid.cs`, `MockPrototypeService.cs` (display strings only), and this README. Shared projects/contracts and other developers' files/entries were not changed.
- Rebuilt the application shell with a green title bar, native window drag/resize chrome and minimize/maximize/close controls, compact top navigation, icon command ribbon, full-width white worksheet, bottom sheet tabs and a restrained status bar. Removed the sidebar, demo login landing page, large warning banner, sample-paste button and user-facing prototype wording.
- Applied Segoe UI typography, neutral borders, fine horizontal/vertical gridlines, 30-pixel rows, green cell selection/focus, subtle calculated-cell shading, and consistent button hover/focus/disabled states. Added a selected-cell address and live read-only value inspector above the grid, plus compact Find in worksheet filtering. Retained keyboard editing, copying, rectangular paste, frozen identifiers and validation.
- Ribbon commands are wired for New invoice, Validate, Copy and Paste. Save, Export and Import are visibly disabled with explanations until their backend services are available; the UI does not report fake successful persistence. Read-only screens hide irrelevant editing actions. Receipt totals use three clear summary panels; account reports and owner submission history share the worksheet styling. Owner tab remains role-gated.
- Kept a small Sample workspace / Sample data notice because services still contain synthetic data. The role selector previews roles and is not authentication. Static values are still fixtures; no accounting formulas or backend implementations were added. Added unsaved-close confirmation that remains active after validation or screen changes.
- Verification: .NET 10 build passed with zero warnings/errors; `--self-test` passed after the redesign, covering existing paste/locking/default/validation checks plus role navigation and layout/rendering of all screens. Corrected a reused validation-panel parenting issue caught during repeated screen navigation. Rendered real WPF invoice, receipt and owner screens to PNGs under the Desktop project's ignored `bin/` directory and visually inspected them. Corrected transparent render background and flushed WPF binding/layout work so row headers align with column headings. Synthetic render images contain no company data.
- Developer-only visual verification: `dotnet Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.dll --render-ui <absolute-image-path> [invoices|receipts|details|ledger|owner]` renders the current sample service only. Normal application startup does not create images, logs, settings or business files. This command is a design-review helper, not a report export feature.
- Limitations: exact Figma matching remains unverified; interactive user acceptance, live clipboard/high-DPI/screen-reader checks, clean-PC launch and production USB packaging are still pending. Backend readiness has not changed: sign-in, saves, allocation editing/posting, report generation/export and real report import/review remain pending Developer B. No commercial UI dependencies were introduced.
- Run the updated UI with `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`; it now opens directly to the invoice worksheet. Use bottom tabs for the distinct screens; Owner can be selected in the sample role control to inspect Operator Reports. Previous work-log demonstration steps describe the superseded UI.
- Next: obtain visual feedback on the redesigned application, inspect the exact reference when accessible, then integrate Developer B's agreed service adapter without placing calculations, credentials, parsing or persistence in UI code.
