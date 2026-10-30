<p align="center">
  <img src="Src/TradeVault.Desktop/Assets/TradeVault.png" width="76" alt="TradeVault logo">
</p>

<h1 align="center">TradeVault</h1>

<p align="center">
  <strong>Export records. Customer payments. Clear party accounts.</strong><br>
  An offline Windows desktop application designed for independent Owner and Operator USB workspaces.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows-0078D4" alt="Platform: Windows">
  <img src="https://img.shields.io/badge/.NET-10-512BD4" alt=".NET 10">
  <img src="https://img.shields.io/badge/UI-WPF-2458D3" alt="WPF desktop interface">
  <img src="https://img.shields.io/badge/storage-SQLite-003B57" alt="SQLite local storage">
  <img src="https://img.shields.io/badge/currency-USD-247653" alt="USD accounting">
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> ·
  <a href="#screenshots">Screenshots</a> ·
  <a href="#operator-to-owner-handover">Excel handover</a> ·
  <a href="#database-and-local-storage">Database</a> ·
  <a href="#testing-and-current-status">Testing</a> ·
  <a href="#troubleshooting">Troubleshooting</a> ·
  <a href="#documentation">Documentation</a>
</p>

## Overview

TradeVault replaces scattered export-tracking worksheets with party-specific invoice registers, shipment records, payment allocations and account reports. It is built around a company exporting dyes from India, including shipments routed through Malaysia.

The application runs locally without an application server or internet connection. Owner and Operator use separate application folders and databases. The Operator exports an Excel report; the Owner imports a frozen copy for review. Working databases are independent.

**Current source build:** `UI 2026.10.29.3` · **Database schema:** `5`

### Recent changes reflected in this documentation

- **Customer payments:** a dedicated screen shows unpaid bank/difference portions, editable payment allocations, live totals, advances and payment history.
- **Reporting:** the main UI no longer exposes As of, Recalculate, row density or Selected terms. Generated tabs, previews and main Excel exports include **all saved records**, including future-dated entries. Reminders and payment-entry balances still use their own effective dates.
- **Editing:** clearing/cutting cells remains a draft operation. Confirmed whole-record deletion/cutting saves that one removal immediately, preserving unrelated draft edits.
- **Navigation:** New party, vector tab icons, keyboard shortcuts and compact headers replace earlier layouts. Invoice register labels are clearer; internal field keys and stored records remain compatible.

Current instructions in this README take precedence over older dated work logs and screenshots. The complete earlier README remains in the expandable development history below.

## Features

| Area | What is implemented |
|---|---|
| Authentication | Predefined Owner/Operator accounts, case-sensitive sign-in, password visibility toggle and offline single-use recovery codes |
| Party workspaces | Separate registers, balances and report settings per party; selected party inherited by new invoices |
| Excel-style entry | Keyboard navigation, search, frozen identifiers, copy/cut/paste, row selection and protected calculated cells |
| Shipment tracking | Proforma/invoice references, containers, BL dates, arrival dates, shipping and payment terms |
| Payments | Partial receipts, one payment allocated across invoices, bank/difference portions, unallocated advances and dated corrections/reversals |
| Reports | Account Details and Debit & Credit derived from saved records; automatic recalculation and current all-record views |
| Reminders | Party and invoice references, outstanding amounts, due-soon/overdue status and settled/BL-pending cases |
| Excel handover | A readable `.xlsx` filename, two report tabs, automatic opening after export and Owner import/preview/review |
| PDF and print | Direct paginated PDF output plus Windows printing, with PDF fallback when Windows printing fails |
| Data safeguards | Transactional SQLite saves, stale-revision protection, date/USD validation, backups, verified restore and migrations |

All financial amounts are **USD**. New invoices default to **90 calendar days** from the Malaysia-to-Pakistan BL date and **USD 3,800** shipping; defaults are editable. Changing new-record terms does not rewrite existing invoices.

## Screenshots

Screenshots below use **synthetic QA data**, not company transactions.

### Invoice workspace

![TradeVault invoice register with synthetic records](Docs/Testing/2026-10-29/CellCut/Screenshots/form.png)

