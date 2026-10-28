# TradeVault

Offline Windows software for shipment records, USD receivables and party reports, launched independently from Owner and Operator USB drives.

## Project status

Current implementation: **UI 2026.10.27.9**, 2026-10-27. Party workspaces, generated reports, receipt allocation/correction, Excel/package export, Owner import/review, reminders, backup/restore and password management are implemented. Legacy independent worksheets are preserved. Generated reports require explicit field mapping; see the [workflow guide](Docs/Workflow-Guide.md).

Verification: 28 backend tests and 39 desktop acceptance checks passed, along with the published executable's isolated self-test. See the [implementation report](Docs/Testing/2026-10-27/Implementation/Report.md) for evidence and limitations. Physical USB/clean-PC, hands-on UI/printer acceptance and confidentiality decisions remain outstanding.

## Run the connected application

- Development: `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`. Close the old running copy before rebuilding/relaunching.
- Portable release: extract the entire `Src/TradeVault.Desktop/bin/Portable/TradeVault-win-x64-2026.10.27.7.zip` onto each USB and open TradeVault.Desktop.exe. Keep separate Owner and Operator folders. The runtime is bundled.
- Preserve existing Data/ and Backups/ folders during upgrades. Company databases, exported reports and backups live beside the executable. No company data was changed during implementation tests.
- Predefined usernames are owner and operator; credentials use salted PBKDF2 hashes. Change passwords through the application. Do not record passwords in this log.
- USD throughout; 90-day terms and USD 3,800 shipping defaults are editable. Terms are retained per invoice; changing the default affects new records.
- Owner import stores immutable submissions separately from working records. Checksums provide integrity, not sender authentication. The database and packages are not encrypted.
- Earlier work-log entries below describe historical milestones and may contain superseded status statements.

## Historical foundation status (superseded)

The original introductory milestone statements are retained here for attribution/history. The current implementation status above supersedes them.

Desktop interface prototype implemented in Src/TradeVault.Desktop/ with synthetic in-memory services and dependency-free UI checks. Database, real authentication, accounting services and file handling were not implemented in that prototype. Docs/ contains three reference workbooks.

Backend foundation implemented and verified: Domain/Application/Infrastructure projects, SQLite migration, party/invoice services, receipt allocations, bank-contract balance queries, consistent backup and nine passing automated tests. At that milestone there was no desktop executable or production-ready application; authentication, full accounting reports, Excel exports and owner report import were future work.

The later modern Excel-style workspace milestone initially used synthetic services pending integration. Its authentication, accounting, persistence and file operations were not production-ready at that stage. Subsequent entries record integration and replacement of those mocks.

Historical backend quick start: open TradeVault.sln with the .NET 10 SDK; run build-backend.ps1 (initial restore requires network). See Docs/Backend-Integration.md and Application/Contracts.cs. The original schema foundation is Migrations/001_Initial.sql; subsequent migrations and schema 4 extend that foundation.

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

### 2026-10-27 — User-requested realistic test records

- User explicitly requested dummy data and confirmed the application was closed. Added an opt-in `--seed-test-data` command; normal startup never seeds records. Duplicate test-party names abort insertion to avoid repeat seeding.
- Created a consistent pre-insertion backup: `Src/TradeVault.Desktop/bin/Debug/net10.0-windows/Backups/tradevault-20261027-075543-8948a704a2f64aca80d9400a11d7bdb7.db`.
- Added three clearly labelled fictitious parties: `[TEST] Meridian Textile Trading`, `[TEST] Bengal Colour Mills`, `[TEST] Gulf Fabric Industries`. Each contains 4 input invoices, 4 detail-report rows and 13 ledger rows: 12 invoices, 12 details and 39 ledger rows total.
- Scenarios: fully settled invoice, partial bank/difference receipts (two bank receipt lines), unpaid invoice, missing final BL/due date, editable USD 4,200 shipping versus the USD 3,800 default. All references/containers/BL identifiers are prefixed TEST. Baseline as-of date is 27-Oct-2026; sample amount-field equality is a fixture choice, not an inferred business rule.
- Seeded through actual authenticated party/worksheet services and calculation controller. Existing Sinuvera Technologies and Unassigned records remained unchanged and empty. Samples are party-scoped; both roles can inspect them on this USB database.
- Validation: normal Desktop build passed with 0 warnings/errors; independent SQLite integrity check returned `ok`; verified per-party counts and reconciled every sample ledger closing balance to pending contract plus pending difference. Closing USD totals: Meridian 191,202.50; Bengal 224,952.88; Gulf 258,703.25.
- Scope: these are manual worksheet fixtures, not integration with normalized receipt-allocation tables. The input worksheet's overdue field remains calendar-based even for settled sample invoices, as documented in the audit. For testing, switch the Party selector to a `[TEST]` party after reopening.

