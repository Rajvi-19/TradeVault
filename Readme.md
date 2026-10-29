# TradeVault

Offline Windows software for shipment records, USD receivables and party reports, launched independently from Owner and Operator USB drives.

## Project status

Latest UI/account update: **UI 2026.10.28.1**. Compact sign-in, Show/Hide password, case-sensitive usernames, offline single-use recovery codes, application icon and a redesigned Operator Reports inbox. Workspace Password buttons are removed. See [Offline account recovery](Docs/Offline-Account-Recovery.md) before relying on Forgot password. Schema 5 preserves existing credentials/records and adds recovery storage.

Current implementation: **UI 2026.10.27.10**, 2026-10-27. Party workspaces, generated reports, receipt allocation/correction, Excel/package export, Owner import/review, reminders, backup/restore and password management are implemented. Legacy independent worksheets are preserved. Generated reports require explicit field mapping; see the [workflow guide](Docs/Workflow-Guide.md).

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

### 2026-10-28 — Six-character passwords and visibility icons

- Changed password change/reset validation and reset-screen guidance to 6–128 characters, as requested. Existing passwords are unchanged.
- Replaced Show/Hide words with vector eye/crossed-eye icons across sign-in/recovery inputs. Accessible Show password / Hide password names, tooltips, keyboard access and secret-clearing behaviour remain.
- Added a regression test accepting exactly 6 characters and rejecting 5 for both password change and recovery. Updated the recovery guide; build label is `UI 2026.10.28.2`.
- Verification: 34 backend tests passed; normal Desktop build passed with 0 warnings/errors; live UI/persistence checks passed. No company records or passwords were changed by verification.

### 2026-10-28 — Authentication, recovery, branding and Operator Reports redesign

- Rebuilt login as a compact single-form layout with visible username case guidance, Show/Hide password, progress/errors and Forgot password. Removed the large decorative side panel and workspace Password actions. Added reusable password-visibility control and tested show/hide value preservation and clearing.
- Fixed case sensitivity at the storage boundary: removed automatic lowercasing and used binary username matching. Existing lowercase account identities/password hashes are preserved.
- Implemented schema 5 account recovery: explicit authenticated code generation, one-time display, 160-bit random codes stored only as salted PBKDF2 hashes, rotation, single-use consumption, session invalidation and persistent failure cooldown. Existing-database migration takes a backup. No OTP, security questions, master password or automatic reset bypass added. Password changes invalidate older recovery codes. Recovery flow and limitations are documented in `Docs/Offline-Account-Recovery.md`.
- Added a new original TradeVault vault/monogram logo, reproducible asset builder `Tools/create_app_icon.py`, multi-size ICO and PNG resources. Embedded ApplicationIcon in the Windows executable and placed the mark in login, main header, Operator Reports and window icons. Extracted the built executable icon successfully for verification.
- Replaced the old Operator Reports toolbar with a dedicated inbox window: search, status filter, submission/review counts, clear empty state, disabled actions until selection, source/revision details and a compact review-note editor. Existing preview/checksum/revision/import services remain connected.
- Added backend tests for case-sensitive login, recovery replay/rotation, restart persistence, cooldown, session invalidation, other-account isolation and invalidation after password changes. Updated legacy migration fixtures for schema 5. All 33 backend tests passed. Desktop build passed with 0 warnings/errors; live UI/persistence checks and 42 acceptance checks passed on the isolated build. Normal Desktop output was rebuilt successfully. Company database was not opened or modified by tests; migration happens on next application launch.
- Rendered and reviewed compact login and Operator Reports; fixed dark inherited dialog backgrounds found during review. No company report records or sample submissions added. Existing import/privacy and physical-device acceptance limits remain.

### 2026-10-28 — Inspected newly added report/workflow features

- Inspected clean commit `22f6b8a`, application startup, schema 4, workflow contracts/storage/calculations/packages, Desktop dialogs/orchestration, tests and publish/verification scripts. Recorded the updated architecture and implementation in `Docs/Change-Inspection-2026-10-28.md`.
- Confirmed report setup, generated/legacy views, per-invoice IDs/terms, dated receipts/corrections/advances, Excel/HTML/package export, Owner import/review, reminders, backup restoration, password management and portable publication scripts are now implemented. Earlier chat statements calling these missing are superseded.
- Fresh isolated source verification: Desktop build 0 warnings/errors; 29 backend tests passed; live self-test passed; 42 acceptance checks passed. Output was isolated under `.build/inspection-20261028`; no company database was opened/changed and no portable ZIP was republished.
- Noted documentation/package-version drift (source UI .9 versus README portable ZIP .7) and retained documented historical/confidentiality/device-acceptance boundaries. This was an inspection, not application logic modification.

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

## Work session — 2026-10-27 — realistic workflow samples in MAIN application