<details>
<summary>View sign-in and generated account report</summary>

![TradeVault sign-in with password visibility control](Docs/Testing/2026-10-29/CellCut/Screenshots/login.png)

![Generated Account Details report using synthetic data](Docs/Testing/2026-10-29/CellCut/Screenshots/generated-details.png)

</details>

## Quick start

### Development prerequisites

- Windows with the **.NET 10 SDK**.
- Git, if cloning or collaborating on the repository.
- Internet access for the initial dependency restore. Normal application use is offline.

From the repository root:

```powershell
dotnet restore TradeVault.sln
dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --no-restore
dotnet run --no-restore --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj
```

Close the running application before rebuilding; Windows locks application files while they are in use.

The normal Debug executable is created at:

```text
Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.exe
```

### First use

1. Sign in with the provisioned Owner or Operator account. Usernames are case-sensitive: `owner` and `operator`.
2. Create and select a named party using **New party**.
3. Complete **Report setup** for that party: select the actual contract/receipt sources, opening balances and shipping treatment. These business meanings are not guessed automatically.
4. Enter invoice and shipment records. Calculated values refresh automatically; save ordinary edits with **Save changes** or **Ctrl+S**.
5. Open **Receipts** to allocate customer payments. Use **Reminders** for follow-up, or open the report tabs to review balances.
6. Export Excel or use **Preview / Print** for report output.

Keep account passwords outside the repository documentation. Save a recovery code separately before relying on **Forgot password**; see the [offline recovery guide](Docs/Offline-Account-Recovery.md).

### Report setup and calculation rules

Select a named party before opening **Report setup**. Settings are per party, not shared between USB installations.

| Setting | Meaning |
|---|---|
| Contract source | Choose the actual bank-contract amount source: our customs amount (`F6`) or Pakistan customs amount (`F7`). These amounts are not assumed to mean the same thing. |
| Earlier bank receipts source | Choose MY → PK receipts (`F22`, dated `F24`) or Nemichem receipts (`F25`, dated `F26`). |
| Earlier difference receipts | The generated workflow uses `F23`, dated `F24`, for earlier difference payments. |
| Shipping treatment | When enabled, deduct each invoice row's registered shipping amount once on its invoice date. When disabled, generated balances exclude this deduction. |
| Opening balances | Enter bank and difference amounts outstanding before the invoices being entered, together with their effective date. Use zero if there is no earlier outstanding balance. |

The company must confirm the source choices. Once a party has recorded receipt history, mappings cannot be changed through ordinary setup. Do not duplicate an outstanding amount in both opening balances and entered invoices.

The generated calculation uses:

```text
Difference before shipping = Original invoice amount − Selected contract amount
Net difference             = Difference before shipping − Approved shipping deduction
Bank unpaid                = Contract amount − Earlier bank receipts − Allocated bank payments
Difference unpaid          = Net difference − Earlier difference receipts − Allocated difference payments
Invoice unpaid             = Bank unpaid + Difference unpaid
Due date                   = Malaysia → Pakistan BL date + Invoice terms in calendar days
```

An unallocated advance reduces the party's ledger balance but does not settle any particular invoice. Opening balances are not aged against individual invoices. Generated report tabs are read-only; update their saved register/payment sources instead.

Before setup is complete, independently entered Account Details and Debit & Credit worksheets may still be displayed. They are preserved data, not automatically reconciled generated reports. The old manual-report toggle has been removed.

### Recording, correcting and cancelling payments

1. Save the party's invoice records and complete Report setup.
2. Open **Receipts** (`Ctrl+Shift+R`) to enter **Customer payments**.
3. Enter the date money was received, the total in USD and a description/bank-transfer reference.
4. Review the unpaid balances at that date. Enter amounts only in **Pay bank portion** and **Pay difference portion** for the invoices being paid.
5. Check the automatically updated assigned amount, unassigned advance and unpaid-after-payment amounts.
6. Choose **Save new payment**. This saves the payment directly and refreshes balances; it is separate from saving worksheet drafts.

