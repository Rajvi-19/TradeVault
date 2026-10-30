# Role removal verification — 2026-10-29

Build: UI 2026.10.29.11, main Debug executable updated.

- Build: zero warnings/errors.
- Backend: 51 passed, 0 failed; backend.trx.
- Desktop acceptance: 42 passed, 0 failed; desktop-results.md.
- Live self-test: passed persistence/worksheet checks.
- Main database: schema 7, no role/account settings, no imported-report records/review audits, unchanged fingerprints across all 11 remaining business tables, integrity OK; main-migration.json.
- Migration/restore regression: older role/import data removed selectively; existing parties, invoice worksheet, report mapping and receipt audit preserved.
- Visual: Invoice Register rendered with three worksheet tabs; no role selector or Operator Reports tab.

TradeVault opens directly to Invoice Register. Receipts, report generation/export and backups remain. Historical backups are preserved; restoring them applies the current schema cleanup. Physical keyboard/mouse, DPI and USB behavior remain manual acceptance checks.