User explicitly requested direct changes/sample data in the main app rather than another ZIP or separate test distribution. Confirmed the running copy was bin/Debug/net10.0-windows; user saved and closed it before updating. Built that normal app in place as UI 2026.10.27.10, bringing in the existing calculated Difference Amount, locked due columns, screen-fit and tooltip changes. No ZIP or distribution package was created in this session.

Added TestDataSeeder.RunWorkflow and an explicit DEBUG-only --seed-workflow-data command. Authenticated with existing Owner credentials, made a consistent pre-seed backup, and added three new named [TEST] parties to the actual main application database. Existing records were preserved. Each party has four fictional invoices, explicit TEST contract/shipping mapping, three dated receipts (settlement, partial and shared allocation), and USD 100 unallocated advance. Twelve invoices and nine receipts total. The scenarios are settled, overdue partially paid, due within 30 days and BL pending as of 2026-10-27. All generated reports derive from saved register/receipts; duplicate manual report rows were not inserted. No exports or imported submissions were manufactured; users can exercise those actions from the UI.

Files changed: Desktop/TestDataSeeder.cs, App.xaml.cs, Ui.cs; actual bin/Debug/net10.0-windows/Data/tradevault.db (authorized sample additions only); Docs/Testing/2026-10-27/MainWorkflow hash-only preservation evidence. Existing company values were not copied into verification logs. Main database integrity_check returned ok. Before/after hashes confirm every pre-existing party and worksheet row is unchanged. This session created 12 invoices with 12 persisted calculated Difference Amount values. Re-running the command reported existing samples and made no changes. Normal startup never adds sample data.

Verification: normal Desktop build zero warnings/errors; sample generation checked four report rows and four distinct reminder states per party; main database preserved/integrity checks passed; idempotency verified. The isolated desktop self-test was also run (see conversation for result). Sample receipts/mappings follow existing verified backend workflow. Main sample bank-contract source F6 is a declared TEST assumption; no existing company mapping was changed. Physical USB, printer, multi-monitor/cursor-artifact acceptance remain unverified.

How to use: reopen the normal main app, select [TEST] Indigo Textile Imports — Workflow (not Unassigned records), keep Legacy manual reports unchecked, then inspect Invoice register, Account details, Debit & credit, Receipts and Reminders. For Indigo, invoice 001 settled; 002 outstanding USD 13,700 and 28 days overdue; 003 outstanding USD 25,800 and due 2026-10-30; 004 outstanding USD 12,000 and BL pending. Party ledger closes at USD 51,400 after USD 100 unallocated advance. Other TEST parties use scaled fictional invoice amounts. Next: exercise edits, receipt correction/reversal, export, and Owner import between independent copies. Purpose/workflow explanation supplied to the user in the conversation.
# Work log — 2026-10-28: reports, receipts guidance and branding

- Added separate Excel-only export: two workbooks in a party-named folder on the application drive; removed explanatory rows below totals. Owner submission packages remain a separate action to preserve offline import.
- Added party name beside invoice number in reminders. Removed Legacy manual reports and Selected terms controls; stored historical data and per-invoice terms remain intact.
- Full-width row paste now copies input cells at their original positions and skips protected formula cells. Partial ranges retain atomic validation.
- Replaced print layout with landscape paginated tables, repeated identity/column headers, numeric formatting, totals and page numbers. Windows Microsoft Print to PDF is the PDF destination; physical printer/PDF dialog acceptance remains manual.
- Replaced app icon with a navy/teal geometric TV ribbon mark across executable and headers. Build label UI 2026.10.28.3.
- Added [reports and receipts guide](Docs/Reports-and-Receipts.md) explaining cutoff dates, allocations, setup and recalculation. No company records changed.
- Initial verification: Desktop build passed without warnings, 34 backend tests and live checks passed. Added focused export and full-row paste regressions for final verification.
- Final verification: Desktop build passed with zero warnings/errors; all 35 backend tests and live UI/persistence checks passed, including the new export/paste regressions. Windows print-to-PDF output remains a manual acceptance check.

# Work log — 2026-10-28: Excel-only handover (UI 2026.10.28.4)

- Replaced .tvreport export/import with one standard .xlsx workbook containing Account Details and Debit Credit tabs. Removed Create Owner submission button. Export names include party, report date and revision; saved in a party-named folder without random folder IDs. Export opens the saved file through Windows; a missing spreadsheet application shows the path without losing the export.
- Owner import validates the original workbook's embedded snapshot and both visible tables, preserves source/revision review history and working-database isolation, and checks preview-to-import file identity. Existing stored imports remain readable. Edited/resaved or arbitrary Excel files are not supported for snapshot import; original TradeVault exports are required.
- Report setup automation remains pending the user's required default choice for contract/receipt sources; no accounting fields were silently reinterpreted. Existing per-party configurations remain intact. Manual setup remains available until this choice is supplied.
- Verification: normal Desktop rebuild succeeded (zero warnings/errors) after user closed the locked app; 36 backend tests and live UI/persistence checks passed. Tests cover Excel handover/reopen/revisions, duplicate import, metadata and visible-cell tampering, preview swapping, meaningful naming and both worksheets. Independent openpyxl reader loaded both report tabs. Opening in installed Excel via the Windows association still needs interactive acceptance.
- No company data or credentials modified. Updated report guide. Earlier .tvreport documentation describes the superseded workflow; this entry and Reports-and-Receipts.md describe current behavior.

