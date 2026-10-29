# TradeVault — report handover, receipts and portable workflow

Build: UI 2026.10.28.1. All amounts are USD.

## Confirmed deletion and save state

Delete or the Delete toolbar button clears selected editable cells without a modal. Select a whole record by clicking its row number to request record deletion; only whole-record removal shows the selected record and party for confirmation. Keep record/Escape/closing the dialog cancels. Delete record now saves that record's removal immediately; Reload or reopening will not bring it back. Payment-history records remain protected. Stale-revision/validation failures leave the record in place. Other unsaved edits remain unsaved, so Save changes still applies to ordinary edits. Saved has a checkmark; pending edits show the blue Save changes button. Shortcuts is removed from the navbar, but keyboard shortcuts and F1 help remain. The main and login branding no longer shows Offline/USD mode badges. Currency units remain on amount fields and reports.

## Cutting cells and records

Ctrl+X cuts a selected editable cell or rectangular range: clipboard data is written before values are cleared. These are ordinary draft edits until Save. Clipboard failure leaves the values unchanged. Calculated cells and read-only reports cannot be cut/cleared. When a text editor has focus, native text Delete/Ctrl+X behavior is retained.

Select a whole record using its row number, then Ctrl+X to cut the record. Confirmation is required because its removal is saved immediately, like whole-row deletion. Cancel does not change the record or clipboard. The record is copied before storage removal; protected payment-history records cannot be removed. Ctrl+V pastes its visible fields into the matching worksheet and the app retains invoice terms via its own clipboard metadata. Destination party identity is inherited from the destination workspace; calculated values are recalculated. Only one whole record is removed at a time. Ordinary external text pastes continue to work.

## Simplified main screen

The main screen no longer has Recalculate, As of or Comfortable rows. Calculations refresh automatically while editing and on save. Reports, preview, print and main-screen Excel export include all saved invoices and recorded payments, including future-dated entries, without a manual cutoff. Prepared date is today's date; it does not claim to be a historical balance. Overdue arithmetic and reminders still use today's date. Receipt entry retains its actual payment/correction date. Historical reporting services remain available internally and existing submitted snapshots are unchanged.

The selected party appears beside the worksheet heading. Invoice Register hides the repeated party-name column; it retains party identity in each saved record and automatically sets it for new rows. Party selection still isolates records, balances and settings. The restyled selector supports keyboard selection and type-to-select. Unassigned records remain a temporary workspace for later assignment.

## Start safely

- Development: close any old running copy, then run dotnet run --no-restore --project Src/TradeVault.Desktop/TradeVault.Desktop.csproj.
- Portable: copy the ENTIRE Src/TradeVault.Desktop/bin/Portable/win-x64 folder onto each USB. Open TradeVault.Desktop.exe. Do not copy only the executable.
- Keep separate Owner and Operator application folders/drives. Data, Reports and Backups live beside each application's executable; no online synchronization is introduced.
- For an existing installation, close it and keep a separate verified backup before replacing application binaries. Preserve its Data/ and Backups/ folders. First startup upgrades older supported databases to schema 5 after an automatic pre-upgrade backup.
- A published application contains its Windows .NET runtime and native dependencies. Physical clean-PC and USB-ejection acceptance is still required.
- No company database was created or upgraded as part of this implementation's tests.

## Modes: preserved manual sheets and generated reports

Existing independently entered Account Details and Debit/Credit worksheets are preserved.

For a named party, Report setup enables a register-based workflow:
1. Select the bank-contract source explicitly: F6 or F7. The independent amounts are not silently equated.
2. Select the existing cumulative bank-receipt source explicitly: F22 dated F24, or F25 dated F26. Difference receipts use F23 dated F24.
3. Decide whether to deduct each register row's shipping amount once on that row's invoice date. The default is NOT to infer a shipping posting. Checking the box is explicit approval of this particular rule, not inference about per-container or paid-date policy.
4. Enter the effective date and any supplementary opening balances. Opening balances must exclude invoices already represented in the register, to avoid double counting.
5. Enable generated reports. A backup is made before configuration is saved.

