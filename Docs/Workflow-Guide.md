> Current behavior (2026-10-29): TradeVault opens directly to Invoice Register. There are no accounts, roles, role selector or Operator Reports page.

# TradeVault — invoices, accounts and payments

Build: UI 2026.10.29.17. All amounts are USD.

## Confirmed deletion and save state

Delete or the Delete toolbar button clears selected editable cells without a modal. Select a whole record by clicking its row number to request record deletion; only whole-record removal shows the selected record and party for confirmation. Keep record/Escape/closing the dialog cancels. Delete record now saves that record's removal immediately; Reload or reopening will not bring it back. Payment-history records remain protected. Stale-revision/validation failures leave the record in place. Other unsaved edits remain unsaved, so Save changes still applies to ordinary edits. Saved has a checkmark; pending edits show the blue Save changes button. Shortcuts is removed from the navbar, but keyboard shortcuts and F1 help remain. The header no longer shows Offline/USD mode badges. Currency units remain on amount fields and reports.

## Cutting cells and records

Ctrl+X cuts a selected editable cell or rectangular range: clipboard data is written before values are cleared. These are ordinary draft edits until Save. Clipboard failure leaves the values unchanged. Calculated cells and read-only reports cannot be cut/cleared. When a text editor has focus, native text Delete/Ctrl+X behavior is retained.

Select a whole record using its row number, then Ctrl+X to cut the record. Cut acts immediately without a confirmation dialog. Use Ctrl+Z to undo it. The record is copied before storage removal; protected payment-history records cannot be removed. Ctrl+V pastes its visible fields into the matching worksheet and the app retains invoice terms via its own clipboard metadata. Destination party identity is inherited from the destination workspace; calculated values are recalculated. Only one whole record is removed at a time. Ordinary external text pastes continue to work.

## Undo and redo

Ctrl+Z undoes the last worksheet change; Ctrl+Y redoes it. Both are listed in F1 help. There are no on-screen undo/redo buttons. Edits, pasted ranges, cell clears/cuts, new rows and whole-record removal can be undone. Calculations refresh from the restored inputs.

History stays in memory for the active worksheet (up to 100 steps, bounded by size). Reloading, switching party/tab, or closing clears it. A new edit clears redo. Ordinary cell changes remain drafts until Save. Undoing/redoing an immediately saved cut/deletion restores/removes only that stored record immediately; unrelated draft edits stay unsaved. Existing validation, receipt-history protection and revision-conflict checks still apply. History does not reverse receipts, exports, backups, party creation or another application's clipboard contents.

## Simplified main screen

The main screen no longer has Recalculate, As of or Comfortable rows. Calculations refresh automatically while editing and on save. Reports, preview, print and main-screen Excel export include all saved invoices and recorded payments, including future-dated entries, without a manual cutoff. Prepared date is today's date; it does not claim to be a historical balance. Overdue arithmetic and reminders still use today's date. Receipt entry retains its actual payment/correction date. Historical reporting services remain available internally and existing submitted snapshots are unchanged.

The selected party appears beside the worksheet heading. Invoice Register hides the repeated party-name column; it retains party identity in each saved record and automatically sets it for new rows. Party selection still isolates records, balances and settings. The restyled selector supports keyboard selection and type-to-select. Unassigned records remain a temporary workspace for later assignment.

## Start safely

- Development: close any old running copy, then run dotnet run --no-restore --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj.
- Portable: copy the ENTIRE Src/TradeVault.Desktop/bin/Portable/win-x64 folder onto each USB. Open TradeVault.Desktop.exe. Do not copy only the executable.
- Data, Reports and Backups stay beside the executable. Do not synchronize a live database.
- For an existing installation, close it and keep a separate verified backup before replacing application binaries. Preserve its Data/ and Backups/ folders. First startup upgrades older supported databases to schema 5 after an automatic pre-upgrade backup.
- A published application contains its Windows .NET runtime and native dependencies. Physical clean-PC and USB-ejection acceptance is still required.
- No company database was created or upgraded as part of this implementation's tests.

