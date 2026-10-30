# Fixed blank row positions — 2026-10-29

Build UI 2026.10.29.17, normal Debug app updated.

- Build: zero warnings/errors.
- Backend: 55 passed, 0 failed (backend.trx).
- Desktop: 49 passed, 0 failed (desktop-results.md).
- Live self-test: passed persistence/workspace/worksheet checks.
- Row-position regression: Delete/Cut leave a vacancy at the same position; record 4 stays on row 4 after clearing row 3.
- Persistence: blank position survives save/database reopening; undo replaces the vacancy, redo clears it again.
- Reuse: entering a new invoice into a cleared row assigns a fresh identity without shifting another record.
- Reports/counts: vacancies excluded; receipt-history protection remains.
- Main migration: schema 9, all 11 existing business-table fingerprints unchanged, integrity OK (main-migration.json).
- Visual: Screenshots/blank-third-row.png shows row 3 blank and row 4 populated.

Delete still confirms. Cut does not. No company records seeded or financial rules changed. Physical keyboard/mouse interaction remains manual acceptance.
