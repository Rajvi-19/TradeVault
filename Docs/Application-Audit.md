# Application audit — 2026-10-27

## Scope and verification

Inspected Desktop startup, login, navigation, worksheet/grid/controller, field definitions, authenticated worksheet store, normalized invoice/receipt services, database initialization, portable paths, solution and tests. This is a source and automated-check audit, not physical USB or interactive acceptance certification.

- Desktop project build: passed, 0 warnings, 0 errors.
- Desktop `--self-test`: passed live authentication, persistence and worksheet/UI checks.
- Backend test project: 9 passed, 0 failed, 0 skipped.
- No company records or application logic changed during this audit.

## Implemented end to end

- Predefined owner/operator login with salted PBKDF2 password hashes, invalid-login feedback, temporary failed-attempt throttling, session checks and sign-out.
- Corporate WPF shell, role-sensitive navigation, custom window frame, search, cell selection, clipboard rectangles, frozen identifiers, compact rows and keyboard commands.
- Three Excel-shaped worksheets: 26 input fields, 14 account-details columns and 9 populated debit/credit columns. Labels match source definitions with surrounding whitespace trimmed.
- Blank worksheet rows become actual records when edited. Untouched display rows are not seeded company entries.
- New/edit/delete/save/reload, persistence across reopen, discard confirmation, inline validation and save feedback.
- Worksheet-level atomic SQLite saves, revision checks, migration tracking, data-folder lock, application-relative storage and save-level audit events.
- USD entry validation, editable 3800 shipping defaults and 90-day terms; dates normalize to ISO internally with common day-first input formats accepted in the UI.
- Details calculations F=D-E, G=C-D, I=C-D-H, K=previous K+I-J and N=L+M. Ledger running balance E=previous E+C-D and calculated due K=I+J. Input-form due/reminder/overdue calculations and report opening balances.

## Implemented backend but not connected to the UI

`TradeVaultService` has normalized party/invoice creation and editing, invoice duplicate checks, per-invoice terms, partial receipts, allocation across invoices, unallocated receipt remainder, allocation guards and dated bank-contract balance queries. Desktop currently uses `WorkbookStore`, not this service.

`PortableDatabase.CreateBackup` makes a consistent snapshot and verifies integrity. Backup reopen is tested, but there is no user-facing backup/restore workflow.

## Material gaps and correctness boundaries

1. **Two data models remain disconnected.** Desktop saves JSON worksheet snapshots in `worksheets`; the normalized `parties`, `invoices`, `receipts` and `receipt_allocations` tables are a separate backend path. UI edits do not populate those entities, invoke invoice duplicate guards or generate the other two sheets.
2. **Account reports are not partitioned by party.** Each report has one global worksheet/opening balance per database. Its columns do not include a party key; mixing parties merges running balances. A party/workbook selector outside the Excel columns should own distinct datasets.
3. **Report export and owner import are missing.** No Excel/PDF output, print layout, versioned report package, preview/import, duplicate handling or immutable submission history. The Owner reports control is disabled.
4. **Receipt history is not available through the UI.** Report fields store manually entered cumulative receipt values; they do not expose multiple dated receipts, allocation/reallocation or reversals.
5. **As-of is limited.** It updates overdue-day arithmetic on the input sheet. It does not filter invoice/receipt transactions, reconstruct historical balances or freeze an exported report. It is not persisted in worksheet metadata.
6. **Terms scope differs.** The input sheet uses a shared terms setting for all rows. Details/ledger rows have their own TERMS column; the normalized service supports per-invoice terms.
7. **Missing amounts become zero in formulas.** Incomplete rows can show plausible balances. Missing shipping versus confirmed zero is not explicitly modelled.
8. **Overdue calculation ignores settlement in the input worksheet.** It is calendar days after due date; the separate normalized service suppresses overdue days when its bank-contract balance is paid.
9. **No complete totals or reminder workflow.** Workbook-style summary totals, party aging, reminders/notification list and report period selection are absent. Reminder date calculation exists; background notifications do not.
10. **Release safeguards unfinished.** Password change/recovery, database encryption, actor/before-after audit history, recovery UI, safe upgrades, USB enforcement, self-contained publication and clean-PC/removable-media tests remain outstanding. Current runtime needs .NET 10 Desktop Runtime.
11. **Build coverage gap.** The Desktop project is not in `TradeVault.sln`; backend solution builds alone do not verify the app. Desktop must be built separately until added.
12. **Typed sorting incomplete.** Grid sorting compares string values; numeric amounts need numeric comparison to sort correctly. Display sorting does not change the stored row order used by running balances.

Passing checks establish their covered behaviours, not production readiness or completion of missing workflows.

## User policy recorded

- Preserve the four amount fields independently; do not infer financial equivalences or require their meanings to proceed with unrelated work.
- Keep shipping editable with default USD 3800. The user tentatively associates it with shipments but has asked to defer the unit policy.
- The answer 'yes' to 'when charged or paid' does not identify an accounting trigger. Do not silently introduce automatic shipping ledger postings. Existing explicit Excel-shaped manual calculations remain as documented.

## Recommended implementation order

1. **Party-scoped worksheets and data safety:** choose/create a party outside the Excel columns; separate report balances/metadata; preserve current datasets through an explicit migration with backup. Add stable row IDs, typed sorting, per-invoice terms and missing/zero handling. Add Desktop to the solution/build checks.
2. **Finish the original offline report workflow:** real Excel export and a versioned snapshot package; Owner import preview, duplicate/revision handling and immutable stored submissions. This can work with explicit existing columns and formulas without guessing customs mappings.
3. **Connect receipt history:** authenticated services and actual receipt/allocation screens. Preserve current manually entered totals during migration; never create automatic duplicate credits. Generate report amounts only where mappings are explicit.
4. **Harden report calculations:** party totals, opening balances, period/as-of semantics, settlement-aware overdue status and deterministic report order. Keep unresolved amount/charge mappings manual until explicitly defined.
5. **Release:** backup/restore UI, credential management, chosen confidentiality/recovery design, self-contained USB packages, update/recovery checks and end-to-end tests on two real USB drives and a clean Windows PC.

Developer B owns data scoping/migration, services, packages, calculations and tests. Developer A owns party selector, receipt screen, report preview/import UI, feedback and accessibility. Agree contracts before modifying shared callers; update README at every handoff.