### 2026-10-27 — Party-wise separation phase

- Added schema migration 3: `worksheet_parties` and composite-key `party_worksheets`. Version-1/2 databases receive a consistent integrity-checked backup before migration. Legacy JSON/revisions are copied exactly to Unassigned records; the original table is preserved.
- Added authenticated party create/list APIs, normalized unique names, party-existence validation, party-scoped save conflicts and safe whole-worksheet assignment. Assignment refuses destination overwrite and mixed invoice ownership, copies metadata, empties the unassigned source and increments its revision to block stale resurrection.
- Added Party selector, Create party dialog and Assign worksheet flow outside the Excel columns. Switching retains the active sheet and prompts on unsaved edits. Selected parties get independent records/openings/terms/as-of dates; invoice PARTY NAME defaults to the chosen party and mismatches block Save.
- Preserved existing Excel field labels, date typing, formulas and role login. No automatic cross-report generation or normalized receipt allocation integration is claimed. Mixed legacy datasets are not silently split or assigned.
- Added `PartyWorkbookTests.cs`: 5 new tests covering party isolation across all sheets/reopen, validation/authentication, assignment safeguards, mixed ownership and exact migration/pre-upgrade backup. Existing 9 backend tests pass: 14 total, 0 failed/skipped. Desktop live tests cover UI switching/save/reload and party defaults/settings. Existing acceptance checks pass 39/39.
- Built the normal Desktop output successfully with 0 warnings/errors; build label `UI 2026.10.27.6`. Reviewed rendered normal/minimum layouts with the new selector. Tests use isolated synthetic databases; no company records were created or assigned by this session. Real schema migration runs on next application launch.
- Added `Docs/Party-Workspaces.md` with usage, schema and migration limitations. Next phase: party-specific report export and Owner import.

### 2026-10-27 — Automatic date typing and header window controls

- Replaced calendar cells with a keyboard-first date TextBox. Day-first formatting inserts separators and pads single-digit months: `277` becomes `27-07-`, and `2772026` becomes `27-07-2026`. Completed dates persist as ISO; partial/invalid dates remain editable and are rejected on Save. Backspace/Delete are not forcibly reformatted. Replaced the As-of calendar as well.
- Date display uses DD-MM-YYYY, including read-only calculated dates. Regression coverage includes character-by-character entry so a two-digit partial year is not prematurely expanded.
- Added minimize/maximize/close directly beside Sign out in the actual visible workspace header, removing the duplicate upper strip for that window. Resize/drag chrome and login caption controls remain. Header account controls are explicitly hit-testable. Build label: `UI 2026.10.27.5`.
- Verification: isolated Desktop build passed with 0 warnings/errors; live regression checks passed; acceptance harness passed 39/39. Render review confirmed date fields have no calendar icons and header window controls are visible. No company data modified.
- Original output was initially locked by the open app. After the user confirmed it was closed, the standard executable was rebuilt successfully with 0 warnings/errors and live checks passed again. Existing Data location was preserved. No process was forcibly stopped.

### 2026-10-27 — Caption controls, validation and calendar regression fixes

