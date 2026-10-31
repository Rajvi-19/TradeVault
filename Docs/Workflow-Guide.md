# TradeVault user guide

Build UI 2026.10.31.16 — all amounts USD. TradeVault opens directly to Invoice Register with no login or roles. The six main tabs are Party Master, Agent Master, Invoice Register, Party Receipt, Agent Receipt and Reports.

## Setup and invoices

Create buyers in Party Master and intermediaries in Agent Master. In Invoice Register choose New Invoice, select the party and agent, and enter the invoice identity. Party, agent, invoice number/date and country are fixed after creation. Complete the shipment and monetary fields in the party-specific register. All parties is a read-only overview. Party, agent and search filters combine; filtered saving preserves hidden invoices.

Register Difference amount = Original minus Pakistan customs. Payment due date defaults to agent-country BL date plus 90 calendar days; an explicit due-date override is retained. The 30-day reminder follows that deadline. Save changes / Ctrl+S saves normal invoice edits. Invalid dates and amounts must be corrected first.

## Two payment legs

Party Receipt records destination buyer -> receiving intermediary. Agent Receipt records intermediary -> your company. Do not enter the same money as a second Party Receipt when the agent remits it onward.

Choose Add New Receipt (Ctrl+N on either receipt page). Select the party or agent from its master. Party Receipt also requires Receiving agent. Payment date defaults to today and is editable. Enter a positive USD value (up to two decimals), optionally a reference/description, then Save receipt. USD is already converted; there is no exchange-rate calculation. Cancel saves nothing. Saving immediately updates the history table and database; no separate Save changes step is needed.

### Party receipt allocation

At save, the backend considers only that party's invoices assigned to the selected receiving agent. Eligible invoices are ordered by proforma date, then invoice number and stable ID for ties. It clears each invoice's bank-contract portion first (Our customs amount), then its difference (Original minus Our customs), before moving to the next invoice. Existing allocated/baseline payments are reserved and cannot be paid twice. Future-dated historical payments and reversals are checked to avoid interim overpayment.

Excess remains unallocated/advance; it reduces the party account but does not settle a specific invoice. Invoices with a future proforma date are not prepaid by this allocator. If the oldest eligible invoice lacks valid original/customs amounts, allocation stops and the remaining receipt stays unallocated. Complete invoice data before saving the receipt when allocation is needed. Saved allocations are retained; completing a later invoice does not silently reassign an existing advance. Advance reassignment/correction is not included in this phase.

Party Receipt shows party, receiving agent, payment date, receipt amount in USD, USD value and description. The previous allocated/unallocated columns are hidden; FIFO allocation still runs in the backend. USD value is an optional plain text field recording the value of a dollar at payment time (for example 280 PKR). It does not convert, multiply or otherwise change the USD receipt amount. Older receipts have a blank USD value because none was recorded. Older receipts without a recorded receiving agent remain unlinked; the Set receiving agent action is no longer available. New Party Receipts still require receiving-agent selection. Allocated invoice amounts/agent references and deletion/cut are protected.

Agent Receipt records remittances and does not reduce party debt a second time. Both history tables support search, sorting, copying, resizing, scrolling, F5 reload and current-page export. Ordinary saved receipt rows are read-only.

## Reports

Open Reports and choose Account Details, Party Ledger or Agent Ledger. Choose a party for the first two, or an agent for Agent Ledger. Selecting a report or party/agent rebuilds it from saved data; F5 also reloads it. There is no Refresh button. Reports are read-only and include all saved transaction dates, not a historical reporting cutoff. Search filters displayed rows; summary values continue to cover the full selected account.

- Account Details follows the reference with 13 visible columns (Terms removed). Original, bank-contract, bank receipts/pending, difference, received difference, BL and due dates come from invoices and saved allocations. Account difference is Original minus Our customs, unlike the register's Pakistan-customs difference. Historical receipt allocations/baselines are included once. Unlinked or excess receipts appear as unallocated amounts.
- Party Ledger follows the reference with 8 visible fields (Terms removed). Original invoices are debits; party receipts and preserved historical payments are credits. The running value is net recorded movements excluding any opening adjustment, and is labelled accordingly. Agent-to-company remittances are not another party credit. Missing invoice amounts make the affected running balance unavailable.
- Agent Ledger follows the 7 reference fields: date, description, party money sent, bank charges, deposit to agent, credit to company and pending. Linked party receipts are incoming gross money; Agent Receipts are onward company credits. Older unlinked receipts are counted in a note and excluded from any guessed agent assignment.

Shipping deductions, bank charges and opening balances intentionally have no input in this phase. Dependent cells (account shipping/post-shipping cumulative balances and agent net deposits/pending) remain blank/unavailable. A blank is not zero. Expand Calculation notes and missing inputs for details. No default USD 3,800 shipping or USD 260 bank charge is inferred from the examples.

## Export, theme and shortcuts

Export -> Excel or PDF exports the selected page/report's visible rows and column order. Ctrl+E exports Excel; Ctrl+P exports PDF. The selected report/account is used in report exports. Wide report PDFs repeat invoice identifiers across column groups. Exports default beside the app; choose a USB destination to keep exported files there. External Excel/PDF programs follow their own caching/recent-file behavior.

The square moon/sun button after Saved toggles theme; the preference stays in Data/ui-settings.json beside the executable. F1 lists shortcuts. Ctrl+1..6 select the six tabs. Ctrl+F searches, F5 reloads. Worksheet Ctrl+X cuts without confirmation; Ctrl+Z/Ctrl+Y undo/redo within the current worksheet session. Delete clears selected editable cells; whole-row deletion requires confirmation and persists immediately. Deleted/cut rows stay blank. Financially linked invoices cannot be removed or have protected values changed.

## Storage and recovery

Copy the whole self-contained application folder to USB and launch TradeVault.Desktop.exe. Data and Backups remain beside it. Reports are calculated views, not independent editable copies or another database. Current schema is 13; optional receipt metadata is backward-compatible. Existing records are not auto-assigned to agents or silently migrated into new payments. Backups include the stored receipt links and allocations. Save/close before ejecting or updating; never synchronize an open database. A backup on the same USB does not protect against losing the drive. The database is not encrypted. Use Backups in the title bar for available backup/restore actions.

## Fresh demo records (2026-10-31)

The active database now contains a new [DEMO] dataset: three buyers, three agents, nine invoices, four party receipts and three agent remittances. See Docs/Testing/2026-10-31/FreshDemo/Testing-Guide.md in the project for expected totals and scenarios. Old marked test records were replaced after a backup; the unmarked Dhyan party was preserved, with its copied TEST invoices removed. Stored historical audit events and backups remain for recovery. Sample USD value text is fictional, not a live exchange rate.