Example: two invoices each have USD 10,000 bank unpaid and USD 1,200 difference unpaid. For a USD 8,000 transfer, assign USD 6,000 to invoice A's bank portion and USD 2,000 to invoice B's bank portion. A then owes USD 5,200 in total and B owes USD 9,200. If only USD 7,500 is assigned, USD 500 remains an advance.

Open **Recorded payments** to select an active payment. **Edit selected payment / assign its advance** opens correction mode; choose the date the correction takes effect and save it. The original is retained with reversal/replacement movements. **Cancel recorded payment** requires an effective date and reason; it reverses the recorded allocations and does not initiate a bank refund.

Do not record the same money twice: the register's **Earlier receipts** fields are cumulative starting amounts, while this screen records additional dated payments. Future invoices are excluded from the payment-date balance list. Pre-invoice money can be saved as an unallocated advance and assigned later through correction.

### Reminders and report dates

**Reminders** shows the selected party's invoice references, due dates, unpaid USD amounts, overdue days and statuses: Settled, BL pending, Not yet due, Due within 30 days, or Overdue. A missing Malaysia → Pakistan BL date leaves the due date unknown. Same-day payments are valid; overdue is determined from the due date, not from the payment description.

Reminders use today's date. Main report tabs/exports use all saved records, so a future-dated invoice/payment can appear in a report while being absent from today's reminders or an earlier payment-date balance list. A report's prepared date is **not** a transaction cutoff. The date-filtered `ReportWorkflow.Build` service remains available internally; the current main UI calls `BuildCurrent`.

### Validation and save behavior

- Party and invoice references are required; invoice references and stable IDs must be unique within the party.
- USD amounts must be non-negative, within the supported limit of USD 1 trillion per entered amount, and have at most two decimal places.
- When dates are supplied, proforma/invoice/BL/arrival dates must follow their shipment sequence. A payment allocated to an invoice cannot predate its invoice; positive earlier receipt amounts require their effective payment dates.
- Payment allocations must belong to the selected party, fit the receipt total and not exceed their bank/difference components at any effective-date checkpoint.
- Payment terms must be 0–3650 calendar days and cannot produce a date beyond the supported range.
- Concurrent/stale worksheet revisions are rejected. Invoices with receipt history cannot be deleted, and protected historical amount/date inputs cannot be overwritten by ordinary editing.

Required-field/business checks run when saving/posting, rather than displaying a missing-field warning after every cell edit. Failed saves leave stored records unchanged. Unknown shipment dates can remain blank; a new invoice need not have completed its entire shipment lifecycle to be entered.

### Editing and deletion

- **Delete** on editable cells clears the selection without a confirmation dialog. These changes remain drafts until saved.
- Click a row number to select a whole record. **Delete** then requests confirmation and saves that record's removal **immediately**.
- **Ctrl+X** cuts editable cells, or requests confirmation when cutting a whole record. Clipboard failure leaves the input unchanged.
- **Ctrl+V** pastes inputs; calculated columns are recalculated. A copied invoice needs a new unique reference before saving.
- Records linked to payment history are protected from deletion. Confirmed record removal does not save unrelated draft edits.

The main toolbar no longer exposes manual **As of**, **Recalculate** or row-density controls. Main reports include all saved entries, including future-dated ones; the prepared date is not a historical cutoff. Reminder aging uses today's date, and receipt/correction dates still control payment validation. Historical reporting services remain available internally.

### Useful shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+S` | Save changes |
| `Ctrl+N` | Move to a new editable record |
| `Ctrl+Shift+N` | Create a party |
| `Ctrl+L` | Choose a party |
| `Ctrl+F` | Search the current worksheet |
| `F5` | Reload saved records, with unsaved-change protection |
| `Ctrl+1` / `Ctrl+2` / `Ctrl+3` | Invoice register / Account Details / Debit & Credit |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | Copy / cut / paste |
| `Delete` | Clear selected cells or confirm whole-record deletion |
| `Ctrl+E` | Export Excel reports |
| `Ctrl+P` | Open report preview / print |
| `Ctrl+Shift+R` | Open customer payments |
| `F1` | Open the complete shortcut guide |