- Added explicit minimize, maximize/restore and close caption buttons to both top-level windows, with resize chrome, hit testing and accessible names. Build label is `UI 2026.10.27.4`.
- Changed draft validation: missing required fields are checked on Save instead of every cell change. Invalid entered values are still validated. Fixed row-wide error propagation by scoping error messages/highlighting to the affected field; disabled inappropriate automatic validation on the read-only formula bar.
- Added calendar-backed cells for editable date fields, including proforma date, arrival date, BL dates and receipt dates. Selected dates persist as ISO dates; manual day-first input remains supported. Calculated due dates stay locked.
- Fixed numeric sorting that previously compared amounts lexically. Fixed invalid rows clearing unrelated row calculations. Unknown amount prerequisites no longer create invented difference totals; running balances remain unavailable downstream of incomplete rows. Added BL/terms overflow validation.
- Added targeted regression checks for draft/save validation, field error scoping, calendar round trips/clearing, numeric sorting, unrelated row calculation preservation and caption controls. Updated existing acceptance layout/chrome checks for explicit 36-pixel captions.
- Verification: isolated Desktop build passed with 0 warnings/errors; live regression checks passed; comprehensive acceptance harness passed 39/39 checks; backend tests passed 9/9. Generated isolated UI renders in `.build/fix-review`. All synthetic records remained in isolated test databases; company data was not edited.
- The original Desktop executable was initially locked by an already running application. Verified binaries were first built under `.build/verified-app`; after the lock cleared, the normal Desktop output was rebuilt successfully with 0 warnings/errors. Open the normal executable to keep using its existing Data folder. No user process was forcibly terminated.
- Render review found clipped calendar text in compact rows; added a dedicated compact date-cell template and verified readable dates/calendar icons in the regenerated worksheet render. Final live regression checks passed after that correction.
- Remaining scope: automated checks and renders do not certify every OS-level mouse action, multi-monitor/high-DPI setup, physical USB failure scenario or unfinished report-import workflow. Existing functional gaps from `Docs/Application-Audit.md` are not claimed complete.

### 2026-10-27 — Full application status audit

- Inspected the current application after the latest UI/date/blank-row improvements. Recorded implementation status and evidence in `Docs/Application-Audit.md`.
- Verified Desktop build (0 warnings/errors), Desktop live checks (PASS), and backend tests (9 passed, 0 failed/skipped). Used disposable synthetic test data only; no company records changed.
- Key findings: three independently saved worksheet snapshots; normalized receipt backend not wired into Desktop; account reports lack party scoping; owner import/export unavailable; As-of only changes overdue-day arithmetic; backup exists only as a backend API. Desktop is not included in the shared solution.
- Recorded user instruction to retain independent amount fields and defer shipping unit policy. No shipping posting trigger was inferred from the ambiguous 'yes' response.
- Recommended next: party-scoped report datasets and migration safeguards, then actual export/Owner import, receipt integration, report correctness and USB release hardening.
- Changes this session: audit document and README only. No application logic or reference workbooks modified.

### 2026-10-27 — Corporate desktop redesign

- Applied the design-system skill to replace the basic green toolbar layout with a navy navigation rail, slate workspace, white worksheet cards, contextual headings and consistent blue primary actions.
- Rebuilt login as a split brand/sign-in layout with proper labels, keyboard focus, empty-field feedback, authentication progress and inline errors. Real credentials/session handling remains connected.
- Restyled shared WPF buttons, text inputs, tooltips, grid headers/cells and selection/focus/disabled/error states in `App.xaml`; added shared component helpers in `Ui.cs`. No third-party dependencies or network assets added.
- Updated `LiveMainWindow.cs`: active navigation, live record count, first-record empty state, contextual terms/opening balance controls, Save enabled only for edits, inline error notification, saved/unsaved feedback, compact row toggle and Ctrl+S/Ctrl+N. Retained working new/save/reload/delete/recalculate and sign-out flows. Storage path is available as a tooltip rather than occupying the workspace.
- Restyled `ExcelGrid.cs` while preserving workbook headers, calculated-cell locking, frozen identifiers, filtering and rectangular clipboard behaviour. Removed misleading hardcoded shipping/terms text from the grid legend. Owner import remains visibly disabled because its workflow is not yet implemented.
- Added `Docs/UI-Design.md` with tokens, component behaviour, keyboard shortcuts and layout boundaries. Added disposable render mode and UI action checks to `LiveChecks.cs`; no sample records were added to the company database.
- Verification: final Desktop build passed with 0 warnings and 0 errors; live persistence/authentication checks passed, including UI Add/Save/Reload and all worksheet layouts at minimum size. Rendered and visually reviewed login plus all three worksheet views at 1480×920 and the ledger at 1100×720. Corrected transparent canvas rendering and small-window empty-state overlap during review.
- Limitations: render/action checks do not establish physical mouse/clipboard, high-DPI or screen-reader acceptance. Existing worksheet independence, unencrypted database and unavailable owner import/export are unchanged functional limitations.

### 2026-10-27 — Connected worksheets and authentication