# Work log — 2026-10-28: business validation, date calendar and print reliability (UI 2026.10.28.5)

- Added shared InvoiceRules applied both on worksheet Save (field-scoped issues) and database register Save (atomic rejection). Validates chronological proforma/invoice/BL/arrival/payment dates, required effective dates for positive cumulative payments, USD precision/range and terms/date overflow. Same-day payments remain valid; pre-invoice money is supported through unallocated receipt advances. Unknown shipment dates may stay blank. Existing receipt allocation/reversal/history safeguards retained.
- Removed date instruction tooltips, retained typed date formatting, added calendar button to As of with immediate recalculation after selection.
- Investigated screenshot's native RuntimeWrappedException. The custom paginator had a null source; replaced it with a valid source and print via FixedDocument. Landscape requested before dialog; native printing failure routes to direct PDF saving. Exact driver-specific failure cannot be established from the screenshot alone.
- Added direct Save PDF using the same white-background page layout, with high-resolution Unicode-aware rendered pages, repeated headings, amounts, totals and page numbers. No printer driver or extra runtime package needed. PDF is rasterized; text is not selectable. PDF opens after saving where an association exists.
- Verification: isolated build passed with zero warnings/errors, 50 backend tests passed, 42 acceptance checks passed, live UI/persistence checks passed. Added regressions for date ordering/same-day acceptance, missing payment dates, USD constraints, atomic invalid-save rejection, absent tooltip, paginator source and multi-page PDF creation. Normal executable rebuilt after user closed running app. Final PDF inspection/normal-build results recorded below when complete. Physical printer interaction remains manual. No company data changed.
- Details: [business validation guide](Docs/Business-Validation.md).
- Final verification: normal build passed with zero warnings/errors; all 50 backend tests, live checks and all 42 acceptance checks passed. Independent PDFium and pypdf readers validated the generated three-page PDF; all three pages rendered successfully and the first page was visually inspected. PDF generation does not exercise an installed printer driver.

# Work log — 2026-10-28: client-delivery audit (UI 2026.10.28.6)

- Audited authentication/recovery, Owner/Operator workspaces, party/register entry, dates/terms/shipping/formulas, receipts/advances/corrections/reversals, reminders, report cutoff/setup, Excel transfer/review, backups/migrations/restore and PDF output. Used isolated synthetic databases only.
- Fixed historical receipt over-allocation hidden by future reversals, duplicate internal invoice IDs, generated-tab cutoff resets and missing opening-only difference totals. Added focused regressions and calendar event checks.
- Fixed reminder date/USD formatting, standalone date-input affordance, PDF headings/date/integer presentation and clipped total label. Added pending-login close guard and a friendly duplicate-instance error.
- Added repeatable WPF page/dialog capture harness covering Owner/Operator pages, correction/reversal and review dialogs, recovery and reports. Improved screenshot sizing for shown windows. Replaced obsolete Desktop.Audit probes and made verification scripts propagate live-check failures.
- Verification: 53 backend tests, 42 acceptance checks and live UI/persistence checks pass; Debug/Release and audit runner build without warnings/errors. Full-width 35-invoice PDF has 9 pages; independent PDFium rendering and pypdf parsing pass. No real company records changed.
- Native UI automation could not see the test process launched by the execution environment; hardware printing, physical USB handover/ejection, Windows file associations and a self-contained release on a clean client PC remain manual acceptance items. Source-field decisions for automatic Report setup are still pending; no accounting mapping was guessed.
- Full findings, coverage, evidence and client sign-off items: [client audit report](Docs/Testing/2026-10-28/Client-Audit.md). Do not distribute `.build/client-audit` synthetic workspaces.
- Final evidence: Release page harness passes with 36 screenshots/22 page states; normal Debug executable rebuilt successfully with current sources, zero warnings/errors. Final independent PDF/Excel checks saved under `.build/client-audit/final-release-pages/independent-file-checks.json`.


## Work session — 2026-10-28 — clearer payments, field names and linked report updates

User requested invoice edits reflected in both report tabs, per-invoice unpaid amounts in Receipts, clearer component/action labels and scrolling/accessible summary. User saved and closed the main app before update. Implemented PaymentsWindow with one row per invoice, dated service-provided bank/difference/total outstanding, editable Pay bank/Pay difference, per-invoice remaining preview, automatic receipt/assigned/advance summary, inline guards, collapsed payment history, explicit new/edit/cancel modes, and pinned summary/save footer below a scrolling form. Removed manual Calculate summary and ambiguous Component/Reference-account/Post/Reverse labels. Correction date wording changes by mode; history identifies cancelled/replaced entries. Added Application InvoicePaymentBalance and Infrastructure PaymentBalances/RemainingAfterPayment; no accounting persistence/formulas were added to UI code.

