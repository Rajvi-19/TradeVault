# TradeVault — current-state review, tests and remaining delivery plan

**Review date:** 27 October 2026  
**Source reviewed:** commit 5029364, Party Wise Feature Added  
**Application build label:** UI 2026.10.27.6  
**Environment:** Windows, .NET SDK 10.0.403, WPF, SQLite.  
**Review type:** source inspection, isolated synthetic-data execution and rendered UI review. No application logic was changed.

## 1. Overall assessment

TradeVault is now a working, party-scoped offline record-entry application. Authentication, saved worksheets, validation, party isolation and existing worksheet calculations work in the executed tests.

It is **not yet the complete original export-receivables application**. The desktop still stores three separately edited worksheets. It does not create both reports automatically from one authoritative invoice/receipt dataset, expose the normalized receipt-allocation workflow, export the required report files or import operator submissions on the Owner USB.

Party isolation is a meaningful improvement and is verified. The older audit's statements that party scoping and as-of persistence are absent are superseded. Its warnings about disconnected data models, historical reporting and the missing sharing workflow still apply.

## 2. Changes now present

| Area | Current implementation | Assessment |
|---|---|---|
| Parties | Create/select party outside workbook columns; independent worksheets, revisions, openings, terms and as-of settings | Implemented and tested |
| Legacy migration | Schema version 3; pre-upgrade backup; prior sheets retained in Unassigned records and legacy table | Migration and readable backup tested |
| Legacy assignment | Whole-sheet transactional move to an empty named-party destination; overwrite/stale/mixed-party guards | Implemented and tested |
| Desktop layout | Compact horizontal navigation, options strip, 28/36 px rows, restrained colors and source workbook labels | Rendered at normal/minimum sizes |
| Window controls | Custom WPF WindowChrome; explicit minimize/maximize/restore/close buttons in login and workspace | Present in source/render and regression checks; physical clicks not tested this session |
| Dates | DD-MM-YYYY display, masked digit entry, ISO storage, typed/pasted common formats | Automated regression checks pass |
| Validation | Field-specific errors, incomplete draft entry allowed while typing, required-field checks on Save, date overflow rejection | Regression checks pass |
| Calculation isolation | Invalid inputs in one row no longer erase independent dates in every other row; unknown running balance remains unknown downstream | Covered by regression checks and source review |
| Sorting | Numeric/date-aware comparator, spare rows after records | Previous text-only numeric sorting issue has been addressed |
| Blank sheet | 100 replenished display rows; untouched blanks are not stored | Acceptance check passes |
| Metadata | Terms/opening/as-of retained for each party and worksheet | Party UI and storage tests pass |
| Test data | Explicit sample seeder with backup; separate acceptance and audit datasets | Executed only against new isolated audit databases |

The current captions are **custom**, not the native-only caption used in the previous review. Party creation/assignment dialogs use standard fixed-size Windows dialogs. Physical title-bar behavior, DPI and multi-monitor maximize behavior still need interactive acceptance.

## 3. Test execution and evidence

### Results

| Suite | Current result | What it establishes |
|---|---|---|
| Desktop build | PASS, 0 warnings, 0 errors | Current Desktop project compiles independently |
| Desktop acceptance suite | 39 PASS, 0 FAIL | Existing authentication, storage, calculations, grid and command-handler checks |
| Desktop self-test | PASS | Existing live checks plus FixRegressionChecks and party UI-handler checks; overlapping assertions not counted as extra independent cases |
| Backend xUnit suite | 14 PASS, 0 FAIL, 0 skipped | 9 normalized-service tests and 5 party/migration tests |
| New targeted audit probes | 4 PASS, 4 GAP CONFIRMED, 0 unexpected failures | Additional real data scenarios, including limitations the ordinary suite does not reject |
| UI renders | Generated and reviewed | Login and worksheets at normal/minimum sizes; not physical input certification |

The 39 desktop and 14 backend cases are **53 passing cases**. The additional audit probes deliberately reproduce four gaps; they must not be presented as proof of complete product readiness.

### Data isolation

