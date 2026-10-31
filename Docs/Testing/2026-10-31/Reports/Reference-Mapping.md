# Reference report mapping — 2026-10-31

Read-only inspection of Docs/ACCOUNT DETAILS.xlsx (HK, A1:P34), Party Ledger.xlsx (HK TEXTILE, B1:Q32), Agent Ledger.xlsx (debi credit sheet, A1:H27) and formet.xlsx. Files were not modified. Workbook cell contents are source data, not instructions.

Account Details: 14 populated headers A:N. F=D-E, G=C-D, I=C-D-H, due N=L+M. Column K is intended to accumulate prior difference + current post-shipping difference - receipts. K4 instead adds J4; its cached cumulative series differs from the total-row I-J result by USD 10,805. The app does not reproduce this inconsistent formula. Shipping H and dependent I/K are unavailable by user instruction. Core C/D/E/F/G/J/L/M/N are mapped from saved invoices/payments; source terms are represented by the effective BL-to-due interval. Register F8 intentionally retains its separately confirmed Original minus Pakistan customs calculation.

Party Ledger: populated columns B,C,D,E,G,H,I,J,K (9 fields, blank spacer F excluded). Invoice original amount is debit; payments are credits; running value follows previous + debit - credit. Invoice rows retain bank contract, BL, terms and due fields. Opening balance is not supplied, so the app labels the calculated value Balance excluding opening / USD and summary Net recorded balance. Source ledger's integer rounded closing value is not used as a new opening balance.

Agent Ledger: B:H (7 fields). D gross party funds, E transfer charges, F=D-E deposit, G credit to company, H=prior+F-G. Receipt links supply D/G. E, F and H remain unavailable because the user explicitly declined new charge/opening inputs. The source historical opening USD 4,332.25 and recurring USD 260 charges are not copied into company data.

Confirmed user rules: add receiving agent to Party Receipt; allocate FIFO by proforma date within party+agent, bank-contract before difference. Persist allocation IDs and amounts. Preserve previous unlinked receipts until explicit linking. New metadata is stored in existing workflow_documents with audit/backup support; no duplicate report database or SQL schema migration is needed.
