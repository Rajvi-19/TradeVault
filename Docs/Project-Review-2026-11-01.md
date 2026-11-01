# Current change review — 1 November 2026

Reviewed latest committed changes, dated README sessions, the existing Project-Review-2026-10-31.md findings, current theme initialization, master ledger projection, report rendering and theme checks. This is a focused change review and automated regression run, not an exhaustive new accounting audit.

## Current behavior

- Six direct-access pages: Party Master, Agent Master, Invoice Register, Party Receipt, Agent Receipt and Reports. Authentication and roles remain removed.
- Master tables use ledger projections for original/incoming totals, payments and pending amounts. Agent company remittances stay distinct from buyer payments.
- Separate party-to-agent and agent-to-company receipts retain dates, amount, informational USD-value text and references. FIFO allocations remain scoped to agent; company remittances do not settle buyer debt twice.
- Reports provide Account Details, Party Ledger and Agent Ledger, compact account selectors, shared party selection and read-only grids. Account Details footer statistics and report notices were removed in recent changes.
- Latest Account Details changes show party receipts before company receipts, remove Difference Amount, and calculate pending as allocated buyer receipts minus allocated company receipts. These are current changes beyond earlier work logs.
- Recent UI work centers table content, improves vector caption icons and maximize state, removes blank validation tooltips, expands last real columns, separates report toolbar/table surfaces and focuses agent on New Invoice.
- Excel exports are table-only, with bold headings, explicit outlines and typed values. PDF uses wide full-column pages; large reports require zoom or appropriate paper size.
- Graphite dark palette already exists. Before this task startup still fell back to light and the main settings file selected light.

## Implemented now

Dark is the initial/fallback theme. Missing, malformed or null settings use dark. Explicit saved light preferences continue to work, so selecting light remains effective after reopening. The main app settings were switched to dark once for this requested update. The existing button switches dark to light and back without changing records. No company database or financial logic changed.

## Known remaining issues from the existing review

The prior review reproduced draft invoices accepting allocations while reminder/report eligibility can exclude them; date eligibility/protection also needs a consistent rule. These findings were reviewed, not re-probed or fixed here. New receipt correction/reversal and advance-assignment workflows remain absent. Fresh-demo render tooling has an obsolete Atlas lookup after agent renaming. README front matter/Workflow-Guide include obsolete instructions. Large-workspace performance and physical USB/printing/recovery testing still need evidence. Passing automated checks does not resolve these uncovered cases.

## Verification

Main Debug build: zero warnings/errors. Backend: 73 tests passed, none failed/skipped (current checkout suite count). Acceptance: 52 passed, including fresh dark default, first toggle to light, persisted light/dark preferences, malformed-setting dark fallback and contrast. Live WPF regression passed. Evidence: Docs/Testing/2026-11-01/DefaultDark. Earlier sessions had 86 backend tests; recent committed test restructuring changed the current count, so this run reports the actual 73.
