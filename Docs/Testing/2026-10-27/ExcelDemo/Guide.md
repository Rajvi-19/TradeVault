# TradeVault Excel-derived test workspace — build .9

Open TradeVault.Desktop.exe in this folder. Log in, then select [TEST] Account Details workbook sample in the Party selector. The initial Unassigned records sheet is empty by design. Account details and Debit & credit show generated reports. Select [TEST] Receipt allocation practice to practise receipts/corrections/advances. Keep this folder separate from your working application. Do not copy its Data folder into a company USB.

## What is loaded

- 14 invoice rows copied from the Account Details workbook, retaining its original amount, bank contract, bank received, shipping, difference received, BL date and supplied terms values.
- Party names and invoice/PI identifiers are replaced by TEST/DEMO labels. Amounts come from your reference workbook and are not independently fabricated; treat this package accordingly.
- Workbook bank contract D is placed in Our Customs (F6) solely for the explicit TEST mapping. This does not establish that mapping for company data. Pak Customs remains blank.
- Missing BL dates stay blank (BL pending). Missing shipping is zero, preserving workbook arithmetic. Missing terms on the last row use the agreed 90-day default.
- Invoice dates are parsed from descriptions. Source row 6 has no invoice date; its TEST date is BL minus 30 days. Payment dates are not supplied by the workbook; TEST baseline payments use one day after the later of invoice/BL date. These dates are synthetic and historical reports are for testing only.
- Unknown container/BL identifiers and proforma dates are left empty, not invented.
- Legacy manual Account Details preserves the source round-off row 8 as a USD 0.25 adjustment. Generated reports omit this non-invoice adjustment because no approved dated adjustment workflow exists. Generated difference pending is USD 312,880.00; corrected legacy balance including that adjustment is USD 312,879.75. The source's first-row running-balance error is not reproduced.
- A second party contains two workbook-derived invoices with zero baseline receipts and two clearly marked synthetic receipts. The first receipt is USD 1,500, allocated USD 800 and USD 400 to two invoices' bank portions and USD 50 to a difference portion, leaving USD 250 unallocated. The second receipt allocates USD 500 to the first invoice's bank portion.
- Reports/ already contains Excel, HTML and .tvreport exports for each TEST party. Export again after editing to create another revision. Owner import should be tested in another application's independent database, not confused with the Operator working database.

## Quick checks

1. Select the workbook sample party: see 14 invoices; first Difference Amount is USD 14,605.00 (64,330 minus 49,725).
2. In Invoice register edit Original Amount, press Tab: Difference Amount recalculates. Typing/pasting into calculated fields is blocked. Save and reload to confirm persistence.
3. Open Account details: first four invoices have zero bank/difference outstanding under the TEST baseline assumptions; five later invoices have BL pending. Do not copy source cached running balances.
4. Open Debit & credit: generated closing balance is USD 713,243.00 before your edits. Bank pending is USD 400,363.00; difference pending is USD 312,880.00.
5. Choose Receipt allocation practice, open Receipts: total received USD 2,000; allocated USD 1,750; unallocated USD 250. Practise correction/reversal and exporting a new revision.
6. Legacy manual reports shows the preserved manually shaped Account Details, including round-off; turn it off for generated reports. The legacy Debit/Credit worksheet is empty; use the generated ledger.
7. Use backups before destructive testing. This demo database is included intentionally. Your existing company database was not opened or modified.

The original reference XLSX files were not modified. Generated due dates, totals and running balances are calculated in the app; exported Excel contains snapshot values, not live formulas. No physical USB/printer acceptance is implied.
