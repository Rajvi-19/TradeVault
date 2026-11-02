# Fresh testing dataset — 1 November 2026

All active previous records, including manually entered masters/invoices/receipts and old audit rows, were replaced. Recovery copy remains beside the application under RecoveryBackups. Only [DEMO] masters are active.

| Records | Count / USD total |
|---|---|
| Parties / agents | 3 / 3 |
| Invoices | 9 / 131,000 |
| Party Receipts | 4 / 62,000 |
| Agent Receipts | 3 / 50,000 |
| Bank charges | 100 (including one receipt with 0.00) |
| Expense Receipts | 4 / 435 |
| Party advance | 2,000 |

| Agent | Net party receipts after bank charges | Company remittances | Expense receipts | Pending |
|---|---:|---:|---:|---:|
| Meridian Colour Malaysia | 20,965 | 16,000 | 150 | 4,815 |
| Straits Dye Trading | 5,985 | 4,000 | 195 | 1,790 |
| Johor Textile Chemicals | 34,950 | 30,000 | 90 | 4,860 |

Party debt balances exclude company remittances/expenses: Faisalabad 35,000; Multan 36,000; Chattogram -2,000 advance. Account Details Expense adds invoice bank-charge shares and explicit expense receipts. A portion of bank charge attributable to the advance remains outside invoice expenses; Agent Ledger still deducts the entire bank charge.

Test flow: inspect both masters; select each party in Invoice Register; inspect all three Receipt sub-tabs; compare Account Details invoice expenses with Agent Ledger movements and pending above. Dataset covers full/partial payments, overdue/upcoming/manual due dates, BL pending, different agents for one party, advance, zero/nonzero bank charges and multiple expenses.

USD-rate text is fictional sample data, not a live exchange rate. Dates were generated relative to 1 November 2026. Existing unavailable opening balances and unimplemented correction workflows remain unchanged.

Verification: 79 backend tests passed; main build zero warnings/errors; live WPF regression passed. Actual main SQLite integrity ok, no FK errors, expected record counts and pending totals, no old active master names or old audit rows.