Native text editing behavior is retained while a text editor has focus.

## Operator-to-Owner handover

```mermaid
flowchart LR
    A[Operator USB: save records] --> B[Export Excel workbook]
    B --> C[Copy workbook to Owner USB]
    C --> D[Owner: preview and import]
    D --> E[Review frozen report]
```

1. The Operator selects a party, saves changes and chooses **Export Excel**.
2. TradeVault creates a workbook containing **Account Details** and **Debit Credit** tabs, then asks Windows to open it in the installed spreadsheet application.
3. Copy the original workbook to the Owner's USB using a computer that can access both drives.
4. The Owner opens **Operator reports → Import report**, previews the file and confirms import.
5. The Owner reviews the frozen submission and records a review status/note. Revised exports retain earlier imported revisions.

Export names contain the party, report date and revision, for example:

```text
Meridian - Account Reports - 2026-10-29 - Revision 1.xlsx
```

Use the **original TradeVault-exported workbook** for import. Arbitrary spreadsheets and edited/resaved copies are not supported as submission inputs. Import validates report contents and preview-to-import identity. No `.tvreport` handover is required, and importing does not overwrite the Owner's working invoices.

## Portable USB deployment

Publish a self-contained Windows folder:

```powershell
.\Src\TradeVault.Desktop\Publish-Usb.ps1 -Runtime win-x64
```

The script also supports `win-arm64`. Initial publishing may download the corresponding runtime packages.

Copy the **entire** published folder from `Src/TradeVault.Desktop/bin/Portable/win-x64/` to each USB and launch `TradeVault.Desktop.exe`. The self-contained package bundles .NET; the client should not need to install the SDK. This is an application launched from USB, not a bootable operating system.

```text
TradeVault/
├── TradeVault.Desktop.exe
├── ... application and runtime files
├── Data/
│   └── tradevault.db
├── Reports/
└── Backups/
```

Paths are resolved relative to the running executable, so each USB keeps its own database. During updates, close the application, create a verified backup and preserve `Data/` and `Backups/`. Do not copy development databases or QA evidence into the client package. Close the application before ejecting the drive.

## Architecture

```text
Src/
  TradeVault.Desktop/         Windows, spreadsheet controls, PaymentsWindow and report presentation
  TradeVault.Application/     Contracts for invoices, receipts, report bundles and imports
  TradeVault.Domain/          USD/domain primitives and business exceptions
  TradeVault.Infrastructure/  SQLite, migrations, validation, reporting and Excel transfer
Test/
  TradeVault.Tests/           xUnit business, database and workflow suites
  TradeVault.Desktop.Audit/   Desktop verification wrapper
Tools/                       Build/verification helpers and icon-generation source
Docs/                        Reference spreadsheets, operating guides and dated QA evidence
```

| Layer | Responsibility |
|---|---|
| `TradeVault.Desktop` | WPF windows, spreadsheet controls, authentication UI and report presentation |
| `TradeVault.Application` | Workflow contracts and report/payment models |
| `TradeVault.Domain` | Business primitives and domain rules |
| `TradeVault.Infrastructure` | SQLite persistence, validation, receipt/report workflows, Excel transfer, backup and recovery |
| `TradeVault.Tests` | Automated domain, persistence, workflow, validation and recovery tests |
| `TradeVault.Desktop.Audit` | Wrapper for isolated desktop live/acceptance checks |

Technology: **C# · .NET 10 · WPF · Microsoft.Data.Sqlite · Open XML Excel files · xUnit**. Excel export does not require Microsoft Excel to be installed; opening a workbook requires a compatible spreadsheet application.

### Database and local storage

The database is **`Data/tradevault.db` relative to the running executable**. The normal Debug application therefore uses:

```text
Src/TradeVault.Desktop/bin/Debug/net10.0-windows/Data/tradevault.db
```

Running another build/output folder creates or uses that folder's own database. This is why a development build, a verification build and a USB installation can contain different records.

