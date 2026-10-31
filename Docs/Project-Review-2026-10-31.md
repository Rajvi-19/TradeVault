# Current project review — 31 October 2026

Reviewed commit `61fc646` (Agent & Party master fixed), following `ac516c6` (Receipt & Report Functionality Implemented). The working tree was clean before this review. Current source label is **UI 2026.10.31.23**, database schema **13**. This review changes documentation only; the normal executable and company database were not replaced or seeded.

## Current application

| Page | Current behavior |
|---|---|
| Party Master | Create a party; read-only name, invoice count, original invoice total, paid by party to agent, and party balance. |
| Agent Master | Create an agent; read-only name, invoice count, receipts from parties, remittances to company, and agent balance. |
| Invoice Register | 21 columns; party/agent/invoice number/invoice date/country are immutable; first three frozen. Named-party editing, combined party/agent filters, search, new-invoice modal, spreadsheet editing, save/history, custom due date and reminders. |
| Party Receipt | Add a payment from a party to a selected receiving agent. Saved read-only history; six columns including optional USD-value text. |
| Agent Receipt | Add a remittance from an agent to the company. Saved read-only history; five columns including optional USD-value text. |
| Reports | Account Details, Party Ledger, Agent Ledger, separate master selectors, read-only grids and page exports. |

The app opens directly into the workspace: no active login, Owner/Operator split or owner report inbox. Backups remain in the title bar. Light/dark styling, custom window controls, keyboard navigation and current-page Excel/PDF exports remain.

## Architecture and storage

- C# / .NET 10; WPF desktop, with most screens assembled in C# and shared XAML styles. The main window is divided across partial classes rather than a full MVVM presentation layer.
- Domain and Application projects contain monetary types, contracts and older workflow models. Infrastructure contains SQLite persistence, worksheet validation, allocation and report projections. Desktop uses those services directly.
- SQLite provider: Microsoft.Data.Sqlite 10.0.14. Backend tests use xUnit. Excel output is generated through the existing Open XML ZIP/XML writer; PDF output uses the WPF report paginator/export implementation.
- PortablePaths resolves application-relative Data, Backups and export locations. The normal Debug database is `Src/TradeVault.Desktop/bin/Debug/net10.0-windows/Data/tradevault.db`.
- `worksheet_parties` and `worksheet_agents` hold masters. `party_worksheets` holds revisioned JSON registers; invoice `_id` is stable and `_agentId` links an agent. Older worksheet/normalized tables remain for compatibility.
- New receipts reuse `workflow_documents`: kinds `party_transfer_receipts` and `agent_transfer_receipts`, ID `all`. Each is a revisioned JSON book. Receipt IDs, dates, amounts, master references and allocation splits are stored, with before/after snapshots in `workflow_audit`. No schema increase was needed.
- A folder lease prevents two app instances using the same workspace. Mutations use SQLite transactions; backups/restores preserve receipt books and invoice links. The database is not encrypted.

## Payment flow and calculations

Party Receipt and Agent Receipt are separate accounting legs:

1. Party pays agent: reduces party debt and increases recorded funds held by that agent.
2. Agent pays company: reduces funds held by that agent. It does **not** reduce party debt again.

New receipts allocate automatically, oldest F4 proforma/invoice identity date first, with deterministic reference/ID tie-breakers. Party allocation is limited to that party and receiving agent; agent allocation spans parties for that agent. Future F4 dates are excluded. Each invoice receives its F6 bank-contract portion first, then F5 original minus F6 difference. Excess stays unallocated. An incomplete earliest candidate stops allocation instead of skipping ahead. Existing allocations reserve capacity, including later payments when backdating.

`USD value` is optional informational text, not a currency conversion. Accounting amounts remain USD with positive receipt amounts, a maximum of one trillion per entry and no more than two decimal places.

- Register difference = F5 original − F7 Pakistan customs.
- Account Details difference = F5 original − F6 Our customs.
- Party Ledger = invoice originals − party payments, including preserved historical payments/baselines.
- Agent Ledger = party receipts linked to the agent − agent remittances to company.
- Master financial columns reuse the corresponding ledger projections. Negative balances are retained as advances/excess remittances; unknown original amounts leave party totals/balances blank.
- Due defaults to agent-country BL + 90 days; explicit overrides persist. Reminders and overdue integrate new Party Receipt allocations. Agent remittances are excluded from party settlement.
- Reports show all recorded dates; reminder aging uses today. Report search filters grid/export rows, while ledger summary cards are full-account totals.