After setup, the report tabs automatically show read-only reports derived from the saved register and dated allocations. Check Legacy manual reports to see/edit the original independent worksheets. Generated reports never overwrite those worksheets.

Report setup can be changed before receipts exist. After receipts are posted, mappings are protected to avoid silently reinterpreting history.

## Register and terms

- Every invoice has a stable internal ID and its own captured terms.
- New terms changes the default for future rows; it does not change existing invoices.
- Select a register row and use Selected terms to adjust that invoice deliberately.
- Duplicate invoice references within a party are rejected. Include the financial year/container suffix where separate records genuinely need distinct references.
- Original and the two customs amounts remain separate inputs. Register Difference Amount is locked and calculated as Original minus the contract source selected in Report setup. It stays blank if mapping or either required input is missing. Shipping is deducted only in the separate net-difference report calculation, when approved.
- Existing cumulative receipts are opening/baseline movements at the dates you supply. The app cannot invent the earlier payment history behind a cumulative amount.
- Invoices with receipt history cannot be deleted. Their historical amount/date inputs are protected; use explicit receipt corrections rather than overwriting history.
- Generated reporting requires complete invoice dates, source amounts and dates for nonzero baseline receipts. An incomplete register remains accessible for correction even when a generated report cannot be built.

## Customer payments and unpaid invoices

Open Payments / unpaid invoices for the selected customer. One row per invoice shows the unpaid bank portion, unpaid difference portion, total unpaid and payment due date at the selected payment date. Future invoices are excluded. Invoice balances come from application services; their sum does not subtract unassigned advances or invoice-unallocated opening balances.

1. Enter Date money was received, Total money received (USD), and a Payment description / bank transfer reference. The description is a note, not a bank-account selector.
2. Enter amounts in Pay bank portion and Pay difference portion against the relevant invoices. Bank means the contract portion; Difference means the portion remaining after approved shipping. Leave other amounts at zero.
3. The summary updates automatically: money received, assigned to invoices and unassigned advance. Unpaid after this payment previews each invoice balance. A separate Calculate summary button is no longer needed.
4. Save new payment validates and saves. Negative/invalid amounts, assignments above a portion's unpaid balance and assignments above the receipt total are blocked. Unassigned money is a customer advance; it reduces the ledger but does not settle an invoice until assigned.
5. Open Recorded payments to select an existing active payment. Edit selected payment / assign its advance loads that payment. The date label changes to the date the correction takes effect. Save payment correction records reversal plus replacement atomically. Its preview removes the original payment before showing available balances, so its amounts can be reassigned. Future payments are excluded from the dated preview; final save still validates against all recorded payments.
6. Cancel recorded payment asks for an effective date and reason. The original stays in history and its effect is reversed. This does not initiate a bank refund. Cancelled/replaced entries are marked and cannot be edited again.
7. Start new payment / clear form exits correction mode and clears draft entries. The form scrolls vertically, and the summary/save controls stay visible at the bottom. The invoice table also scrolls horizontally/vertically when needed.

Saving valid register edits or switching from the register to either report saves the register first and rebuilds the generated view from it. Invalid edits keep the user in place with an error. Customers without calculation settings are prompted to choose explicit sources before opening generated reports. Existing manual worksheets remain preserved. Invoice amount/date fields linked to receipt history remain protected; the change does not bypass historical safeguards.

Historical receipts/corrections are included at their effective dates. Both main report tabs now include all saved entries, with no selected reporting cutoff. Changing source invoice values does not reconstruct every previous invoice version; existing exported/imported snapshots stay frozen.

## Report preview, export and print

1. Save working edits.
2. Select a named party and the As of date.
3. Use Preview / Print for both reports with metadata, totals and notes.
4. Print both reports opens the standard Windows printer dialog. Physical printing/driver acceptance remains a manual check.
5. Export reports creates a unique folder under Reports/ with:
   - AccountDetails.xlsx
   - DebitCredit.xlsx
   - Print.html — offline landscape print layout, usable without Excel
   - Submission.tvreport — versioned handover package