- Built to bin/Audit20261027 so the normal application executable and company data location were not used.
- Acceptance dataset: 12 invoice-register entries, 12 Account Details rows and 36 Debit/Credit rows, using QA-prefixed references.
- Additional named-party dataset: three [TEST] parties, 12 invoices, 12 detail rows and 39 ledger entries, with settled, partially paid, unpaid and pending-BL scenarios.
- Probe dataset also contains separately named [AUDIT] parties for duplicate references, terms scope, settlement and historical-cutoff tests.
- A second independent database root was tested to represent a separate USB copy; it did not inherit the first dataset.
- Sample seeding was invoked against a newly constructed isolated database. The production --seed-test-data command was not run against company storage.
- Synthetic audit databases are retained beneath the test project's ignored bin directory as evidence. Existing company rows and source workbooks were not edited.
- No passwords are included in report artifacts.

### Detailed artifacts

- [39 desktop acceptance cases and evidence](desktop-results.md)
- [Desktop machine-readable results](desktop-results.json)
- [14 backend test results — original TRX](backend-results.trx)
- [Eight new audit probes and observations](audit-probes.json)
- [Combined test summary](summary.json)
- [Invoice register at minimum size](Screenshots/form-minimum.png)
- [Invoice register](Screenshots/form.png)
- [Account Details](Screenshots/details.png)
- [Debit/Credit](Screenshots/ledger.png)
- [Login with synthetic username](Screenshots/login.png)
- [Operator layout](Screenshots/operator.png)

The screenshots are actual WPF control renders against synthetic data. They establish layout/content, not successful mouse clicks or OS window-state transitions.

## 4. Backend cases rerun

| Case | Result |
|---|---|
| Defaults and invoice changes persist after reopen | PASS |
| Multiple partial receipts and one receipt across invoices | PASS |
| Over-allocation rolls back the entire receipt | PASS |
| Cross-party and duplicate allocations rejected | PASS |
| Historical normalized-service balances exclude future receipts/invoices | PASS |
| Normalized-service overdue stops when paid; unknown BL has no due date | PASS |
| Stale edits, duplicate normalized invoices, reduction below allocated amount rejected | PASS |
| Invalid precision/dates/negative values rejected | PASS |
| Backup reopens and second instance is blocked | PASS |
| Party worksheets/revisions/metadata remain independent after reopen | PASS |
| Invalid/duplicate/unauthenticated party actions rejected | PASS |
| Assignment moves metadata atomically; overwrite/stale saves rejected | PASS |
| Mixed invoice ownership cannot be silently assigned | PASS |
| Version-2 migration preserves exact data and makes a readable pre-upgrade backup | PASS |

Important distinction: the normalized backend rejects duplicate invoices and handles settlement/historical queries. The worksheet UI does not call those same paths, so backend passes do not establish those behaviors in the desktop register.

## 5. New findings demonstrated with test data

### F1 — Duplicate invoice numbers can be saved in a party worksheet

**Probe P02:** two rows with invoice reference AUDIT-DUP-1 were saved and reloaded in one named party.

This can duplicate receivables or confuse reconciliation. Add a service-level uniqueness rule with a deliberate identity convention, normally party + invoice number + financial year. Define legitimate invoice/container splits rather than indiscriminately rejecting them.

Evidence: WorksheetController validates required F9 but has no duplicate-reference check. The normalized service already has a separate duplicate guard.

**Priority:** High, before relying on worksheet totals.

### F2 — Register overdue days are independent of receipt/settlement fields

**Probe P03:** sample contract 10,000, bank-received inputs 10,000, net difference 1,200 and difference received 1,200 still produced 41 overdue days as of 26 October.

Current formula is simply max(0, as-of minus due date). This is not a subtraction error: it is a missing settlement/status policy. Preserve the four amount fields independently and agree which receipts settle which receivable component before implementing the rule. Display settled/partially paid/overdue status explicitly.

Evidence: WorksheetController.cs, F21 calculation.

**Priority:** High for collections/reminders.

### F3 — As-of is not a historical report cutoff

**Probe P04:** a ledger row with BL date 1 January 2027 and debit 100 remained in a balance calculated with as-of 1 October 2026.

As-of is now persisted, which is fixed. It still does not filter worksheet movements or reconstruct historical balances. Ledger rows have unstructured particulars instead of reliable effective transaction dates for every debit/credit.

Either label the present control as the overdue-calculation date where appropriate, or introduce dated transactions and true historical reporting. Do not claim a frozen historical report until the necessary data exists.

**Priority:** High before exporting historical statements.

### F4 — Register terms are a worksheet-wide setting

