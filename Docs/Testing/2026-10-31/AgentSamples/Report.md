# Agent samples and receipt alignment — 2026-10-31

- UI 2026.10.31.12, final build: zero warnings/errors.
- USD input left aligned in both receipt modals; live regression passed.
- Live seed regression: six invoices/six receipts on first run, zero additions on repeat; shared stable agent references verified.
- Desktop acceptance: 52 passed, zero failed.
- Main database: three fictional agent/buyer pairs, six invoices, three party and three agent receipts added after backup. Existing agents reused where present.
- Preservation: 14 existing records/documents checked, zero changed/missing; integrity ok, foreign-key errors zero.
- Final synthetic visual review: agent master and left-aligned receipt input inspected.
- No automatic balance reconciliation or Reports-page implementation introduced.