## Modes: preserved manual sheets and generated reports

Existing independently entered Account Details and Debit/Credit worksheets are preserved.

For every named party, the report tabs automatically show read-only reports from the saved Invoice Register and dated payments. There is no Report setup screen or saved report configuration.

- Bank-contract portion always uses Our customs invoice amount (F6).
- Earlier bank payments always use Nemichem receipts (F25), dated by Nemichem receipt date (F26).
- Difference receipts retain their separate F23 amount and F24 date.
- No automatic shipping deduction or supplementary opening balances are applied. Shipping amounts remain recorded in Invoice Register; they do not represent payments.
- Existing independent worksheets remain stored and are not overwritten by generated reports. Unassigned records retain their legacy worksheets until assigned to a party.

## Register and terms

- Every invoice has a stable internal ID and its own captured terms.
- New terms changes the default for future rows; it does not change existing invoices.
- Select a register row and use Selected terms to adjust that invoice deliberately.
- Duplicate invoice references within a party are rejected. Include the financial year/container suffix where separate records genuinely need distinct references.
- Original and the two customs amounts remain separate inputs. Register Difference before shipping / USD is locked and always calculated as Original invoice amount minus Pakistan customs invoice amount. It stays blank if either amount is missing and does not require setup. Shipping is not automatically deducted from generated balances.
- Existing cumulative receipts are opening/baseline movements at the dates you supply. The app cannot invent the earlier payment history behind a cumulative amount.
- Invoices with receipt history cannot be deleted. Their historical amount/date inputs are protected; use explicit receipt corrections rather than overwriting history.
- Generated reporting requires complete invoice dates, source amounts and dates for nonzero baseline receipts. An incomplete register remains accessible for correction even when a generated report cannot be built.

## Customer payments and unpaid invoices

Open Receipts for the selected customer. One row per invoice shows the unpaid bank portion, unpaid difference portion, total unpaid and payment due date at the selected payment date. Future invoices are excluded. Invoice balances come from application services; their sum does not subtract unassigned advances .

1. Enter Payment date, Amount received (USD), and an optional Reference / description. The description is a note, not a bank-account selector.
2. Enter amounts in Pay bank and Pay difference against the relevant invoices. Bank means the contract portion; Difference means Original invoice amount minus Our customs invoice amount for account reports. The separate Invoice Register Difference before shipping column remains Original minus Pakistan customs. Leave other amounts at zero.
3. The summary updates automatically: money received, assigned to invoices and unassigned advance. Unpaid after this payment previews each invoice balance. A separate Calculate summary button is no longer needed.
4. Save payment validates and saves. Negative/invalid amounts, assignments above a portion's unpaid balance and assignments above the receipt total are blocked. Unassigned money is a customer advance; it reduces the ledger but does not settle an invoice until assigned.
5. Open Payment history to select an existing active payment. Edit payment loads that payment. The date label changes to the date the correction takes effect. Save correction records reversal plus replacement atomically. Its preview removes the original payment before showing available balances, so its amounts can be reassigned. Future payments are excluded from the dated preview; final save still validates against all recorded payments.
6. Cancel payment asks for an effective date and reason. The original stays in history and its effect is reversed. This does not initiate a bank refund. Cancelled/replaced entries are marked and cannot be edited again.
7. Clear form exits correction mode and clears draft entries. The form scrolls vertically, and the summary/save controls stay visible at the bottom. The invoice table also scrolls horizontally/vertically when needed.

Saving valid register edits or switching from the register to either report saves the register first and rebuilds the generated view from it. Invalid edits keep the user in place with an error. All named parties use the fixed sources automatically. Existing manual worksheets remain preserved. Invoice amount/date fields linked to receipt history remain protected; the change does not bypass historical safeguards.

Historical receipts/corrections are included at their effective dates. Both main report tabs now include all saved entries, with no selected reporting cutoff. Changing source invoice values does not reconstruct every previous invoice version; existing exported reports stay frozen.

