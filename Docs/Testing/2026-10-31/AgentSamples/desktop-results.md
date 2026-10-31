# TradeVault desktop acceptance results

Synthetic data only; production database was not opened.

Build: UI 2026.10.31.12. PASS: 52; FAIL: 0.

| ID | Feature | Result | Evidence |
|---|---|---|---|
| D01 | Fresh datasets empty | PASS | Fresh isolated SQLite database contains no company rows. |
| D02 | Synthetic 12-invoice save/reload | PASS | 12 invoices with current register fields and preserved receipt history persisted/reloaded. |
| D03 | Removed register fields | PASS | Shipping and historical receipt inputs are absent from the register. |
| D04 | BL + terms due date | PASS | 17-Jun-2026 + 90 days = 15-Sep-2026. |
| D05 | Reminder and as-of overdue | PASS | Reminder 16-Aug; overdue 41 days as of 26-Oct. |
| D06 | Editable due date | PASS | Custom deadline drives reminder; clearing restores BL plus 90. |
| D07 | Shipping cannot persist | PASS | Removed shipping values are discarded at the storage boundary. |
| D08 | Invalid USD blocked; revision unchanged | PASS | Negative amount rejected without saving. |
| D09 | Sub-cent amount blocked | PASS | USD precision beyond cents is rejected. |
| D10 | Required party/invoice validation | PASS | Missing party blocks save and valid persisted data remains. |
| D11 | Invalid date blocked | PASS | Invalid calendar day rejected. |
| D12 | Unknown dates remain blank | PASS | No BL date produces no due/reminder date. |
| D13 | Stale update rejected | PASS | Revision conflict rejected instead of overwriting changes. |
| D14 | Deletion saved and reloaded | PASS | Delete underlying record, save and reload retains 11 records. |
| C01 | Details formula results | PASS | Per row: pending bank 7,000; difference 5,000 less receipts 1,200 increases running pending by 3,800. |
| C02 | Cumulative receipt input recalculates | PASS | Changing manual bank-received input 3,000 to 5,000 reduces pending to 5,000. This is not receipt-allocation UI. |
| C03 | Corrected first-row difference rule | PASS | First invoice follows the same net-minus-receipts rule as later invoices. |
| C04 | Ledger debits/credits and opening | PASS | Opening 100 + 12 × (15,000 − 3,000) = 144,100. |
| C05 | Source workbook field counts | PASS | 20 visible invoice fields plus hidden party, 13 details, 9 ledger columns. |
| U01 | Date normalization | PASS | Day-first and named month normalize to ISO. |
| U02 | Invalid date input retained | PASS | Invalid date preserved so validation can explain it. |
| U12 | Calendar date validation is immediate | PASS | Impossible days and month lengths are flagged immediately; real leap days and blank optional dates remain valid. |
| U03 | Continuous blank sheet without stored blanks | PASS | 100 spare rows available independently of stored rows. |
| U04 | Rectangular paste | PASS | 2×2 range updates the selected input cells. |
| U05 | Paste date normalization | PASS | 1/2/2026 is interpreted as 1-Feb-2026. |
| U06 | Calculated cells locked; mixed paste atomic | PASS | Paste spanning due date and locked reminder is rejected before any changes. |
| U07 | Malformed paste rejected | PASS | Ragged range does not change cells. |
| U08 | Find / filter and clear | PASS | Matching one real row then clearing restores full worksheet. |
| U09 | Frozen identifiers and resizable columns | PASS | Two frozen identifiers; column resizing enabled. |
| U10 | Typing promotes row and preserves defaults | PASS | A spare row becomes a record only after input. |
| U11 | Read-only grid rejects paste | PASS | Read-only view refuses programmatic paste. |
| U13 | Tab commits valid dates and skips calculated cells | PASS | Invalid date keeps current cell; Tab/Shift+Tab navigate input columns and Tab wraps to next row. |
| U14 | Invalid pasted date is atomic | PASS | A range containing an impossible date is rejected before changing any input. |
| U15 | Row header selects the entire visible record | PASS | Row-header handler selects every visible column of one record, including calculated cells. |
| U16 | Cell cut clears only selected editable values | PASS | Ctrl+X path copies before clearing; cells do not remove rows, clipboard failure is non-destructive, and formulas stay locked. |
| U17 | Whole-record copy includes all editable dates | PASS | WPF clipboard bindings round-trip all five visible input dates; calculated dates stay locked. |
| U18 | Ctrl-click row selection toggles multiple records | PASS | Two nonadjacent rows remain selected; Ctrl toggles and plain click replaces selection. |
| U19 | Horizontal wheel scrolls worksheet in both directions | PASS | Horizontal wheel deltas move the sheet right and left while retaining frozen columns. |
| U20 | Native Windows touchpad messages reach the worksheet | PASS | Real HWND dispatch covers horizontal mouse/pointer wheels, shifted-wheel drivers and legacy horizontal scrolling without cached mouse-over. |
| U21 | Cleared row stays in place without shifting later records | PASS | Clearing row 3 keeps it blank and record 4 at row 4; worksheet position numbers stay unchanged. |
| W04 | Workspace fits a 1366 by 768 desktop | PASS | Startup size accounts for taskbar space so caption and report navigation remain on screen. |
| W05 | All six workspace tabs remain visible | PASS | Party Master, Agent Master, Invoice Register, Party Receipt, Agent Receipt and Reports button bounds are inside the 1366 by 728 workspace. |
| W01 | Visible caption controls on workspace | PASS | The workspace window have explicit minimize/maximize/close controls and resize chrome; OS click acceptance is separate. |
| W02 | All sheet layouts stay compact | PASS | Worksheet component begins above 290px including the explicit 36px caption at 1000×600 content size. |
| W03 | UI Save/reload handlers | PASS | Real MainWindow input → Save → reload round trip; Save disabled after success. |
| U22 | Worksheet undo and redo preserve edits and stored records | PASS | Keyboard undo/redo covers formulas, pasted dates, cell cuts, deletion, redo invalidation, unrelated drafts and reload scope. |
| U23 | Cut and delete preserve reusable blank row positions | PASS | Delete/cut, undo/redo, reopening and re-entry preserve lower record positions; empty rows are excluded from reports. |
| U24 | Square theme toggle updates existing worksheet without data changes | PASS | Two clicks switch dark/light in place; button follows Save; inputs/revision/undo history unchanged. |
| U25 | Theme preference survives reload and invalid settings are harmless | PASS | Preference stored only in isolated application-local test folder; dark/light reload and invalid-file fallback verified. |
| U26 | Light and dark theme text contrast | PASS | Body, muted and accent text meet 4.5:1 against surfaces; white primary-button text also meets 4.5:1. |
| C06 | Register difference is calculated and protected | PASS | Difference uses original minus Pakistan customs independently of the fixed F6 bank-contract source, overrides stale input, updates after edit/reload and locks F8. Both legacy ledger due columns follow BL + terms. |
| D15 | Database reopen persistence | PASS | 12 detail rows plus metadata survive database disposal/reopen. |
