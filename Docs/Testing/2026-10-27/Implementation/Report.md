# Implementation and verification — 2026-10-27

Build: UI 2026.10.27.7. This report supersedes the earlier gap audit for the workflows listed below. Earlier audit artifacts remain as historical evidence; the old Audit probe runner encodes pre-change expectations and is not the current release gate.

## Delivered phases

| Phase | Implementation |
|---|---|
| Report export | Two typed Excel workbooks, printable HTML and versioned .tvreport package; USD totals, party, dates, submission ID and calculation metadata. |
| Owner handover | File selection, preview, checksum verification, duplicate/conflicting revision rejection, preserved corrected submissions and persistent read-only review history/status/notes. Independent databases tested. |
| Receipts | Dated receipts, Bank/Difference splits over multiple invoices, partial payments, unallocated advances, atomic correction/reversal and outstanding balances. |
| Report population | Explicit setup maps register fields to generated Account Details and Debit/Credit reports. Preserved legacy worksheets remain available. Calculations and validation run in Infrastructure services. |
| Reporting accuracy | Effective-date filtering, receipt-aware invoice settlement, per-invoice payment terms and overdue/due-soon/BL-pending reminders on opening. |
| Recovery and accounts | Backup interface, validated Owner restore with safety backup and rollback, own-password changes and Owner Operator-password reset. Sessions are invalidated after credential changes/restore. |
| USB packaging | Self-contained Windows x64 folder and ZIP, runtime/native SQLite bundled, guide included, no initial company Data folder. |

Application contracts are in WorkflowContracts.cs; workflow storage, calculations and packaging are in Infrastructure. Desktop provides dialogs, grids and orchestration. Schema 4 adds workflow documents and before/after audit records. Prior normalized records and independent report worksheets are preserved; they are not silently converted. Imported report payloads remain separate from editable Owner records.

## Verification evidence

- Desktop build: zero warnings and errors.
- Backend: **28 passed**, zero failed/skipped; see workflow-results.trx.
- Desktop acceptance: **39 passed**, zero failed; see desktop-results.md/json.
- Desktop self-test passed, including generated read-only reports, date refresh, receipt updates and preservation of legacy worksheets.
- Published self-contained executable self-test: exit 0; portable-self-test.log. Synthetic records were isolated from company data.
- Excel files independently opened with openpyxl: numeric/date cell types, frozen headers, repeated print rows, merged metadata and landscape settings verified.
- Tests cover allocations and atomic failures, advance correction, historical receipt reversal, settlement, duplicate invoices, separate-database handover, changed revisions, persistence after source files are unavailable, package tampering/path entries, preview-to-import changes, credentials and backup restore (including older schema).
- Rendered UI evidence is under Screenshots/. Automated checks/rendering do not establish physical mouse, clipboard, caption controls, accessibility or printer-driver acceptance.

## Boundaries and remaining release gates

1. Perform physical USB tests and launch on another Windows computer without an installed runtime/admin rights. Check removable-drive disconnect/recovery, target architecture and file permissions.
2. Perform hands-on Owner/Operator acceptance: keyboard/clipboard, all modal workflows, window controls, DPI/accessibility, printed page layout and real printer driver. Windows x64 was published; ARM64 requires its own build/test.
3. Confirm the report setup against company examples before using generated balances. Shipping deduction is an explicit option, not inferred. Baseline cumulative payments are one dated movement; earlier payment history is not invented. Avoid recording a baseline payment again as a receipt.
4. Historical reports use current invoice values filtered by effective dates, rather than reconstructing every prior invoice edit. Frozen submitted revisions preserve their exported state. Generated negative-net credit-note handling is not implemented.
5. Decide confidentiality/recovery policy. SQLite and report packages are not encrypted. Checksums detect changed content but do not authenticate the sender; manually verify the handover source. Authentication does not protect a copied database from direct access.
6. Validate backup recovery and upgrade with a copy of an actual company dataset before deployment. No company database was upgraded during testing. Preserve Data and Backups when replacing binaries.

The core software workflows are implemented. Operational release acceptance and the confidentiality policy remain outstanding; this is not a claim of completed production acceptance.
