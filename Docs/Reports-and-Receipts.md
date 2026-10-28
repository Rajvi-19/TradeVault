# Reports and receipts

## As of
This is the reporting cutoff, not an invoice date. Generated reports include invoice and receipt movements effective on or before it; overdue days use that date. Example: an invoice due 15 September shows 10 overdue days on 25 September. A receipt dated 26 September appears when the cutoff reaches 26 September. Change As of and click Recalculate, then inspect Account details, Debit & credit, Reminders, Preview / Print, or Excel export. Historical reports use current invoice values filtered by dates; they do not reconstruct every earlier edit.

## Receipts example
Complete Report setup, save invoice records, then open Receipts for the selected party. Suppose INV-001 has USD 10,000 bank outstanding and INV-002 has USD 5,000 bank outstanding. Receive USD 8,000 on 20 October: enter that date, total and reference, allocate USD 6,000 to INV-001 Bank and USD 2,000 to INV-002 Bank. Calculate summary shows USD 8,000 allocated, zero unallocated. Post receipt. Remaining bank balances become USD 4,000 and USD 3,000. A later USD 1,000 allocation to INV-001 makes its bank balance USD 3,000. Difference allocations reduce the separate difference balance, not the bank balance. Unallocated money is an advance and does not settle an invoice until allocated. Do not enter the same money both as an opening cumulative receipt in the register and as a new receipt. Correct/reverse a mistaken receipt with an effective correction date to preserve history.

## Report setup
Setup defines which existing Excel amount is the bank contract source (F6 or F7), which cumulative receipt field is the starting source (F22 or F25), opening balances and their effective date, and whether registered shipping reduces balances. These fields have different meanings and cannot safely be guessed. Configure once per party before generated reports and receipts. Mapping changes are blocked after receipts exist to keep posted allocations consistent. Historical manual worksheet data remains stored; the legacy switch has been removed.

## Recalculate
Recalculate refreshes derived amounts, dates and balances after edits or changing the reporting cutoff. It does not post a receipt or save draft entries. Save changes before exporting or opening workflows. Generated report tabs are read-only projections of the saved register and receipt book.

## Export and print
Export Excel creates one .xlsx workbook with two visible tabs (Account Details and Debit Credit) under Reports/<party name> on the application drive, with no explanatory notes below the totals. Its filename contains party name, report date and revision, for example `Meridian - Account Reports - 2026-10-28 - Revision 1.xlsx`. The application opens it using the installed spreadsheet application after saving. If none is available, it displays the saved path.

The Owner imports this same original TradeVault Excel file in Operator reports. There is no separate Create Owner submission action and no .tvreport transfer. The workbook includes application metadata for identifying the party, source, cutoff and revision. Import checks both visible report tables against that snapshot, retains duplicate/revision checks, and protects against changing the selected file after preview. Arbitrary spreadsheets and edited/resaved reports are not accepted; export a fresh original workbook for handover. Sender identity still depends on the physical handover process. Existing imported history remains readable in the database.

Preview / Print → Save PDF creates the PDF directly, without a printer driver, then opens the saved file. Save it on the USB. The separate Print button opens Windows printing and falls back to direct PDF saving if Windows fails. Pages use landscape orientation, repeated party/report identity and column headings, formatted USD numbers, closing totals and page numbers. The direct PDF uses high-resolution page images (text is not searchable/selectable). Long cell text can be truncated to fit fixed row heights; inspect output for unusually long references. Physical printer behavior still requires manual acceptance.

## Copying a complete row
Copy the row, select the first cell of a blank row and paste. Full-width pastes copy all input columns and skip calculated columns, which are recalculated. Supply a new unique invoice number before saving a new invoice. Partial ranges crossing calculated columns remain rejected atomically to avoid silently misaligning input.