- Re-read all three source workbook field lists. Generated `ExcelFields.cs` using their exact nonblank labels/order without importing sample company entries; source workbooks unchanged.
- Added schema migration 2 for salted password hashes and versioned worksheet storage. Existing version-1 business tables are preserved. Added authenticated sessions, owner-only access checks, invalid-login throttling, transactional worksheet saves and save audit events in `WorkbookStore.cs`.
- Connected Desktop to Infrastructure. Added `LoginWindow`, real startup composition/disposal, and `LiveMainWindow` with new/save/reload/delete actions, unsaved-change prompts, editable defaults and persisted opening balances. Removed old `MainWindow.cs`, mock service and obsolete prototype checks; production has no dummy data source.
- Added `WorksheetController` validation and actual formula calculations; calculated cells are cleared when input validation fails. Kept the Excel grid's keyboard/copy/paste/filter features and green application header. Date/amount validation failures block saves; failed and stale saves retain input.
- Tests: Desktop build passed with 0 warnings/errors. `--self-test` passed real authentication, wrong-password rejection, owner access, logout invalidation, empty initial worksheets, exact field counts, formula results, date/reminder rules, stale revision rejection, persistence across reopen, locked columns and main-window construction/layout. All 9 existing backend tests also passed. Synthetic test rows existed only in disposable test databases and were removed.
- Created the application's actual `Data/tradevault.db` using `--initialize-db`. Independent SQLite inspection returned integrity `ok`, zero worksheet datasets and zero parties/invoices/receipts; only the two predefined account identities were seeded. No production company records were added or deleted.
- Build environment: cached dependencies were used; online vulnerability metadata was unreachable, so NuGetAudit was disabled for that local restore command only. Final build used `--no-restore`; no project-wide audit suppression added.
- Limitations: worksheet datasets are independent, not linked automatically to normalized invoice/receipt tables. This delivers exact Excel-shaped manual entry/storage, not automatic cross-report generation or allocation posting. Owner file import/export, database encryption, password recovery/change UI and physical USB validation remain outstanding. Existing normalized backend tests/services remain intact.
- Next: agree mappings among customs/bank amounts and shipment charge policy, then connect receipt allocations and generate both reports from one authoritative dataset. Add owner package import/export afterwards.

### 2026-10-26 — Investigated executable lock during launch

- Read the user's pasted terminal output. MSB3026 retries and MSB3027/MSB3021 failures identify `TradeVault.Desktop` PID 9380 as holding the executable open while `dotnet run` attempted to rebuild it.
- Cause: an existing application instance prevents Windows from replacing its executable; the supplied output does not indicate a C# compilation failure.
- Resolution: save any needed edits, close all TradeVault windows, then rerun `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`. Use Task Manager only if a process remains after closing. For reopening an already built version without rebuilding, use the executable or `dotnet run --no-build --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`; this does not incorporate newly pulled changes.
- No application process was forcibly terminated and no application source was changed. Validation was inspection of the supplied error log and a current process query; no rebuild claimed in this session.

### 2026-10-26 — Rebuild after pulling revised UI

- Confirmed the pulled `MainWindow.cs` contains the revised green title bar, horizontal navigation and ribbon interface. No running TradeVault process was found during inspection.
- Rebuilt the Desktop project with `dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --no-incremental --nologo`: succeeded with zero warnings and zero errors. The updated executable is `Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.exe`.
- Git pull updates source files; it does not rebuild ignored executable outputs or update copies on another folder/USB. Launch the rebuilt output, or use `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj` after pulling.
- Changes: this README and generated build outputs only; no UI or backend source changes. Interactive visual acceptance remains pending.

### 2026-10-26 — Pulled desktop prototype verification and launch guide