| Table | Active purpose |
|---|---|
| `schema_migrations` | Applied schema versions and migration dates |
| `app_instance` | Identity of this independent application/database, used in report lineage |
| `credentials` | Account roles and salted password hashes |
| `account_recovery` | Hashed single-use recovery codes and recovery attempt/cooldown state |
| `worksheet_parties` | Named party workspaces and the Unassigned records bucket |
| `party_worksheets` | Party + worksheet key, optimistic revision and JSON worksheet rows/metadata |
| `workflow_documents` | JSON documents keyed by kind/ID: report mapping, receipt book, exported revisions and imported submissions |
| `workflow_audit` | Actor, action, entity and before/after workflow audit data |
| `audit_events` | Additional record/account/storage events |

The schema also retains earlier normalized domain tables (`parties`, `invoices`, `receipts`, `receipt_allocations`) and the older `worksheets` table for existing backend/migration paths. Do not mistake those for the current WPF register/payment source of truth: the desktop uses `WorkbookStore`, `party_worksheets` and workflow documents. Generated report rows are derived rather than written back into the manual worksheet tables.

An exclusive `Data/.instance.lock` lease prevents two instances using the same application folder. SQLite saves are transactional with revision checks; temporary SQLite storage is configured in memory. Older supported schemas are upgraded after a pre-upgrade backup. Imported report originals/review state are stored separately from working records in the database.

### Backup and restore

Both roles can create a verified database backup through **Backups**. The Owner can restore a supported backup from that installation's `Backups/` directory. Restore validates it, creates a safety backup, replaces the database and signs out current sessions. This restores the complete database state, including accounts and imports, not a single selected invoice. Separately exported Excel/PDF files are not included in a database-only backup.

Keep a backup on another USB for protection against losing the application drive. Save and close TradeVault before manually copying a database. To troubleshoot a backup or migration issue, preserve the original files rather than editing the SQLite schema directly.

## Testing and current status

The latest recorded UI verification for `UI 2026.10.29.3` passed **47 desktop acceptance checks** and the isolated/main live self-tests. The earlier comprehensive audit recorded **53 backend tests**, **42 desktop acceptance checks**, 22 page/dialog states and independent Excel/PDF checks for build `UI 2026.10.28.6`. These are dated evidence, not a claim that tests ran automatically on every commit.

Run the backend suite:

```powershell
dotnet test Test/TradeVault.Tests/TradeVault.Tests.csproj
```

After restoring dependencies, run the build/backend/live verification script:

```powershell
.\Tools\verify_report_update.ps1 -AuditRunner
```

Generate isolated desktop acceptance evidence:

```powershell
dotnet Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.dll --qa-report .build/acceptance
```

See the [comprehensive client audit](Docs/Testing/2026-10-28/Client-Audit.md) and [latest UI evidence](Docs/Testing/2026-10-29/CellCut/).

For synthetic screenshots without using the working company database:

```powershell
dotnet Src/TradeVault.Desktop/bin/Debug/net10.0-windows/TradeVault.Desktop.dll --render-ui .build/screenshots
```

The verification script also accepts `-Configuration Release -OutputFolder .build/release-check`. Keep `.build/`, test evidence and seeded QA data out of client installations. Seeding commands modify the database beside the executable they run against; they are not ordinary startup commands.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Old UI after pulling code | Close the app, rebuild the Desktop project, then launch the executable from that rebuilt output folder. An older published folder/shortcut will retain its older binaries. |
| Build cannot replace a DLL/EXE | Save and close the running copy from that output folder, then rebuild. |
| Records appear missing | Verify which executable folder is running and inspect its `Data/tradevault.db`; builds and USBs have independent databases. |
| Application says it is already open | Close the other instance using the same folder. Do not delete its lock file while it is running. |
| Receipts/report generation unavailable | Select a named party, save current drafts and complete its Report setup. Check required invoice amounts/dates. |
| Balances differ from a previous report | Check report mode, future-dated records, selected mapping, opening balances, earlier cumulative receipts, additional payments and effective correction dates. |
| Excel import is rejected | Use the original unedited TradeVault `.xlsx`; check duplicate revisions and export a fresh revision after correcting source records. |
| Excel/PDF does not open automatically | The file can still be saved successfully; open it manually with an installed compatible application. |
| Windows printing fails | Use **Save PDF**, which generates the report without a printer driver. |
| Password forgotten | Use a previously saved recovery code. Without a password/code, follow the recovery guide's verified-backup process; there is no master password. |

