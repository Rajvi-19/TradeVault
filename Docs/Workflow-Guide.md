# TradeVault — report handover, receipts and portable workflow

Build: UI 2026.10.28.1. All amounts are USD.

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

## Dated receipts and allocations

Use Receipts for the selected party:
- Enter receipt date, total USD and reference/account.
- Allocate Bank and/or Difference amounts to one or several invoices.
- Calculate summary shows receipt, allocated and unallocated amounts.
- Post receipt validates precision, dates, party ownership, duplicate allocation lines, receipt over-allocation and invoice-component overpayment.
- Leave allocations at zero to record an advance. It appears as a party-ledger credit without settling a specific invoice.
- Correct selected / allocate an advance loads an original receipt into the editor. Posting creates a dated reversal and replacement atomically. This can allocate an earlier advance after invoices exist.
- Reverse selected receipt records an explicit dated correction with a reason. It does not delete the original.
- A failed correction appends nothing and leaves the original receipt in effect.

Historical receipt allocations and reversals are included on their effective dates. Settled invoices are not shown as overdue in generated reports/reminders. The register also uses settlement-aware overdue arithmetic after mapping is configured. Legacy unconfigured sheets retain their manual/calendar behavior.

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
