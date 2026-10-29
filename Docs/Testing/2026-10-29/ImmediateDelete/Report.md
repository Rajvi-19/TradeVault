# Compact confirmation, immediate deletion and header refinement — 2026-10-29

Build UI 2026.10.29.2. Updated the main application in place; no ZIP or company record changes.

The delete dialog sizes to its content instead of reserving a fixed 390-pixel height. Reduced margins, compact record card and adjacent actions eliminate the empty area. Keep record/Escape/X still cancel by default. Its text now explains immediate persistence rather than an extra save step.

Confirmed deletion uses a revision-checked storage operation to remove only the selected stored record. The UI removes it only after storage succeeds. Reload/reopening cannot resurrect it. Pending edits in other rows and settings remain unsaved; the controller compares saved inputs to preserve the correct dirty state. Unsaved draft-only rows are simply removed. Payment-history/concurrency protections stay enforced; errors leave the row in place. No whole-sheet autosave of unrelated draft inputs.

Removed Shortcuts from navigation; F1 and shortcuts remain available. Save is a stable compact icon/button, blue for pending edits and an outlined Saved/checkmark state otherwise. Main header now uses clean TradeVault branding and a role chip; removed Offline/USD text, login OFFLINE badge and offline marketing hint. Currency labels remain on financial fields. Caption bars no longer show debug build suffixes; build footer remains.

Verification: isolated/main builds zero warnings/errors; 46 acceptance checks passed; isolated and final main live self-tests passed. Tests cover cancel, immediate persisted deletion, reload, retaining other draft values without saving them, and dirty-state accuracy. Targeted backend test passed for revision checking, protected-invoice rollback and persistence after closing/reopening the isolated database. The initial test tried to reopen a still-leased fixture; corrected the fixture to close its lease before reopen, without changing production locking. Natural-height confirmation and compact main/login screenshots visually inspected. No physical UI/multi-monitor/printer checks claimed.
