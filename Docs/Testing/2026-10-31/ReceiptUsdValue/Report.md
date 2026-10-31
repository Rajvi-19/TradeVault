# Party Receipt USD value — 2026-10-31

UI 2026.10.31.14.

- Removed two allocation display columns; retained backend FIFO.
- Added plain optional USD value text field/column. No currency selector or conversion.
- Receipt amount remains USD; Agent Receipt behavior unchanged.
- Older values remain blank, without invented rates or company data updates.
- Three focused backend tests passed, live WPF self-test passed, 52 acceptance checks passed; build zero warnings/errors.
- Final synthetic Party Receipt form/table inspected. Values persist/export as text.
- No schema migration or ZIP. Saved receipt rows remain read-only after creation.
