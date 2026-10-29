# Cell versus record actions and navigation styling — 2026-10-29

Build UI 2026.10.29.3. Main app updated directly; no ZIP or company data changes.

Delete/toolbar Delete now clears editable selected cells without record confirmation. A whole record is identified by selection of all visible columns in one real row (row-number click); it retains confirmation and immediate storage removal. While typing in a text box, Delete and Ctrl+X use native text behavior. Calculated cells/read-only reports remain protected.

Ctrl+X cuts cells/ranges or an entire selected record. Cell ranges must be rectangular and contain editable inputs only. Clipboard data is written before clearing, so failures are non-destructive. Whole-record cut prompts, copies all visible values, then uses the existing protected immediate deletion flow. Cancel changes neither clipboard nor data. TSV supports external applications; private bounded JSON clipboard metadata preserves invoice terms on matching in-app paste while destination party/record identity remains local. No clipboard support files are written to disk.

Changed the loose Create party link to an outlined New party action with a person-add line icon. Added consistent vector line icons to all main tabs, selected-state colors, modern Segoe UI Variable/Segoe UI fallback typography for tabs/headings/toolbar/grid. Field names and financial calculations unchanged.

Verification: normal/isolated builds zero warnings/errors; 47 acceptance checks passed; isolated and main live self-tests passed. Tests cover cell Delete with zero confirmations, cell cut, clipboard failure protection, locked values, cancellation of record cut, confirmed record cut persistence, deletion, saving and navigation. Test clipboard writes are injected, not sent to the user's clipboard. Fixed a WPF cell-identity comparison in range/full-row classification and made isolated automation shutdown skip unsaved-close prompts (normal app prompts retained). A blocked old synthetic test process was stopped; company app had been closed by the user. Rendered compact layout inspected. Physical clipboard/keyboard/mouse integration remains manual acceptance.