- Inspected Developer A's Desktop project, startup/self-test entry point, mock service composition and integration proposal. `MainWindow` currently constructs `MockPrototypeService`; no backend adapter is connected.
- Verification: built `Src/TradeVault.Desktop/TradeVault.Desktop.csproj` with the installed .NET 10 SDK: 0 warnings, 0 errors. Ran `dotnet Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.dll --self-test`: PASS. These are build and in-memory prototype checks, not an interactive desktop acceptance test.
- Launch from the root with `dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj`, or open `Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.exe` after building. This development build requires .NET 10 Desktop Runtime and is not a self-contained USB release.
- Manual check: select Operator (demo); inspect Invoices & shipments; edit terms/shipping; use Add invoice row, Paste sample rows, filtering, resizing, keyboard navigation and rectangular copy/paste. Enter invalid shipping text, then Validate / mock save, and confirm errors clear after correction. Calculated fixture values intentionally do not update. Check both read-only reports and receipt summary. Switch to Owner (demo) to inspect Operator Reports and Preview demo submission; verify that navigation is absent for Operator.
- Limitations: saves are memory-only; closing loses mock edits. No real login, database save, receipt posting, Excel export or report-file import is connected. No actual UI session or physical USB test was performed in this verification.
- Repository observation: the current checkout has no root `TradeVault.sln`, common backend build settings, build script or backend integration guide from the earlier foundation. Git reports backend project directories and development caches as untracked. No files were deleted or backend integration attempted during this inspection. Reconcile/track Developer B's foundation before connecting it to Desktop.
- Files changed in this session: `Readme.md` only; build outputs were generated under Desktop bin/obj. Next: manually review the prototype, restore/reconcile backend source control, then implement a Desktop adapter against the shared Application contracts.
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

### 2026-10-26 — Developer A: login, continuous worksheet, dates and window controls

- Reviewed the current connected login/register implementation and latest README history before editing. Preserved other developers' entries, including pre-existing merge-conflict markers in historical README sections; did not resolve unrelated history or change backend contracts.
- Fixed username clipping by removing the custom TextBox content-host template and retaining native WPF text rendering with shared padding/focus styles. Grid editors override minimum height/padding to fit normal and compact rows. Rendered the populated synthetic username at 980x650 and 820x600 and visually confirmed full text visibility.
- Removed Add record and Add first record UI/overlay. Editable worksheets now display 100 spare blank rows, replenished after entry. A row becomes a real record only when a nonblank input is entered; untouched rows never reach the controller, validation, save or database. Defaults apply on promotion without overwriting entered shipping/terms. Existing Ctrl+N remains optional. Deletion is disabled for spare rows. Sorting keeps spare rows after actual records.
- Restyled the As of picker to match the main slate/blue controls: rounded border, normal-sized text, keyboard focus indication and calendar button. Its display uses day-first dates; invalid report-date text blocks recalculation/save instead of silently using the previous date.
- Added UI-only date parsing for date columns on edit commit and rectangular paste. Accepts ISO, day-first slash/dash/dot dates, two-digit years, and named months; normalizes to the existing yyyy-MM-dd storage format. Invalid dates remain visible for existing validation. Invoice/container identifiers and combined reference/description columns remain text. Due/reminder dates remain calculated and locked; no accounting formulas changed.
- Added a shared title bar to login and workspace using WPF WindowChrome and SystemCommands: visible minimize, maximize/restore and close buttons, window drag and resize. Existing unsaved-change confirmation still applies to close.
- Files: Desktop App.xaml, Ui.cs, LoginWindow.cs, LiveMainWindow.cs, ExcelGrid.cs, PrototypeModels.cs, WorksheetController.cs (row-promotion/default handling only), new DateCellConverter.cs, LiveChecks.cs, Docs/UI-Design.md and Readme.md. No Infrastructure/Application/Domain changes or database migrations. No company records, credentials or host-disk business files created by verification.
- Verification: isolated Desktop build passed with 0 warnings/errors; expanded --self-test passed real authentication/persistence, typed and pasted date normalization, invalid-date retention, unchanged identifier types, blank-row promotion/defaults, non-persistence of spare rows, calculated due-date behavior and existing save/reload checks. All 9 backend tests passed. Rendered and visually inspected login/register at normal and minimum sizes; title buttons, full username, styled date picker and blank worksheet are visible.
- Build limitation: a running TradeVault instance locked the normal output executable during the initial build. Did not terminate it. Used -p:OutputPath=bin/UIVerification/ for verification, keeping its disposable test database separate from production data. Physical mouse/calendar-popup/native-window actions, high-DPI and screen-reader checks remain pending.
- Next: save and close the currently running app, then rebuild/relaunch with dotnet run --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj to load these changes from the normal data location. Continue integration within the existing role and file-storage boundaries.

### 2026-10-26 — Developer A: compact workspace, native captions and full testing report