**Probe P05:** changing terms to 60 changed both invoices' due dates to 16 August 2026.

A default should normally be copied to each invoice; changing a default should not revise existing contractual terms without an explicit action. The details/ledger worksheets already have row TERMS fields, and the normalized service supports per-invoice terms.

**Priority:** High if different invoice terms or historical terms must be retained.

### Other audit probes

| Probe | Observation |
|---|---|
| P01 | Three named test parties, 12 invoices, 12 detail rows, 39 ledger entries; pre-seed backup exists |
| P06 | UI controller rejects a party name that differs from the selected named party |
| P07 | A separate application root has independent empty party/worksheet data |
| P08 | Partial-receipt fixture shows USD 30,250.50 pending after USD 20,000 received against USD 50,250.50 |

## 6. Recommended changes, in priority order

| Priority | Recommended work | Reason |
|---|---|---|
| P1 | Connect UI invoice/receipt operations to agreed Application services and one authoritative model | Current worksheets and normalized backend are disconnected; entry is repeated and safeguards differ |
| P1 | Add duplicate invoice identity checks, stable row IDs and draft/posted/reversal rules | Avoid duplicate balances; make corrections and links traceable |
| P1 | Retain per-invoice terms; preserve default changes without retroactive edits | Demonstrated global-terms gap |
| P1 | Define settlement components, settlement-aware overdue and genuine as-of semantics | Demonstrated business/reporting gaps |
| P1 | Generate Account Details and Debit/Credit from the same source transactions | Original core requirement remains incomplete |
| P1 | Implement Excel report export and manual Owner submission import | Original USB sharing workflow remains unavailable |
| P2 | Add dated receipt history/allocation UI with unallocated amounts and safe corrections | Backend exists, but worksheet totals are manually accumulated |
| P2 | Add report totals, aging, date/period filters and on-open due/reminder list | Current screens do not provide the complete reporting/reminder workflow |
| P2 | Add backup/restore UI and verified recovery/update workflows | Backup API and migration snapshot exist; operators cannot complete recovery through UI |
| P2 | Extend structured audit events with actor, before/after values, source and correction reason | Whole-sheet save events do not explain each financial change |
| P2 | Add Desktop and audit checks to the shared build/CI after developer coordination | Desktop is absent from TradeVault.sln; solution-only builds miss UI regressions |
| P2 | Gate sample seeding behind a development/QA build and make multi-party seeding atomic | Current --seed-test-data explicitly operates beside its executable and can partially seed on failure; keep it out of company workflows |
| P2 | Add Undo/edit cancellation, paste preview, keyboard-accessible cell error summaries and persistent layout preferences on USB | Improve operator confidence without changing workbook identities |
| P3 | Provide searchable party selection and party rename/archive rules | Current party creation is present; lifecycle management is incomplete |
| P3 | Add a calendar picker if required; current DateCellEditor is a masked TextBox | Some old test descriptions say calendar columns, but there is no calendar popup in this editor |
| P3 | Explain the read-only selected-value strip clearly; remove misleading fx if formulas are not editable | Avoid suggesting a full Excel formula editor |
| P3 | Reconcile README merge markers and stale audit/design/test descriptions | Current docs still contradict the app on native captions, party scoping and as-of persistence |

Technical cleanup should follow functional priorities: move financial rules out of Desktop into Application/Domain, replace old prototype contract remnants, use typed field metadata rather than label-word inference, and add asynchronous/cancellable long-running data operations. These are recommended changes, not changes made in this review.

The fixed USD currency requirement remains. Preserve the four independent amount inputs and keep shipping editable with default USD 3,800. Do not invent customs equivalences or automatic shipping postings while their business rules are deferred.

## 7. Remaining phases and completion gates