Invoice navigation now saves valid register edits before rebuilding either generated report. Invalid input blocks switching; explicit mapping is prompted for a named customer with no settings. Both tabs continue sharing reporting cutoff. Existing payment-history amount/date protection and frozen imported reports remain unchanged. Friendly labels applied to register, details, ledger, exports, calculation settings and main actions, preserving field keys and reference workbooks.

Files: Application/WorkflowContracts.cs; Infrastructure/ReportWorkflow.cs; Desktop/new PaymentsWindow.cs, WorkflowWindows.cs, LiveMainWindow.cs, ExcelFields.cs, Ui.cs, LiveChecks.cs; WorkflowTests.cs; Docs/Workflow-Guide.md; Docs/Testing/2026-10-28/PaymentClarity/. Main executable rebuilt in bin/Debug/net10.0-windows as UI 2026.10.28.7; no ZIP created and company database not edited.

Verification: normal/isolated builds zero warnings/errors; 54 backend tests passed; four final focused payment/Excel/PDF checks passed; 42 desktop acceptance checks passed; live self-test verifies saved invoice edits update both report views and automatic summary/over-allocation/footer placement; main executable self-test run. Rendered 1180x700 and 1000x600 payment layouts inspected. Earlier UI-test failure due to unshown Owner window was corrected by attaching only an existing native owner; subsequent self-test passed. No interactive mouse/printer/multi-monitor checks claimed. Next: reopen main app and use Payments / unpaid invoices on the existing TEST customer; see test report for dated preview and protected-history limitations.

## Work session — 2026-10-28 — restore Account Details names

User requested the previous names on the pictured Account Details page. Restored the 14 original workbook headings (including source spelling) in Desktop/ExcelFields.cs and Infrastructure/ReportWorkflow.Columns so Account Details grid, new exports and previews agree. Restored the main page's prior Party/Create party, Opening USD/As of, Recalculate/Reload/Delete/Comfortable rows, Report setup/Receipts/Reminders and Operator reports labels. Invoice Register and Debit/Credit field headings, redesigned payment dialog, automatic summaries, scrolling and invoice-to-report synchronization are preserved. No calculations, schema or company records changed. Build label UI 2026.10.28.8. Files: ExcelFields.cs, ReportWorkflow.cs, LiveMainWindow.cs, Ui.cs. Isolated Desktop build passed with zero warnings/errors; desktop acceptance evidence is under Docs/Testing/2026-10-28/RestoredLabels. Main copy update requires the user to close the running app; confirmation requested. No ZIP created.

Handoff update: user confirmed the app was closed. Rebuilt the normal main executable successfully (zero warnings/errors). All 42 desktop acceptance checks passed. Reopen the usual main app to load UI 2026.10.28.8 with restored labels.

## Work session — 2026-10-28 — diagnose saved invoices missing from reports

User reported saved party invoices not appearing correctly in Account Details and Debit/Credit. Read-only main DB inspection found Crescent TEST party had six saved invoices but cutoff 2026-10-27; the two added invoice dates were 2026-11-20. Both reports filtered them historically. No missing source dates/amounts were found. The user confirmed the main app was closed before update.

Added Application ReportCoverage and Infrastructure Coverage/SetReportDate. Main UI displays excluded-invoice warning/counts and Show all saved invoices action, persists explicitly selected report-date metadata without altering source invoice inputs, and prompts after Save when later invoices would be excluded. Accepting updates the shared report date; declining keeps historical filtering and the visible action. Returning from reports to register retains the cutoff. Names/calculations, receipt-history safeguards and other parties remain unchanged.

Files: Application/WorkflowContracts.cs, Infrastructure/ReportWorkflow.cs, Desktop/LiveMainWindow.cs and Ui.cs, WorkflowTests.cs; Docs/Testing/2026-10-28/ReportCoverage/. Build UI 2026.10.28.9. Normal main and isolated Desktop builds zero warnings/errors. Live self-test passed with future-invoice exclusion/warning, explicit include-all across both views, preserved/persisted date, and save-time accepted/declined date advance. Targeted backend and desktop acceptance evidence is retained in the test directory. No ZIP created or company invoice records modified. Main database cutoff was not silently changed; user can click Show all saved invoices to include the two future-dated test entries. Next: reopen main app and verify that dated view; retain historical date rules. No physical mouse/printer acceptance claimed.

## Work session — 2026-10-28 — invalid dates, Tab focus and grid instructions

User reported typing an impossible day (54-04-2007), requested automatic cell focus on Tab, and removal of the bottom-left grid description. User saved valid work and closed the main app before update. DateCellEditor now validates immediately with red border and an explanatory tooltip. Invalid dates stay visible for correction rather than being silently converted. Tab/commit is blocked for an invalid date; blank optional dates and real calendar dates normalize normally. Pasted ranges containing invalid dates are rejected atomically before any cells change.