- Reviewed current README and Desktop files. Observed the older normal application still running with title TradeVault | Owner; did not terminate or change its company data.
- Replaced custom caption rendering with native WindowStyle.SingleBorderWindow / ResizeMode.CanResize globally for login and both role workspaces. No custom WindowChrome remains. Added UI 2026.10.26.3 to the native title/footer to distinguish new builds. Native system dialogs retain their OS controls.
- Rebuilt the workspace with a compact horizontal tab bar, narrow header/status bar, consolidated options/actions, reduced outer spacing, 28/36 px rows and narrower content-based columns. Removed the wide sidebar and large stacked cards. Compacted login spacing. Existing live authentication/save/validation/worksheet features retained.
- Added AcceptanceChecks.cs with deterministic dummy datasets and case-by-case report output (--qa-report). Added a compile-time-only UI_ACCEPTANCE interactive build, isolated from normal company storage. Updated LiveChecks renders to show synthetic data for all worksheets and both role layouts. Report writes JSON/Markdown; old self-test remains available.
- Files changed: Desktop Ui.cs, LiveMainWindow.cs, LoginWindow.cs, App.xaml, App.xaml.cs, ExcelGrid.cs, LiveChecks.cs, new AcceptanceChecks.cs; Docs/UI-Design.md, Docs/Testing/2026-10-26/ artifacts and this README. No shared backend contracts, SQL, migrations or business formulas were changed.
- Verification: final isolated build passed with 0 warnings/errors; 39 expanded desktop checks passed, 0 failed; existing live self-test passed; all 9 backend tests passed. Produced synthetic client-area screenshots, 39-case JSON/Markdown and backend TRX. Full report: Docs/Testing/2026-10-26/Report.md.
- An initial optional test-folder cleanup failed on a OneDrive folder. The new acceptance harness retains its isolated synthetic databases as evidence and cannot lose its report to optional cleanup. Production data was never opened by the harness. Subsequent 39-case run passed.
- Actual live QA accessibility tree exposed native Minimize/Maximize/Close. Windows screenshot capture timed out and click geometry was unavailable; mouse clicks/state transitions are NOT claimed passed. The user then stopped Computer Use with physical Escape; no further UI automation was performed. An isolated QA window may still be open.
- Limitations: owner submission import/export, integrated allocation UI, automatic cross-worksheet report generation and production USB acceptance remain incomplete. Numeric sorting is currently text-based and is recorded as a limitation in the report. Native clicks, clipboard/keyboard/dialog interactions, calendar popup, high-DPI and multi-monitor testing remain pending. Existing historical README merge markers were preserved.
- Next: save/close the running old app and rebuild/relaunch the normal project to load UI 2026.10.26.3; resume interactive acceptance when allowed. Do not use the synthetic InteractiveQA build for company work.

### 2026-10-27 — Current-state review and isolated synthetic-data audit

- Reviewed commit 5029364, latest source and project history before testing. Confirmed party workspaces/schema-3 migration backups, legacy assignment, custom caption controls, date masks, scoped validation, numeric sorting and persisted party metadata. Older documentation about absent party scoping/native-only captions is superseded; previous entries were preserved.
- No application logic or company data changed. Added only a test-only audit runner under Test/TradeVault.Desktop.Audit and evidence/report artifacts under Docs/Testing/2026-10-27; appended this README entry. Shared solution and backend projects were not modified.
- Built Desktop to isolated bin/Audit20261027: passed, zero warnings/errors. Executed current acceptance suite: 39 passed, zero failed. Current --self-test, including FixRegressionChecks and party UI-handler checks, passed. Backend xUnit: 14 passed, zero failed/skipped, including all five party/migration tests. Rendered and reviewed synthetic login/register screens at normal/minimum size.
- Additional audit seeded 3 named TEST parties, 12 invoices, 12 detail rows and 39 ledger entries into a new isolated database with pre-seed backup. Eight probes produced 4 successful safeguards and 4 confirmed gaps: duplicate worksheet invoices accepted, overdue arithmetic ignores receipt fields, no worksheet historical cutoff, and register terms remain worksheet-wide. Successful probes cover seeding/counts, party mismatch rejection, independent application roots and partial-receipt pending amounts.
- First restore of the new audit project hit NuGet vulnerability metadata access failure in the sandbox; approved network-enabled retry restored dependencies. Fixed a missing System.IO import in that test runner; final probe run succeeded. No project-wide security/audit suppression or application changes were introduced.
- Full report: Docs/Testing/2026-10-27/Report.md. Includes current changes, detailed test evidence, source-vs-UI boundaries, prioritized recommendations, eight remaining delivery phases and completion gates. Summary JSON, desktop case results, eight probe results, backend TRX and synthetic screenshots are alongside it.
- Limitations: no new physical UI/clipboard/caption-button, removable-media, clean-PC, high-DPI, accessibility, performance or damaged-storage tests. Export/import, integrated allocation UI and automatic shared-data reporting remain missing. Application is a functioning party-scoped worksheet system, not yet the full original product. Synthetic audit databases remain in ignored test-output folders; company storage/reference workbooks were not opened for writing.
- Next: review and approve the prioritized work order; address demonstrated correctness/policy gaps and integrate the original invoice-to-reports/manual-owner-sharing flow. Keep independent USD amount fields and defer automatic shipping postings until their accounting trigger is explicit.