Account Details has 14 visible columns, including company receipts allocated per invoice. Unsupported shipment-payment/post-shipment/cumulative-difference cells remain blank. Agent bank-charge/net-deposit cells remain blank. Opening balances are excluded. Account Details statistic cards and expandable calculation-note footer were removed; ledger summary cards remain. These limitations must not be interpreted as zero charges or fully reconciled opening balances.

## Confirmed issues and gaps

### High: draft allocation can break reminders

Reproduced using an isolated synthetic workspace:

1. Create an invoice with F4, agent and identity; save F5/F6 amounts but leave India invoice F9/F10 empty.
2. Save a Party Receipt dated after F4. FIFO allocates money to the draft.
3. Request reminders. `BuildGenerated` excludes the draft because F9/F10 are incomplete, then rejects the allocation: `A receipt references a missing invoice. Restore the invoice or reverse the receipt first.`

The UI converts this error into Needs validation. The draft exists; allocation and reporting disagree about eligibility. Relevant files: `PartyReceiptFifo.cs`, `ReportWorkflow.cs`. A fix should establish consistent eligibility across allocation, reports and reminders, with regression coverage.

### Medium: payment-date basis and protected dates disagree

With F4 = 1 June, India invoice F10 = 10 June, a 5 June Party Receipt is immediately allocated rather than retained as an advance. Moving F10 to 15 June afterward also succeeds despite the saved allocation. `GuardFifoInvoices` protects amounts, baseline dates and agent, but not F10. Older receipt APIs instead require allocated payments on/after F10.

Whether pre-India-invoice allocation is permitted needs one consistent business rule. If F4 is intentionally the accounting start, reports and validation must state that clearly; if F10 is required, pre-invoice payments should stay advances and F10 edits must preserve payment chronology. This review does not change that rule.

### Medium: new receipts have no correction or advance-assignment UI

Current pages add and display receipts. There is no edit, reversal, cancellation or explicit allocation of an existing advance. Old correction APIs operate on the legacy receipt book, not the new transfer books. A mistaken new receipt cannot be corrected through the current UI; completing an invoice later does not redistribute an earlier advance. Historical unallocated agent receipts are not retroactively allocated. This is a functional gap, not a failed existing test.

### Low: fresh-demo rendering command fails

`--render-fresh-demo` produces the register/master/receipt captures, then fails at `LiveChecks.cs` line 14 with `Sequence contains no matching element`. It looks for an agent containing `Atlas`; the current demo seeder creates Meridian, Straits and Johor. Ordinary `--render-ui` succeeds. Test tooling should select a returned stable fixture identity rather than a stale display-name fragment.

### Documentation and maintenance

- The README front matter still says build .10 and describes removed authentication, role handover and receipt placeholders. Its appended work logs correctly describe later changes. Workflow-Guide still says .16/13 Account Details columns and describes controls since removed. Readers cannot safely use those sections as current instructions.
- Master pages build a full ledger separately for every master; receipts and invoices are loaded repeatedly. Correctness is covered for small fixtures, but there is no large-data performance evidence.
- Commits include many generated application binaries, native runtimes and test workspace files under Docs/Testing, including a SQLite fixture. This inflates source history and makes fixture/business-data boundaries harder to review. No tracked artifact was removed during this review.

## Verification performed

All runs used isolated output and synthetic databases under `.build/current-review`; no normal company records were read or modified for the probes.

| Check | Result |
|---|---|
| Desktop build through Tools/verify_report_update.ps1 | Passed; zero warnings/errors |
| Backend test suite | 86 passed, zero failed/skipped |
| Live WPF persistence/workspace/worksheet checks | Passed, including receipt forms, master/report pages, filters and export regressions |
| Automated acceptance report | 52 passed, zero failed |
| Generic UI rendering | Passed; master and Account Details screenshots visually inspected |
| Fresh-demo rendering | Failed at obsolete Atlas agent lookup, after initial page captures |
| Focused synthetic allocation/date probes | Reproduced the two accounting cases described above |

Passing existing tests does not establish correctness for uncovered cases. This was a code/automated-check review, not exhaustive manual interaction on physical USB devices, printers or multiple Windows configurations.

## Recommended next work

1. Resolve draft allocation/report eligibility and establish the receipt-date basis; add focused regression tests.
2. Add audited correction/reversal and advance-assignment workflows for both new receipt books.
3. Repair demo rendering and replace stale README/Workflow-Guide usage sections with current behavior while retaining dated history.
4. Test realistic data volumes and physical USB/printing/recovery workflows before client release.
