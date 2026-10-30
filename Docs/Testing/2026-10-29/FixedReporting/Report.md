# Fixed report rules verification — 2026-10-29

Build UI 2026.10.29.13, main Debug app updated.

- Build: zero warnings/errors.
- Backend: 53 passed, 0 failed (backend.trx).
- Desktop acceptance: 42 passed, 0 failed (desktop-results.md).
- Live self-test: passed, including automatic named-party reports and receipts without setup.
- Main schema: 8; setup mappings removed; all 11 other business-table fingerprints unchanged; integrity OK (main-migration.json).
- Visual: generated reports inspected; setup and opening-balance controls absent.

Fixed bank contract source: Our customs invoice amount (F6). Earlier bank receipts: Nemichem amount (F25) dated F26. No automatic shipping deduction or supplementary opening adjustment. Register Difference before shipping remains Original (F5) minus Pakistan customs (F7). Existing source records/payments and old exports/backups are preserved. Balances recalculate under the new rules.

Physical mouse/keyboard, DPI and printing remain manual acceptance checks.