### 2026-10-27 — Remaining-phase confirmation

- Rechecked current commit 5029364, README and Desktop/Application feature entry points. No new application implementation since the preceding audit; Owner import is still disabled and receipt services remain disconnected from the UI.
- Review only: appended this README entry; no code, company data or reference workbook changes. No tests rerun; referenced the existing 2026-10-27 test report rather than claiming new results.
- Remaining order: report export, Owner package import/review, receipt UI, authoritative shared report data, reporting correctness/reminders, backup/access hardening, portable release and end-to-end acceptance. Track duplicate worksheet references and worksheet-wide terms as correctness work alongside those phases.

## Work session — 2026-10-27 — remaining workflow implementation

Implemented generated reports from an explicitly configured register mapping; dated split receipts, advances, corrections/reversals; settlement-aware reminders and per-invoice terms; two Excel exports, printable views and versioned packages; Owner preview/import, duplicate/revision protection, read-only history and reviews; backup/restore and password management; self-contained x64 USB packaging. Imported submissions remain separate from Owner working data. Existing worksheet/normalized data is preserved.

Files affected: Application/WorkflowContracts.cs; Infrastructure/WorkflowStorage.cs, ReportWorkflow.cs, ReportFiles.cs, PortableDatabase.cs and WorkbookStore.cs; Desktop/WorkflowWindows.cs, LiveMainWindow.cs, WorksheetController.cs, PrototypeModels.cs, App.xaml.cs, Ui.cs, LiveChecks.cs and AcceptanceChecks.cs; Desktop publish/verification scripts; backend WorkflowTests.cs and migration tests; TradeVault.sln; Docs/Workflow-Guide.md and Docs/Testing/2026-10-27/Implementation/. Older developer work-log entries are preserved; stale introductory status and unresolved merge marker lines were removed.

Verification: Desktop build zero warnings/errors; 28 backend tests passed; 39 desktop acceptance checks passed; generated workflow self-test passed; published runtime-contained executable self-test exit 0; Excel structure independently read successfully. Synthetic test databases were isolated; no company database was changed. The portable ZIP excludes Data, Backups, Reports and synthetic TestData.

Limitations/next steps: physical USB and clean-PC testing, hands-on UI/DPI/clipboard/printer acceptance, company field-mapping validation and confidentiality/recovery policy remain release gates. No encryption/signatures or full historical invoice-state reconstruction are claimed. Negative-net generated credit notes remain unsupported. See the implementation report for complete boundaries. Prior audit probes describe historical gaps and are superseded by the workflow tests for changed behavior.

## Work session — 2026-10-27 — visible worksheet navigation and hover rendering

Request: restore Account details and Debit & credit navigation and address intermittent black rectangles during cursor movement. The supplied screenshot showed build .6 with its top caption/navigation outside the visible screen. Fixed the 1440 by 880 startup size to fit the Windows work area (including taskbar), with minimum dimensions constrained for smaller displays. Retained all three tabs and added horizontal navigation scrolling for constrained widths. Replaced the dark default tooltip with a light opaque control template without drop shadow to mitigate hover artifacts; intermittent graphics behavior has not been reproduced interactively and is not claimed conclusively fixed.

Files: Desktop/LiveMainWindow.cs, Ui.cs, App.xaml and AcceptanceChecks.cs. Build label is UI 2026.10.27.8. Verification: Desktop build zero warnings/errors; 41 acceptance checks passed including simulated 1366 by 768 work-area sizing and actual bounds for all three navigation buttons; self-contained x64 publication succeeded. Evidence: Docs/Testing/2026-10-27/NavigationFix/desktop-results.md. No accounting or company data changes.