## Report preview, export and print

1. Select a party and save working edits.
2. Use Preview / Print to inspect Account Details and Debit & Credit with totals.
3. Save PDF or print through Windows. Printer/driver acceptance remains manual.
4. Export Excel creates one .xlsx report with both report worksheets in Reports beside the application. Exports retain snapshot values, numeric amounts/dates, frozen headers, totals and landscape print setup.
5. The main views and exports include all saved records. Effective receipt dates still control payment history and reminders. Exported report revisions remain available as ordinary files; there is no report-import or review inbox.

## Reminders and balances

Reminders shows settled, BL-pending, due within 30 days, overdue and not-yet-due invoices for the selected party. On opening the main window, configured parties are checked for due/overdue counts.

Unallocated advances reduce the party-ledger balance but do not automatically settle invoices. Invoice aging therefore remains distinct from net party credit. Opening balances are not assigned fictitious invoice due dates.

## Backup and restore

- Backups > Create verified backup uses SQLite's consistent backup API and integrity checking.
- Restore a selected file from the application's Backups folder.
- Restore checks integrity, supported version, required business tables and foreign-key relationships.
- A safety backup is made first. Schema-2/3 backups are upgraded after restoration; failed upgrades roll back to the safety copy.
- Successful restore closes the workspace. Reopen TradeVault to view the restored records. Older backups are upgraded and obsolete account tables removed.
- Keep a copy on a DIFFERENT USB for lost-drive recovery.
- The SQLite database is not encrypted. Keep the application and backup drives physically secure.

## Local data and audit

No sign-in or role system is present. Schema 7 removes obsolete role settings and imported-report submissions/reviews. Invoices, parties, receipts, report settings and normal exports remain. A verified pre-upgrade backup is retained. Restoring an older backup migrates its working copy to the current schema.

New audit entries identify Local workspace rather than a person or role. Historical business audit entries remain intact. Prior backups and testing records are historical evidence, not active application features.

## Verification and release boundaries

Run Src/TradeVault.Desktop/Verify-Workflow.ps1 for Desktop build, backend/workflow tests, live UI-handler checks and acceptance cases. Publish-Usb.ps1 builds the runtime-contained folder. Release startup disables sample seeding.

Software checks do not certify physical USB failure behavior, clean-machine launch, printer drivers, high-DPI/accessibility or actual caption/clipboard mouse interaction. Those require the final device/business acceptance checklist.

Older normalized invoice/receipt tables are preserved but not silently migrated into this workflow. Register-based generated reports and dated receipt books are the new UI path. Existing manual sheets remain accessible; don't enter the same payment in both baseline fields and the new receipt book.

## Worksheet selection and scrolling

Click a row number to select its full record. Hold Ctrl and click other row numbers to add records; Ctrl-click a selected row to deselect it. Plain clicking another row selects just that row. Editable date columns are included in full-record copy/paste; calculated dates are recalculated instead of pasted.

Horizontal touchpad wheel gestures scroll the sheet sideways. Shift+mouse wheel is also supported. Toolbar order is Receipts, Reminders, Preview / Print, Export Excel, Backups.

After cutting or deleting a full record, its row remains blank and lower records retain their worksheet positions. For example, clearing record 3 leaves row 3 blank and record 4 stays on row 4. Blank positions survive saving/reopening and are excluded from invoice counts and reports. Type or paste into the blank row to reuse it with a new invoice identity. Row numbers represent current worksheet positions; invoice references are unchanged. Horizontal scrolling accepts standard/pointer horizontal wheels, driver-shifted wheels, legacy horizontal-scroll requests and horizontal Windows pan gestures.

## Appearance

Use the small moon/sun square immediately after Saved or Save changes to switch between light and dark. Click again to return. The selection is remembered in Data/ui-settings.json beside this application, so it travels with the USB copy. Switching preserves current edits and undo history. Worksheet/report viewers and app dialogs follow the theme; exported/printed reports retain white pages. Native Windows dialogs follow Windows appearance.