| Phase | Status | Remaining work | Completion gate |
|---|---|---|---|
| 1. Foundation and party scoping | Largely implemented | Party lifecycle polish, stale documentation/build integration, stable IDs | Named parties, migrated legacy data and independent settings remain safe under regressions |
| 2. Authoritative invoice/shipment model | Partial | Map existing inputs into shared services; prevent duplicates; per-invoice terms; draft/posted/corrections; linked containers/BLs | Enter once, reopen safely, change defaults without altering historical contracts, reject duplicate/invalid postings |
| 3. Receipts and allocations | Backend partial; UI missing | Dated receipts, accounts/components, many-to-many allocations, advances, corrections/reversals | Multiple partial receipts and split receipts work end to end through the UI with transaction-safe totals |
| 4. Reporting and reminders | Worksheet calculations partial | Generate both reports, totals/openings, reconciliation, period/as-of/settlement semantics, aging and on-open reminders; Excel output, optional PDF/print | Both report totals reconcile to the same source and match approved sample layouts |
| 5. Manual operator-to-owner sharing | Missing | Versioned package, preview, validation, duplicate/revision handling, immutable owner snapshots and review notes/status | Operator exports on USB A; Owner imports on USB B; works after removing source media; no merging into working invoices |
| 6. Recovery and access hardening | Partial | Backup/restore UI, recovery rehearsal, credential provisioning/recovery policy, audit trail, encryption/key-recovery decision | Restore a verified backup after simulated loss; preserve role restrictions and usable credentials |
| 7. Portable release and updates | Not accepted | Self-contained supported Windows builds, runtime/native dependency packaging, USB-only application paths, safe shutdown/update/migration behavior | Runs offline on clean supported Windows without installation/admin rights; update rollback and two-drive operation verified |
| 8. Performance, accessibility and business UAT | Incomplete | Representative volumes, keyboard/clipboard/caption/dialog tests, DPI/multi-monitor, screen readers, outage/unplug/recovery, training/user guide | Business users sign off real end-to-end workflows; no unresolved release-blocking correctness issues |

For a shorter first business release, report export plus immutable manual submission packages can be implemented against the current explicit worksheet snapshots first. That does not remove the later need for shared transaction data and reconciliation. Never present independently edited worksheets as automatically generated reports.

## 8. What was not tested or cannot be claimed

- No physical USB drive was unplugged, reformatted or modified.
- No clean-PC/self-contained/no-install acceptance run occurred.
- No new Computer Use session or physical caption-button clicks were performed in this review.
- No report import/export was tested: those features do not exist yet.
- No direct production database seeding or migration was performed.
- No complete physical clipboard, keyboard traversal, custom maximize/taskbar, high-DPI, screen-reader or modal-dialog acceptance was established by renders.
- No large-volume/performance benchmark or damaged-storage recovery test was run.
- Authentication does not encrypt the SQLite file. Choose a confidentiality/recovery design before distribution; do not assume the login page protects a lost USB.
- Calculation tests validate the currently documented Excel-shaped rules. They do not resolve deferred commercial meanings or shipping-posting policy.
- Four confirmed gaps are not counted as successful acceptance of the intended feature.

## 9. Reproduction

From the repository root, after prerequisites are restored:

    dotnet build Src/TradeVault.Desktop/TradeVault.Desktop.csproj --no-restore --nologo -p:OutputPath=bin/Audit20261027/
    dotnet Src/TradeVault.Desktop/bin/Audit20261027/TradeVault.Desktop.dll --qa-report Docs/Testing/2026-10-27
    dotnet Src/TradeVault.Desktop/bin/Audit20261027/TradeVault.Desktop.dll --self-test
    dotnet Src/TradeVault.Desktop/bin/Audit20261027/TradeVault.Desktop.dll --render-ui Docs/Testing/2026-10-27/Screenshots
    dotnet test Test/TradeVault.Tests/TradeVault.Tests.csproj --no-restore --nologo -p:OutputPath=bin/Audit20261027/ --logger "trx;LogFileName=backend-results.trx" --results-directory Docs/Testing/2026-10-27
    dotnet run --no-restore --project Test/TradeVault.Desktop.Audit/TradeVault.Desktop.Audit.csproj -p:OutputPath=bin/Audit20261027/ -- Docs/Testing/2026-10-27

The additional audit project is test-only and not added to the shared solution. Its first restore needed approved network access because NuGet vulnerability metadata was inaccessible inside the sandbox. A missing System.IO import in the new audit runner was corrected before its final successful run. No global NuGet audit suppression was added.

## 10. Files changed by this review

- Added this report and current-date JSON/Markdown/TRX/screenshot evidence.
- Added Test/TradeVault.Desktop.Audit as a repeatable, isolated test-only probe runner.
- Appended a work-session entry to Readme.md, preserving previous developers' history.
- Did not change Desktop/Domain/Application/Infrastructure implementation, SQL, migration logic, real credentials, reference workbooks or company records.
