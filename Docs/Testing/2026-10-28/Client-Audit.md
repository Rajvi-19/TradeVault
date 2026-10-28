# Client-delivery audit — 28 October 2026

Build: **UI 2026.10.28.6**. Scope: application source, isolated SQLite databases, existing automated suites, additional business-edge regressions, WPF dialog/event-handler checks, Excel transfer and rendered PDF output.

Company databases were not opened or modified by this audit. All seeded data and exports are under `.build/client-audit` or isolated test directories. No messages or files were sent to a client.

## Findings fixed

| Finding | Correction | Verification |
|---|---|---|
| Future reversal allowed a backdated receipt to overpay an invoice during an earlier period | Validate allocated bank/difference totals at each effective-date checkpoint, grouping same-day movements together | Reject backdated duplicate settlement atomically; allow same-day reversal/replacement |
| Duplicate stable invoice IDs could make allocations ambiguous | Reject duplicate non-empty internal IDs before saving | Rejected save leaves revision/data unchanged |
| Switching generated report tabs reset the reporting cutoff | Carry selected cutoff between generated tabs and load the register cutoff when changing parties | Real MainWindow handlers retain cutoff and omit later invoices; calendar updates cutoff and closes popup |
| Opening difference disappeared from Account Details totals when no invoice rows existed | Set the closing difference total from the running balance even for an empty details table | Opening-only party shows USD 250 from its effective date, zero before it |
| Reminder date/amount presentation depended on system culture | Explicit DD-MM-YYYY and USD number formatting, readable named columns and cutoff label | Rendered reminder page reviewed |
| Standalone date fields looked like labels rather than editable inputs | Add white background and visible border in workflow forms | Report setup/receipt forms reviewed |
| PDF header text and total label could truncate; serial numbers and terms displayed decimals | More header space, compact TOTAL label, integer serial/terms, day-first dates | Full 14-column report and totals-page PDF rendered and reviewed |
| Closing login during pending authentication could race its completion | Prevent closing while credential verification is pending | Code review; backend authentication and reveal controls covered separately |
| Duplicate-instance startup exposed a raw file-lock error | Friendly message for Windows sharing/lock violations | Existing single-instance regression retained |
| Verification script could return success after a live-check failure | Explicit exit-code propagation | Failure detected during audit; final runner passes |
| Old Desktop.Audit probes expected obsolete bugs as successful results | Replace probes with a wrapper around current isolated live and acceptance checks | Runner builds offline from cached packages and passes |

## Coverage and results

| Area | Checks performed | Result |
|---|---|---|
| Builds | Debug, Release and standalone audit-runner compilation | Passed, no warnings/errors |
| Backend suites | 53 tests across BackendTests, PartyWorkbookTests, WorkflowTests, AccountRecoveryTests and InvoiceRulesTests | Passed |
| Desktop acceptance | 42 checks: required fields, USD/date handling, due dates, formulas, terms, shipping, stale writes, persistence, deletion, field counts, paste/filter/read-only behavior, layout/navigation | Passed |
| Live UI/persistence | Reveal/hide/clear, icon/caption construction, field-scoped errors, typed-date mask, full-row paste, save/reload, party isolation, generated views, calendar selection and cutoff navigation | Passed |
| Authentication/recovery | Exact username case, incorrect credentials, role restrictions, session invalidation, six-character minimum, recovery rotation/single use/restart/cooldown | Backend/live tests passed; native OS click flow remains manual |
| Parties/register | Create/isolate/assign, duplicate references/IDs, draft promotion, date order, precision, save rejection, receipt-history protection | Passed |
| Receipts | Multiple partials, cross-invoice splits, advances, corrections, reversals, effective dates, component overpayment, missing invoice references | Passed |
| Reports/reminders | Generated totals, chosen mappings, shipping, historical cutoff, due-soon/overdue/settled/BL-pending, opening-only balances | Passed within documented accounting assumptions |
| Owner transfer | Excel export/import, preview identity, duplicate/revision handling, review persistence, root isolation, metadata/visible-cell tampering | Passed |
| Backup/restore | Owner permission, verified restore, logout, safety backup, previous-schema migration, incomplete backup rejection | Passed |
| Page/dialog audit | Owner and Operator register/details/ledger; login; create/assign party; setup; receipt/history/correction/reversal; reminders; backups; inbox/review; preview; recovery setup/reset/verification | Open/close/render checks passed using in-process WPF harness |
| PDF | Full-width 35-invoice dataset, 9 pages, repeated headers/totals; PDFium rendering and independent pypdf parsing | Passed; PDF is image-based, not searchable text |

Screenshots are evidence of rendered layout, not proof of every possible mouse/keyboard path. The new `--audit-pages` command creates a separate synthetic workspace, never uses the working company database, and does not submit corrections or reversals into company records.

## Evidence and rerunning

- `Tools/verify_report_update.ps1 -AuditRunner` builds the normal application, runs backend/live checks, then builds/runs the standalone audit wrapper.
- Add `-Configuration Release -OutputFolder .build/client-audit/release` for Release validation.
- `dotnet <built TradeVault.Desktop.dll> --qa-report <evidence folder>` writes the 42-check matrix as JSON and Markdown.
- `dotnet <built TradeVault.Desktop.dll> --audit-pages <evidence folder>` captures application pages/dialogs and creates a full-width report PDF. The root contains synthetic data; do not distribute it.
- Final local evidence: `.build/client-audit/runner-evidence`, `release-acceptance` and `final-release-pages`. The final Release page audit completed successfully and captured 36 screenshots across 22 page/dialog states. An additional independent file check parses/renders every PDF page and opens the exported Excel tabs.

## Required before client sign-off

1. Run the actual self-contained USB release on the client's Windows computer without a .NET SDK. This audit validates framework-dependent Debug/Release builds; a fresh self-contained publication was not produced or validated here because its runtime packages are not cached. Do not treat a development output folder as the client USB package.
2. On that machine, check sign-in/logout, password visibility, calendar selection, caption buttons, keyboard entry/copy/paste, Windows file pickers and opening exported Excel/PDF through file associations. The desktop automation inventory did not expose the test process launched from the execution environment, so these native interactions are not claimed as tested.
3. Perform one physical Operator USB → Owner USB Excel handover, then close/eject/reinsert/reopen. Test backup recovery using a separate test USB. No power-loss or sudden-unplug simulation was performed.
4. Test the installed physical printer. Direct PDF output passed; a hardware driver was not exercised.
5. Have the company confirm the per-party amount/receipt mappings, opening balances and shipping treatment using a known invoice/payment example. Automatic Report setup removal is still awaiting the business source-field decision. Do not guess this before client sign-off.

No remaining failures were observed in the exercised automated checks. This is not a claim that every OS/device configuration or every possible input combination is bug-free.