6. Export runs off the UI thread; editing/closing is guarded while the export is in progress.

Excel exports contain numeric amounts and dates, frozen report headers, totals, print titles and landscape page setup. They contain snapshot values, not live links or editable financial formulas.

Generated mode filters invoices and dated receipts by As of. It uses current saved invoice values. It does NOT reconstruct invoice values before later edits, and cannot reconstruct payments that predate an imported cumulative baseline. Notes identify these boundaries.

Manual snapshot mode exports saved independent report values. Its report date is metadata, not a historical-filter guarantee. Do not present it as an automatically reconciled report.

Exports of the same party/date/mode reuse a submission lineage and increment its revision. Changing the date or mode creates a separate lineage.

## Owner import and review

1. Transfer Submission.tvreport manually or connect the source USB.
2. Owner opens Operator reports > Import report and selects the package.
3. Preview party, As of, revision, source ID, both reports, totals and calculation notes.
4. Close the preview and explicitly confirm import.
5. The Owner database stores the original package and frozen report separately from working parties/invoices.
6. Duplicate revisions are rejected. Conflicting content for an existing revision is rejected. Corrected revisions preserve previous revisions.
7. Open selected displays a read-only snapshot. Review status / note stores Owner annotations separately from report contents.
8. Restarting the Owner app or disconnecting the source does not remove imported reports.

Packages have bounded size, strict known-entry/schema validation and checksums; archive paths are never extracted. Changed files must be previewed again. Checksums establish integrity, NOT sender identity. Confirm the sender through the physical handover process; this version does not digitally authenticate or encrypt packages.

## Reminders and balances

Reminders shows settled, BL-pending, due within 30 days, overdue and not-yet-due invoices for the selected party. On opening the main window, configured parties are checked for due/overdue counts.

Unallocated advances reduce the party-ledger balance but do not automatically settle invoices. Invoice aging therefore remains distinct from net party credit. Opening balances are not assigned fictitious invoice due dates.

## Backup and restore

- Backups > Create verified backup uses SQLite's consistent backup API and integrity checking.
- Owner can restore a selected file from the application's Backups folder.
- Restore checks integrity, supported version, required tables, credentials and foreign-key relationships.
- A safety backup is made first. Schema-2/3 backups are upgraded after restoration; failed upgrades roll back to the safety copy.
- Successful restore signs out all sessions. Credentials and imported submissions also come from the backup; know the credentials valid in that backup.
- Keep a copy on a DIFFERENT USB for lost-drive recovery.
- The login password does not encrypt the SQLite database. Confidentiality/key-recovery policy must be agreed before sensitive deployment.

## Passwords and audit

Workspace Password buttons have been removed. Use Forgot password on the sign-in page with the single-use recovery code explicitly created after authenticating. A setup link is available for users who still know their current password. Both usernames and passwords are case-sensitive. Recovery invalidates affected sessions and consumes the code; generate a new one after signing in. See [Offline account recovery](Offline-Account-Recovery.md) for setup, backup and lost-code limits. There is no hidden master-password or Owner-password bypass.

Worksheet saves and workflow operations have actor/time/before/after audit records. Import review audit entries retain metadata/hash references rather than duplicating the entire original package.

## Verification and release boundaries

Run Src/TradeVault.Desktop/Verify-Workflow.ps1 for Desktop build, backend/workflow tests, live UI-handler checks and acceptance cases. Publish-Usb.ps1 builds the runtime-contained folder. Release startup disables sample seeding.

Software checks do not certify physical USB failure behavior, clean-machine launch, printer drivers, high-DPI/accessibility or actual caption/clipboard mouse interaction. Those require the final device/business acceptance checklist.

Older normalized invoice/receipt tables are preserved but not silently migrated into this workflow. Register-based generated reports and dated receipt books are the new UI path. Existing manual sheets remain accessible; don't enter the same payment in both baseline fields and the new receipt book.