ExcelGrid now handles Tab/Shift+Tab across editable columns in displayed order, skips calculated columns, scrolls the target into view, enters edit mode and focuses/selects its editor; row-end Tab wraps to the next visible row. Removed permanent typing/date-format and read-only instruction text from the bottom-left. Transient validation/paste feedback appears above the grid only when needed; normal save status is preserved.

Files: Desktop/DateCellEditor.cs, ExcelGrid.cs, Ui.cs and AcceptanceChecks.cs; evidence Docs/Testing/2026-10-28/DateTabFix/. Build UI 2026.10.28.10. Main Debug and isolated Desktop builds passed with zero warnings/errors; 45 desktop acceptance checks passed, including day 54, February 31, valid leap day, clearing dates, invalid-date Tab retention, forward/backward navigation skipping formulas, next-row wrapping and atomic invalid-date paste. Live self-test passed. Main executable rebuilt in place; no ZIP, workbook, financial rule, schema or company database changes. Physical keyboard interaction on the user's display remains a manual acceptance check. Next: reopen the main app, correct/clear any unsaved invalid date and test Tab/Shift+Tab in the invoice register.

## Work session — 2026-10-28 — simplified toolbar and party presentation

User requested removal of Recalculate, As of and Comfortable rows, a redesigned Party dropdown, and removal of the repeated party-name column in favour of the party beside Invoice register. User saved/closed the app before replacement. Removed those controls plus date-coverage warning/advance prompts from the main UI. Added queued automatic calculations, fixed compact row spacing and all-saved-record main report/preview/export mode so hidden cutoff metadata cannot exclude saved invoices. Prepared date remains today; existing historical backend methods, receipt dates, source records and submitted snapshots are preserved. Main invoice overdue arithmetic is recalculated using today on load/edit.

Added PartySelectorStyle (rounded white selector, clear arrow, highlighted dropdown rows, type-to-select) and selected-party headings with ellipsis/full-name tooltips. Invoice Register filters F1 out of its visible grid only; saved party identity is preserved and inherited on new rows. Unassigned rows receive their explicit temporary workspace name until assignment. Original source field keys, other worksheet columns and financial mappings remain unchanged.

Files: Desktop/LiveMainWindow.cs, App.xaml, Ui.cs, ReportPaginator.cs; Infrastructure/ReportWorkflow.cs and ReportFiles.cs; WorkflowTests.cs; Docs/Workflow-Guide.md and Docs/Testing/2026-10-28/SimplifiedToolbar/. Main build UI 2026.10.28.11, rebuilt bin/Debug/net10.0-windows; no ZIP created or working database modified.

Verification: normal/isolated Desktop builds zero warnings/errors; 45 desktop acceptance checks passed; main live self-test passed with automatic calculation, hidden party field, heading, party switching, both all-record report tabs and future invoice inclusion checks. Targeted all-record export/preview parity test passed (test fixture corrected to use Owner for preview). Rendered layout inspected at 1100x720 and larger. Historical APIs remain tested separately; operational reminder/receipt effective-date rules remain intact. No physical dropdown/mouse/printer acceptance claimed. Next: reopen main app, select a party and enter invoices; calculations and generated views need no manual recalculate or cutoff choice.

## Work session — 2026-10-28 — compact party selector and right-aligned controls

User requested removal of Assign worksheet beside the party selector, a slightly shorter selector with a more professional icon, right alignment for New terms/Reload/Delete, and removal of the cell-address/formula bar. Removed Assign worksheet from the displayed heading (existing backend assignment code/data preserved). Narrowed selector from 280 to 230 pixels, reduced minimum height to 32, and replaced the text arrow with a small rounded vector chevron. Right-aligned the options group. Removed the reusable ExcelGrid address/value/formula strip and its selection bindings; grid input, automatic calculations and keyboard navigation remain intact.

Files: Desktop/LiveMainWindow.cs, App.xaml, ExcelGrid.cs, Ui.cs; build UI 2026.10.28.12. User confirmed app was closed, then the normal main Debug executable was rebuilt successfully with zero warnings/errors. Isolated build also passed; 45 desktop acceptance checks and live self-test passed. Inspected rendered 1100x720 layout: Assign worksheet and formula/address bar absent, smaller selector/chevron visible, controls correctly right-aligned. Evidence: Docs/Testing/2026-10-28/CompactHeader/. No database, financial calculation, source workbook or saved record changes; no ZIP created. Physical dropdown interaction remains a manual acceptance check. Next: reopen the usual app to load the updated UI.

## Work session — 2026-10-28 — move report/payment actions into upper toolbar

