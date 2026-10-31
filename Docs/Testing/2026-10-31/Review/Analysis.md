# TradeVault change review — 2026-10-31

Reviewed HEAD 9894eef (UI 2026.10.31.10), compared with 967a511 and the recorded sessions. The working tree was clean before this review. Scope: current UI, invoice storage/validation, master data, filters, reminder generation, exports, migration/backup code, tests and documentation. This was an analysis session: no production source changes, main-app replacement, company database access, migration or seeding.

## Findings and priorities

1. High completion priority: Party Receipt, Agent Receipt and Reports are deliberately empty placeholders (LiveMainWindow.OpenPage). The earlier PaymentsWindow was deleted; Account Details / Debit Credit remain internal/backend paths but have no current navigation. The application currently supports invoice administration, not the complete payment-to-account-statement workflow. Do not count these three tabs as completed features. Existing receipt records are retained; missing UI does not mean deleted data.
2. Medium: reminder coverage is incomplete for saved drafts. ReportWorkflow.BuildGenerated excludes new invoices lacking any of India invoice number/date or original/customs amounts. CurrentReminders displays only returned rows and shows no excluded-draft count. A saved visible invoice can therefore be absent from Reminders, even if its BL/due date is present. Show an explicit incomplete/needs-completion group and omitted count; keep unknown balances unknown. Additionally, a validation failure anywhere in the party causes all its currently visible invoices to become Needs validation, even when the invalid invoice is outside the filter. This behavior is explicit but should identify the actual failing invoice.
3. Medium usability limitation: five identity fields are permanently locked (party, agent, invoice number/date, country). A typing mistake has no correction UI. Deleting/recreating a record is possible only without receipt protection. Add a controlled, audited correction workflow before expanding payments; preserve linked invoice IDs and accounting evidence. Immutability itself is intentional, not a failing test.
4. Medium requirement gap: exports default beside the app, but SaveFileDialog accepts any destination. If company files must strictly remain on USB, this is not enforced. An authorized export policy or destination validation is needed. Opening exports in external Excel/PDF applications can also create host-managed recent-file/cache artifacts; current portability cannot promise control of those external programs.
5. Medium documentation defect: Docs/Workflow-Guide.md still describes account tabs, receipt-entry controls, shipping-era flows and full financial exports that are unavailable in the present UI. Publish-Usb.ps1 copies this guide into release output. Rewrite the current workflow before release; preserve historical session notes separately.
6. Lower priority / confirm business rule: New Invoice's displayed Invoice number uses F3 (former proforma number), while accounting uniqueness is enforced for India invoice number F9. F3 is required but not unique. Two visually identical invoice identities can be created, then locked. Decide whether F3 must be unique per party or per party/agent, and make invoice/proforma terminology consistent in errors and reports.
7. Lower priority: PDF exports rasterize each page as JPEG. They preserve visual Unicode rendering but text cannot be searched/copied as normal PDF text; file size grows with pages. Master invoice counts also print with two decimals through the generic numeric formatter. Use integer formatting for counts and consider vector/text PDF output if required.
8. Scale risk, not measured failure: master counts load all JSON invoice rows; GetAgents scans them for each agent, and RequireAgent invokes GetAgents during validation for every new-style invoice. Saving large registers can repeatedly scan all company data synchronously on the UI thread. Add a realistic large-data benchmark and reuse a master lookup per transaction before claiming USB-scale performance.

## Changes confirmed

- Six tabs: Party Master, Agent Master, Invoice Register, Party Receipt, Agent Receipt, Reports; direct startup without authentication/roles.
- Party/Agent master creation, normalized duplicate-checked names, invoice counts and stable agent references; schema 13.
- Modal New Invoice with party/agent selection and locked identities; blank-slot reuse, revision checks and backend identity protection.
- 21 visible register columns; identity order party / agent / invoice number / invoice date / country. First three frozen, all five read-only.
- Combined party, agent and search filtering. All-party aggregate is read-only; selecting a named party restores editing. Filtered saves retain hidden rows.
- Current-page Excel/PDF export only, with visible rows/order and PDF column bands; no financial statement sheets appended.
- Register Difference amount remains Original minus Pakistan customs. Internal account reports still use Our customs as bank portion; their difference therefore differs when the two customs amounts differ. Do not silently unify these formulas.
- Due date is now BL plus fixed 90 days unless an explicit due-date override exists. Earlier editable terms UI is hidden/dead; original editable-term requirements are no longer represented by current register behavior.
- Shipping removed from active schema/visible fields. Old evidence/backups must be distinguished from active values.
- Reminders support filtered all-party views and historic receipt deductions. Backup action is in title bar; custom maximize uses current monitor work area.
- Existing clipboard, blank-row deletion, undo/redo, theme and keyboard functionality remain covered by regression checks.

## Verification performed

- Backend: 69 passed, zero failed/skipped. Evidence backend.trx.
- Isolated desktop build: zero warnings/errors; compiled to Review/App so the normal running copy was not replaced.
- Live WPF persistence/workspace regression: passed.
- Desktop acceptance: 52 passed, zero failed. Evidence desktop-results.md/json.
- Synthetic UI renders completed; inspected minimum-width invoice register with six tabs, frozen identities, visible footer and horizontal scrollbar.
- Checks exercised synthetic data, not company records. Existing successful migration-preservation claims in historical README entries were not independently re-executed against the company database.
- No physical USB, second-computer, touchpad, high-DPI, long-running large-data or native external-app acceptance performed. Passing automated checks does not establish these.

## Recommended remaining phases

1. Specify and implement Party Receipt and Agent Receipt, including who owes whom, allocation rules, dates, corrections/reversals and visible outstanding amounts. Do not infer agent accounting from party accounting.
2. Implement Reports contents: Account Details / Debit Credit access, reconciliation, totals, clear formula labels and statement exports separate from current-page exports.
3. Add draft/completeness visibility, controlled identity corrections and business-approved duplicate rules.
4. Update user guide and remove/segregate obsolete internal UI paths/tests. Current live tests still exercise inaccessible legacy account screens; their passing does not mean Reports is implemented.
5. Benchmark production-sized records; finish USB destination/confidentiality policy, restore/recovery and self-contained clean-computer/device acceptance.

Recommended next milestone: one named party invoice -> recorded dated payment -> invoice outstanding balance -> reconciled account statement. Then define and complete the independent agent workflow.