### Delivery boundaries

- Verify the actual self-contained package on the client's Windows computer, including physical USB handover/ejection, clipboard/keyboard interaction, file associations and installed printer drivers.
- Report mappings and opening balances need company confirmation. Customs amounts remain separate; automatic source selection is not assumed.
- Login controls application access; it does **not encrypt the SQLite database**. Recovery has no hidden master-password bypass.
- Imported reports are frozen copies, not database synchronization or digitally authenticated sender identities.
- Direct PDF output uses rendered page images; its text is not selectable/searchable. Historical services use current invoice values filtered by effective dates, not a complete reconstruction of past invoice edits.

## Documentation

| Document | Purpose |
|---|---|
| [Workflow guide](Docs/Workflow-Guide.md) | Detailed operational notes and feature changes; earlier sections may describe superseded controls |
| [Party workspaces](Docs/Party-Workspaces.md) | Party isolation and legacy assignment context |
| [Offline account recovery](Docs/Offline-Account-Recovery.md) | Recovery-code setup, reset and backup limitations |
| [Business validation](Docs/Business-Validation.md) | Date, amount, payment and report-output rules |
| [Reports and receipts](Docs/Reports-and-Receipts.md) | Accounting examples and Excel handover; historical cutoff UI notes predate the simplified toolbar |
| [Client audit](Docs/Testing/2026-10-28/Client-Audit.md) | Findings, fixes, test coverage and remaining physical acceptance |
| [Latest UI evidence](Docs/Testing/2026-10-29/CellCut/) | Cell/row actions, navigation and screenshot evidence |

Reference spreadsheets are retained in `Docs/` for field/report alignment. Do not add real company transactions, passwords or recovery codes to Git commits, issues or screenshots.

## Development record

Every development session must update this README with the date, concrete changes, verification, limitations and next action. Preserve previous entries and distinguish completed work from proposals. Do not record credentials or company transaction details.

### 2026-10-29 — Source review and operational/technical documentation

- Inspected recent committed changes and the existing uncommitted README refresh. Confirmed the dedicated payments screen, date-specific payment balances, all-saved-record reporting, immediate confirmed record deletion, draft cell cut/clear, shortcuts and current schema/build label.
- Preserved the refreshed overview, screenshots, onboarding and complete archived development history. Added per-party setup guidance, formulas, a worked payment example, correction/cancellation/advance behavior, reminder/report date distinctions, validation/save rules and the remaining shortcuts.
- Added the repository map, active database tables/storage model, database-file location, backup/restore semantics, isolated rendering instructions and troubleshooting. Distinguished the current desktop JSON/document storage from retained normalized backend tables.
- Verification: compared documentation against source and dated QA evidence; checked local links/images, navigation anchors and preserved history. This was a documentation-only update; application tests were not rerun and no company database, binary or financial rule was changed. Earlier test counts remain explicitly dated evidence.
- Next: update this summary with future behavior changes and complete the client-machine USB/printing acceptance recorded in the audit report.

### 2026-10-29 — GitHub README refresh

- Replaced the stale opening with a current project overview, technology badges, synthetic screenshots, onboarding, keyboard shortcuts, Excel handover, USB publishing, architecture and documentation links.
- Documented current all-record reporting, immediate confirmed row deletion, draft cell clearing/cutting and explicit report mappings.
- Preserved the complete previous README below, including every development entry. Historical versions/package references are archived context, not current release instructions.
- Verification: checked current source/build label and screenshot content; validated local document/image links and Markdown structure. No application code, company database or financial behavior changed; application tests were not rerun for this documentation-only update.
- Next: keep the summary aligned with subsequent source changes and record client-machine USB/printer acceptance before delivery.

<details>
<summary><strong>Full development history — preserved previous README</strong></summary>

The following is the complete prior README. It contains superseded plans, milestone counts, controls and package formats. The current instructions above take precedence.

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
