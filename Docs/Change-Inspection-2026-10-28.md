# Change inspection — 2026-10-28

Baseline inspected: commit `22f6b8a` (Report Feature Added), preceded by `5029364` (Party Wise Feature Added). Working tree was clean at the start. Inspection only: no application logic or company records changed.

## Current architecture understood

- Desktop still uses authenticated, party-scoped worksheet storage. Register rows now retain stable `_id` and per-invoice `_terms` values.
- `WorkflowContracts.cs` defines mapping, receipts/splits, report bundles, imports, package previews and reminder DTOs.
- `ReportWorkflow.cs` is the new register-based calculation/receipt service. The older normalized `TradeVaultService` remains preserved; it is not the new Desktop receipt path.
- `WorkflowStorage.cs` adds authenticated workflow document mutations, actor/before/after audit records, register-history guards, password management and backup restoration.
- Schema 4 adds `workflow_documents` and `workflow_audit`. Existing party worksheets and earlier tables remain intact; pre-upgrade backup is retained.
- `ReportFiles.cs` writes typed Open XML Excel snapshots, printable HTML and versioned ZIP-based `.tvreport` packages. It does not use Excel automation or require Excel to generate files.
- `WorkflowWindows.cs` supplies report setup, receipt/correction dialogs, preview/printing, Owner inbox/reviews, reminders, backup/recovery and password screens. MainWindow orchestrates generated versus legacy views and guards unsaved edits/async export.

## Changes since the prior party-wise milestone

1. Explicit per-party setup chooses contract source F6/F7, baseline bank-receipt source F22/F25, opening balance date/amounts and whether shipping is deducted once on invoice date. It does not silently equate amount meanings. Mappings are protected after receipts exist.
2. Generated Account Details and Debit/Credit views derive from saved register rows and dated allocations; they are read-only. Legacy manual worksheets remain accessible and are not overwritten.
3. Register Difference Amount is now calculated/locked: Original minus the chosen contract source. It remains blank without configured/complete inputs. New-term defaults do not rewrite existing invoice terms; Selected terms explicitly edits one invoice. Duplicate references are rejected.
4. Dated receipts split Bank/Difference components across invoices. Unallocated advances, dated reversals, atomic reversal/replacement corrections and allocation guards are implemented. Linked invoice historical inputs/deletion are protected.
5. Generated reports apply effective-date filters and settlement-aware overdue status. Reminder lists and opening-workspace counts cover settled, BL-pending, due-soon and overdue invoices.
6. Export writes both Excel reports, Print.html and Submission.tvreport under a unique Reports subfolder. Same party/date/mode maintains a submission lineage with increasing revisions.
7. Owner import previews a bounded/checksummed known-schema package, rejects duplicates/conflicts/changed-after-preview content, preserves frozen revisions and stores status/notes separately. Imported snapshots never replace editable party records.
8. Backup/restore and password management are connected to UI. Owner restoration validates supported schema/relationships/credentials, takes a safety backup and signs out sessions; password changes/reset also invalidate affected sessions.
9. Runtime-contained Windows publication and verification scripts, test/demo seeding safeguards, independent workflow tests and evidence files were added. Desktop is now included in the solution.

## Fresh verification

Built current Desktop into `.build/inspection-20261028` to avoid replacing an open application or touching its company database:

- Build: 0 warnings, 0 errors.
- Backend test project: 29 passed, 0 failed, 0 skipped.
- Desktop isolated `--self-test`: PASS.
- Desktop `--qa-report`: 42 passed, 0 failed; current output is `.build/inspection-20261028/results`.

These results supersede older count summaries of 28 backend / 39 acceptance checks. No portable package was republished and no physical USB/printer/interactive acceptance was performed in this inspection.

## Important boundaries

- Generated and legacy modes are intentionally different. Legacy exports are snapshots, not reconciled historical reports.
- Generated history uses current invoice values plus effective transaction dates, not a temporal reconstruction of all past invoice edits. Cumulative baseline receipts cannot recover earlier payment history.
- Avoid entering the same receipt in baseline register fields and the dated receipt book.
- Generated negative-net/credit-note cases are explicitly rejected rather than implemented.
- Package checksums are integrity checks, not sender authentication. Database/packages remain unencrypted.
- Shipping is optional explicit configuration; enabling it means invoice-date deduction, not an independently determined shipment/payment policy.
- Source build label is UI 2026.10.27.9, while the README names an older 2026.10.27.7 portable ZIP. Rebuild/verify the portable artifact before assuming it includes all latest source changes.
- Physical USB/clean-PC launch, actual printer output, DPI/accessibility and hands-on Owner/Operator acceptance remain release gates.

The previous 'next phase is report export and Owner import' assessment is no longer current. Those workflows are implemented and covered by tests. Future work should begin from this updated baseline rather than rebuilding them.