Next steps/limitations: close the older running build and launch .8; verify cursor hover on the affected display. Physical DPI/multi-monitor and intermittent GPU/compositor artifacts still require hands-on confirmation. Preserve Data and Backups when updating USB application files.

## Work session — 2026-10-27 — read-only workbook calculation analysis

Inspected all three reference XLSX files using bundled Python/openpyxl in formula mode and compared WorksheetController, ExcelFields and ReportWorkflow. No workbook, company database or application behavior was changed. formet.xlsx contains 26 labels and no formulas. Account Details contains difference C-D, bank pending D-E, net difference C-D-H, running difference, due-date and summary formulas. Debit/Credit contains running balances and BL-plus-terms due dates, with three G due cells linked to K and earlier examples entered as dates.

Findings: invoice-register F8 remains manual although report G is calculated; selecting whether the bank contract maps to Our Customs or Pak Customs requires company confirmation. Account Details K4 uses K3+J4 whereas subsequent rows use previous K+I-J; current app uses the consistent latter rule. J21 sums only J3:J14 despite populated J15:J18; H21 stops at H15 (later H inputs currently blank). N14:N18 lack due formulas. Row 8 is a receipt-only row and should not be mistaken for a missing invoice. Generated app reports use the saved register and receipts after explicit mapping; preserved legacy reports remain independent/manual. Exported Excel contains calculated values, not live Excel formulas. Formula inspection is not a recalculation in Microsoft Excel. Next: confirm bank-contract source and shipping treatment before changing the register difference field to a locked calculated field. Explanation delivered in the conversation; this session makes no formula-rule changes.

## Work session — 2026-10-27 — workbook-derived test workspace and calculated fields

Implemented Invoice Register Difference Amount as a locked calculated column, provided by ReportWorkflow.RegisterDifference: Original minus the explicitly configured bank-contract source. It stays blank if mapping or required inputs are absent; register status/tooltip explains setup. No company mapping was guessed. Ledger's duplicate G Due is now locked and follows calculated K Due. Existing consistent first-row running balance, complete generated totals and BL-plus-terms calculations apply rather than reproducing the source spreadsheet errors. Reference XLSX files remain unchanged.

Created a separate self-contained build UI 2026.10.27.9 at Src/TradeVault.Desktop/bin/Portable/ExcelDemo-2026.10.27.9 with an intentionally included TEST database: 14 workbook-derived invoices plus a two-invoice receipt-practice party, two shared/partial receipts and USD 250 advance. Source names/identifiers replaced; real reference amounts retained. Missing dates/terms and round-off treatment are documented in DEMO-READ-ME.md and Docs/Testing/2026-10-27/ExcelDemo/Guide.md. Blank shipping is zero; pending BL is blank. Unknown Pak Customs amounts stay blank. Missing invoice date and all baseline payment dates use explicitly documented TEST assumptions. Main generated balance USD 713,243.00, bank pending USD 400,363.00 and difference pending USD 312,880.00 were independently inspected. Corrected legacy balance including USD 0.25 round-off is USD 312,879.75. Generated reports intentionally omit that undated non-invoice adjustment.

Files: Infrastructure/ReportWorkflow.cs; Desktop/ExcelFields.cs, WorksheetController.cs, LiveMainWindow.cs, Ui.cs, App.xaml.cs and new ExcelDemoSeeder.cs; WorkflowTests.cs and AcceptanceChecks.cs; Workflow-Guide.md; workbook-derived JSON fixture, evidence and guide under Docs/Testing/2026-10-27/ExcelDemo. Demo seeding is explicit and rejects existing database files; normal startup never inserts samples. Existing company databases were not opened or modified.

Verification: build zero warnings/errors; 29 backend tests passed (TRX retained), 42 acceptance checks passed, isolated desktop self-test passed, self-contained x64 publication succeeded, 16 calculated F8 values persisted, 14/2 generated report rows verified, corrected legacy round-off balance checked. Samples and exports are included only in the separate TEST package. Physical cursor/DPI/USB/printer acceptance remains outstanding. Next: run the test package, select a TEST party and exercise edits/receipts/export/import in independent application folders. Confirm contract mapping for actual company records before enabling generated reports there.
