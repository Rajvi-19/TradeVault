# TradeVault desktop acceptance results

Synthetic data only; production database was not opened.

Build: UI 2026.10.28.14. PASS: 45; FAIL: 0.

| ID | Feature | Result | Evidence |
|---|---|---|---|
| A01 | Owner authentication | PASS | Assigned Owner role returned. |
| A02 | Operator authentication | PASS | Assigned Operator role returned. |
| A03 | Incorrect password rejected | PASS | Wrong credentials rejected by real password verifier. |
| A04 | Owner-only access enforcement | PASS | Operator denied owner report scope. |
| D01 | Fresh datasets empty | PASS | Fresh isolated SQLite database contains no company rows. |
| D02 | Synthetic 12-invoice save/reload | PASS | 12 invoices with 26 source fields persisted/reloaded. |
| D03 | Shipping default | PASS | All 12 invoice shipping inputs start at USD 3,800. |
| D04 | BL + terms due date | PASS | 17-Jun-2026 + 90 days = 15-Sep-2026. |
| D05 | Reminder and as-of overdue | PASS | Reminder 16-Aug; overdue 41 days as of 26-Oct. |
| D06 | Editable terms | PASS | Changing terms to 60 recalculates due to 16-Aug. |
| D07 | Editable shipping persistence | PASS | USD 4,200 remains after reload; historical default does not overwrite. |
| D08 | Invalid USD blocked; revision unchanged | PASS | Negative amount rejected without saving. |
| D09 | Sub-cent amount blocked | PASS | USD precision beyond cents is rejected. |
| D10 | Required party/invoice validation | PASS | Missing party blocks save and valid persisted data remains. |
| D11 | Invalid date blocked | PASS | Invalid calendar day rejected. |
| D12 | Unknown dates remain blank | PASS | No BL date produces no due/reminder date. |
| D13 | Stale update rejected | PASS | Revision conflict rejected instead of overwriting changes. |
| D14 | Deletion saved and reloaded | PASS | Delete underlying record, save and reload retains 11 records. |
| C01 | Details formula results | PASS | Per row: pending bank 7,000; net difference 1,200; received difference 1,200; running pending 0. |
| C02 | Cumulative receipt input recalculates | PASS | Changing manual bank-received input 3,000 to 5,000 reduces pending to 5,000. This is not receipt-allocation UI. |
| C03 | Corrected first-row difference rule | PASS | First invoice follows the same net-minus-receipts rule as later invoices. |
| C04 | Ledger debits/credits and opening | PASS | Opening 100 + 12 × (15,000 − 3,000 − 3,800) = 98,500. |
| C05 | Source workbook field counts | PASS | 26 invoice, 14 details, 9 ledger columns. |
| U01 | Date normalization | PASS | Day-first and named month normalize to ISO. |
| U02 | Invalid date input retained | PASS | Invalid date preserved so validation can explain it. |
| U12 | Calendar date validation is immediate | PASS | Impossible days and month lengths are flagged immediately; real leap days and blank optional dates remain valid. |
| U03 | Continuous blank sheet without stored blanks | PASS | 100 spare rows available independently of stored rows. |
| U04 | Rectangular paste | PASS | 2×2 range updates the selected input cells. |
| U05 | Paste date normalization | PASS | 1/2/2026 is interpreted as 1-Feb-2026. |
| U06 | Calculated cells locked; mixed paste atomic | PASS | Paste spanning shipping and locked due date is rejected before any changes. |
| U07 | Malformed paste rejected | PASS | Ragged range does not change cells. |
| U08 | Find / filter and clear | PASS | Matching one real row then clearing restores full worksheet. |
| U09 | Frozen identifiers and resizable columns | PASS | Two frozen identifiers; column resizing enabled. |
| U10 | Typing promotes row and preserves defaults | PASS | A spare row becomes a record only after input. |
| U11 | Read-only grid rejects paste | PASS | Read-only view refuses programmatic paste. |
| U13 | Tab commits valid dates and skips calculated cells | PASS | Invalid date keeps current cell; Tab/Shift+Tab navigate input columns and Tab wraps to next row. |
| U14 | Invalid pasted date is atomic | PASS | A range containing an impossible date is rejected before changing any input. |
| W04 | Workspace fits a 1366 by 768 desktop | PASS | Startup size accounts for taskbar space so caption and report navigation remain on screen. |
| W05 | All three worksheet tabs remain visible | PASS | Invoice register, Account details and Debit & credit button bounds are inside the 1366 by 728 workspace. |
| W01 | Visible caption controls on login and workspace | PASS | Both top-level windows have explicit minimize/maximize/close controls and resize chrome; OS click acceptance is separate. |
| W02 | All sheet layouts stay compact | PASS | Worksheet component begins above 290px including the explicit 36px caption at 1000×600 content size. |
| W03 | UI Save/reload handlers | PASS | Real MainWindow input → Save → reload round trip; Save disabled after success. |
| C06 | Register difference is calculated and protected | PASS | Calculation follows configured contract, overrides stale input, updates after edit/reload and locks F8. Both legacy ledger due columns follow BL + terms. |
| A05 | Logout invalidates session | PASS | Expired session rejected. |
| D15 | Database reopen persistence | PASS | 12 detail rows plus metadata survive database disposal/reopen. |