Moved Report setup, Export Excel, Preview / Print, Receipts and Reminders into the existing upper options row. Combined layout uses a two-column Grid: workflow actions on the left, New terms/Opening USD, Reload and Delete on the right. The former second action row is removed; workflow buttons can wrap at smaller widths. No actions, calculations or records changed. Files: Desktop/LiveMainWindow.cs and Ui.cs. Main app was not running; normal Debug build succeeded with zero warnings/errors as UI 2026.10.28.13. Acceptance/render evidence: Docs/Testing/2026-10-28/SingleToolbar/. No ZIP or database changes. Next: reopen the normal app to see the single toolbar.
Verification update: all 45 desktop acceptance checks passed; inspected the rendered 1100x720 layout and confirmed all five actions share the upper toolbar, with New terms/Reload/Delete right-aligned.

## Work session — 2026-10-28 — useful shortcuts, save feedback and confirmed deletion

User requested useful shortcuts, save-button review, removal of shaded-columns text, and confirmation before record deletion. User saved/closed the main app before update. Kept Save in the consistent top-right location rather than crowding the already full action toolbar; added pending/saved/read-only states, Ctrl+S label, tooltips and a reserved width. Added 14 shortcut registrations with F1/Shortcuts help: save, new row, new party, confirmed delete, search, reload, party chooser, three tabs, export, preview/print and receipts. Repeated global keydown events are ignored; native Tab/copy/paste/text deletion remain intact. Removed editable-grid shaded-columns statement while retaining readonly identification.

Delete toolbar and Ctrl+Shift+Delete now share an explicit record/party modal with No as default. Cancel leaves data untouched. Confirmed removal remains a draft until Save; Reload can discard it. Added backend EnsureInvoiceCanDelete preflight for payment-history records, retaining storage guard. No row-delete is bound to plain Delete. Added test seams only for automated confirmation outcomes; production uses the modal.

Files: Desktop/LiveMainWindow.cs, ExcelGrid.cs, Ui.cs and LiveChecks.cs; Infrastructure/ReportWorkflow.cs; WorkflowTests.cs; Docs/Testing/2026-10-28/Shortcuts/. Main build UI 2026.10.28.14. Verification: isolated build zero warnings/errors, 45 desktop acceptance checks passed, live self-test passed including Ctrl+S/new-row/tab shortcuts, save labels, cancelled/confirmed deletion and save-only persistence. Backend deletion preflight test passed. Rendered 1100x720 layout inspected. Normal main build/self-test also run for final update. An initial unassigned test-hook compiler warning was resolved by adding its exercised test path; no production warning suppressions added. No company database, schema, workbook or existing record changed and no ZIP created. Physical keyboard/mouse/printer checks remain manual. Next: reopen the app and press F1 for the full shortcut list.

## Work session — 2026-10-29 — Delete key, row-number selection and branded confirmation

Changed record-delete shortcut from Ctrl+Shift+Delete to Delete while the worksheet grid is active. Other input controls retain native Delete behavior; in-grid text editing can use Backspace. Updated F1 help/tooltips and shortcut tests. Added row-header mouse handler selecting all visible columns of the clicked record while preserving cell-selection navigation. Calculated-cell backgrounds now reveal the selection highlight consistently. Invalid pending date edits still block changing records until corrected/cleared.

Added ConfirmDeleteWindow with TradeVault styling, record/party card, Keep record default/cancel action and red Delete record action. X/Escape cancel. Toolbar and Delete shortcut share it. Cancelling changes nothing; confirmation removes only the selected draft row, with Save required to persist. Cancelled edit transactions are cleared only after confirmation before row removal. Payment-history preflight/storage guards remain intact; no bulk deletion introduced.

Files: Desktop/ExcelGrid.cs, LiveMainWindow.cs, new ConfirmDeleteWindow.cs, Ui.cs, AcceptanceChecks.cs and LiveChecks.cs; evidence Docs/Testing/2026-10-29/RowDelete/. Main build UI 2026.10.29.1. Main app was not running during replacement. Isolated/normal builds zero warnings/errors; 46 desktop acceptance checks passed; isolated live self-test passed with Delete cancellation/confirmation and save-only persistence. Main executable self-test run for final verification. Rendered dialog inspected at 560x390. No company database, schema, source workbook or financial rule modified and no ZIP created. Physical mouse/keyboard acceptance remains manual. Next: reopen app, click a row number, press Delete and review the confirmation.

## Work session — 2026-10-29 — compact modal, immediate deletion, save/header polish

User requested less whitespace in confirmation, removal of Shortcuts navbar item, improved Save appearance, immediate deletion persistence, and removal of Offline/USD branding from header/login. User confirmed the running main app was saved and closed. ConfirmDeleteWindow now sizes to content with compact margins/card; confirmation text explains immediate saved removal. Main removes Shortcuts navigation while preserving F1/keyboard shortcuts. Added SaveActionButton styling with save/check icons, blue pending state and readable outlined Saved state. Simplified main brand to TradeVault with role chip; removed Offline/USD descriptor and login OFFLINE badge/hint. Caption text no longer displays debug suffix, while main footer retains build identification.

