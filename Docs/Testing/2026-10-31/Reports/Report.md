# Reports verification — 2026-10-31

Build UI 2026.10.31.13. Main Debug app updated.

- Backend: 80 passed, zero failed/skipped (backend.trx).
- Desktop: 52 acceptance checks passed, zero failed (desktop-results.md/json).
- Live WPF: passed, including receipt allocation results, report columns/selectors/reload, export data types/PDF identifiers and compact viewport.
- Build: zero warnings/errors.
- Synthetic renders: all three reports, both themes and receiving-agent receipt modal generated; inspected three report layouts and final Party Receipt modal.

Financial tests verify party+agent FIFO by proforma date, bank-first ordering, multiple/partial/excess payments, old receipts, future/incomplete eligibility, historical reversal checkpoints, protected linked records, backup/restore and settlement/reminder consistency. Missing shipment, bank-charge and opening inputs are deliberately unavailable by user instruction.

No production database was opened or modified by automated checks; no migration or seeding performed. Old receipts retain their data until explicitly linked through Set receiving agent. Existing schema-13 document/audit storage supports the new metadata; no separate report database is introduced.

Remaining limitations: physical USB/high-DPI/device acceptance, receipt correction/reversal and advance reallocation UI, and any future charge/opening inputs. Exports retain existing current-visible-row behavior; summaries represent the full selected account and are labelled accordingly. PDFs use the existing raster page renderer.
