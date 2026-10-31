# Fresh demo and UI checks — 2026-10-31

- UI 2026.10.31.16: main Debug app updated, build zero warnings/errors.
- 83 backend tests passed; live WPF self-test passed; 52 acceptance checks passed.
- Due-date override/default/reload/BL-edit/clear-to-auto behavior verified by existing regression checks.
- Active report Terms columns and Refresh button removed; due dates remain calculated correctly.
- Main data reset after backup: six marked parties, three old agents and twenty old test invoices removed. Empty unmarked Dhyan retained.
- Fresh data: three demo parties, three agents, nine invoices, four party receipts, three agent receipts, USD 2,000 advance.
- Live database integrity ok; zero foreign-key errors; receiving-agent/party allocation links and due-date formulas verified; temporary staging removed.
- Fresh Account Details and Party Ledger visual render inspected. See Testing-Guide.md and live-data-results.json for expected amounts.
- No automatic startup seeding, schema migration, ZIP, or purge of historical audit/backups/export files. Physical device acceptance and previously unavailable financial inputs remain outside this change.