Added revision-checked WorkbookStore.DeleteWorksheetRecord and WorksheetController.DeleteImmediately. Only the confirmed record is removed from stored snapshot; UI removes it after storage succeeds, recomputes and preserves other pending edits/dirty state. No unrelated draft inputs are saved. Reload and app reopening cannot restore a deleted saved row. Payment-history storage guard and preflight remain. Failures keep the row present. Unsaved draft deletion does not invent a stored record. Confirmation Cancel/Escape/X remain non-destructive.

Files: Infrastructure/WorkflowStorage.cs; Desktop/WorksheetController.cs, LiveMainWindow.cs, ConfirmDeleteWindow.cs, LoginWindow.cs, App.xaml, Ui.cs and LiveChecks.cs; WorkflowTests.cs; Docs/Workflow-Guide.md; Docs/Testing/2026-10-29/ImmediateDelete/. Main build UI 2026.10.29.2 rebuilt in bin/Debug/net10.0-windows. No ZIP or company database modifications.

Verification: isolated/normal builds zero warnings/errors; 46 desktop acceptance checks passed; isolated/main live self-tests passed with immediate deletion, reload, and retaining unsaved edits in another row. Backend targeted test passed for persistence/reopen, stale revision rejection and protected-record rollback. Initial fixture reopen error was corrected to respect the existing folder lease; production locking unchanged. Inspected natural-height dialog, 1100x720 main layout and login screenshots. Physical mouse/keyboard/multi-monitor/printer acceptance remains manual. Next: reopen the main app; confirmed Delete now saves immediately, ordinary edits still use Save changes.

## Work session — 2026-10-29 — separate cell clearing from record deletion, add cut and refine navigation

User requested confirmation only for whole-row deletion, Ctrl+X for cells/records, cleaner party creation and modern tab icons/type. User saved/closed main app before replacement. Delete dispatches by selection: editable cells clear without a modal; selecting all columns of one record requests confirmation and immediate saved removal. Native text Delete/Ctrl+X retained while editing a text box. Locked calculated cells/read-only reports cannot be cleared/cut. Selection classification uses explicit item/column identity rather than WPF owner-sensitive DataGridCellInfo equality.

Added Ctrl+X for rectangular editable ranges (write clipboard before clear) and whole records (confirm, copy, then protected atomic removal). Cancelled record cuts leave data and clipboard unchanged. Private bounded JSON clipboard metadata retains invoice terms on matching in-app row paste; party/new identity stays with destination workspace and calculated cells recalculate. Clipboard failures do not clear input. Updated F1 help and tests.

Restyled Create party as outlined New party with person-add vector icon. Added matching vector icons for main tabs and clean Segoe UI Variable/Segoe UI fallback typography to tabs, headings, toolbar and grids. Kept field names, existing mappings/financial rules and saved records intact.

Files: Desktop/ExcelGrid.cs, LiveMainWindow.cs, ConfirmDeleteWindow.cs, Ui.cs, App.xaml, App.xaml.cs, AcceptanceChecks.cs and LiveChecks.cs; Docs/Workflow-Guide.md; Docs/Testing/2026-10-29/CellCut/. Build UI 2026.10.29.3 rebuilt in the normal Debug location; no ZIP or company database changes. AutomatedRun only skips close prompts for isolated self-test/qa-report/render-ui shutdown, preventing failed automation from hanging; ordinary user close prompts remain.

Verification: normal/isolated builds zero warnings/errors; 47 desktop acceptance checks passed; isolated/final main live self-tests passed for single-cell no-confirm behavior, Ctrl+X, cancellation, clipboard atomicity, formulas locked, row cut persisted, party isolation and navigation. Initial selection identity issue was fixed and retested; stale synthetic tests were stopped rather than interacting with user data. Rendered 1100x720 layout inspected. Native OS clipboard/mouse/keyboard interaction remains manual acceptance; tests use injected clipboard writes. Next: reopen the app, click a cell for clear/cut or a row number for whole-record actions; Ctrl+V pastes cut data.

## Work session — 2026-10-29 — record count beside search and compact New party dialog

User requested moving the record-count label from beside New party to the right side of the Search records bar and removing excess whitespace from the create-party dialog. Moved count rendering into reusable ExcelGrid search tools; it excludes spare rows, updates on add/delete/reload and shows filtered versus total counts when searching. Main heading no longer includes the count, leaving party selection/New party clean. Source keys, records and calculations unchanged.

PartyDialog now uses the app caption and natural content height (440-pixel width), concise explanatory text, labelled Party name input, Cancel/Create party actions aligned right, hidden-until-needed inline errors, Enter default and Escape cancel. Assignment variant retains existing validation/selection behavior. Removed irrelevant Excel-column explanation and reserved empty error space.

Files: Desktop/ExcelGrid.cs, LiveMainWindow.cs, PartyDialog.cs, Ui.cs and LiveChecks.cs. Main build UI 2026.10.29.4, rebuilt normal Debug app in place while not running. Verification: isolated/main builds zero warnings/errors; isolated live self-test passed; 47 desktop acceptance checks passed; rendered new-party natural-height dialog and 1100x720 search/count layout inspected. Evidence: Docs/Testing/2026-10-29/PartyDialog/. No database, financial rule, schema or company record changes; no ZIP. Physical interaction remains a manual acceptance check. Next: reopen usual app and use New party; counts now sit at the search bar's right edge.

## Work session — 2026-10-29 — restructured sign-in page

Restyled authentication as a spacious 860x550 two-column window, with navy branded workspace overview and a focused white sign-in form. Minimum size is 740x510, bounded by available desktop dimensions; form can scroll at constrained heights. Retained visible window controls, keyboard default sign-in, initial username focus, password reveal and recovery action, asynchronous busy/error states and existing application-service authentication. No new authentication or persistence logic. Removed excessive single-column blank space through balanced composition, concise feature descriptions and consistent typography. Corrected AuthTextBox content-host alignment and vertical padding after rendered inspection revealed clipped username text.

Files: Src/TradeVault.Desktop/LoginWindow.cs, App.xaml, Ui.cs and LiveChecks.cs. Main Debug app rebuilt in place as UI 2026.10.29.6. Existing uncommitted work preserved; no running main process detected before build. Verification: build zero warnings/errors; 47 acceptance checks passed, live persistence/authentication/worksheet self-test passed. Final text-padding adjustment rebuilt and screenshots rerendered; username is fully visible. Evidence: Docs/Testing/2026-10-29/LoginRefresh/. Rendered 860x550 and 740x510 layouts reviewed. Physical keyboard, password-recovery interaction and multi-monitor/DPI acceptance remain manual. No company database changes or ZIP. Next: reopen the usual app to see the new authentication layout.

## Work session — 2026-10-29 — authentication-only redesign

Removed the entire descriptive left panel at the user's request. TradeVault logo/title now sit at the upper left in a white brand header. Retained 860x550 startup dimensions and existing minimum sizes; centered a compact white authentication card on a pale background with username, password reveal, recovery and Sign in. Removed feature descriptions, account-access eyebrow and role explanation. Retained concise case-sensitivity hint, inline errors, busy state, keyboard focus/default action and existing authentication/recovery services. Scrollable content accommodates constrained heights.

Files: Src/TradeVault.Desktop/LoginWindow.cs and Ui.cs. UI 2026.10.29.7; main Debug app rebuilt after user confirmed closure. Verification: isolated build zero warnings/errors; 47 acceptance checks passed; rendered login at 860x550 and minimum 740x510; default render visually inspected. Evidence: Docs/Testing/2026-10-29/LoginClean/. No database/authentication-rule changes, company data edits or ZIP. Physical interaction and DPI checks remain manual. Next: reopen main app for updated sign-in.

Follow-up verification — 2026-10-29: Confirmed authentication-only redesign is present in main source/output (UI 2026.10.29.7), with left-aligned branding and no descriptive panel. Rebuilt main Debug executable with zero warnings/errors and reviewed LoginClean login render. No additional UI or data changes required; existing session entry preserved. Verification rerun uses isolated synthetic records. Next: reopen normal app; physical interaction remains manual.

## Work session — 2026-10-29 — remove authentication card surround

Removed the fixed-width floating card, rounded outer border and grey surrounding canvas at the user's request. Sign-in now uses the page width beneath left-aligned TradeVault branding, with consistent 28px side margins and a single white surface. Retained window dimensions, input labels, recovery/reveal controls, inline error handling and existing authentication services. User confirmed app closure before main replacement.

Files: Src/TradeVault.Desktop/LoginWindow.cs and Ui.cs. Main Debug app rebuilt in place as UI 2026.10.29.8. Verification: isolated/main builds zero warnings/errors; 47 acceptance checks passed. Rendered login inspected at 860x550; minimum-size render also generated. Evidence: Docs/Testing/2026-10-29/LoginFlat/. No company data, authentication rules or dependencies changed; no ZIP. Physical interaction and DPI verification remain manual. Next: reopen usual app for the page-width login layout.

## Work session — 2026-10-29 — normal sign-in width

Reduced login startup width from 860 to 480px (400px minimum), and height from 550 to 490px (460px minimum). Retained single white surface, left-aligned TradeVault branding, 28px margins, labelled inputs and scrolling for constrained height/error messages. Authentication, recovery and main workspace dimensions unchanged. User confirmed app closure before replacement.

Files: Src/TradeVault.Desktop/LoginWindow.cs, LiveChecks.cs and Ui.cs. Main Debug app rebuilt as UI 2026.10.29.9. Isolated/main builds passed with zero warnings/errors. Updated default/minimum render dimensions; default screenshot visually inspected in Docs/Testing/2026-10-29/LoginNormal/Screenshots/. No new behavioral tests for this sizing-only change. No data changes or ZIP. Next: reopen the normal app; physical DPI/window interaction remains manual.
