# Saved invoices and reporting-date visibility — 2026-10-28

Build UI 2026.10.28.9. Main Debug app rebuilt in place; no ZIP and no working invoice/database changes.

## Diagnosis

Read-only inspection of the main database found six saved invoices for the Crescent TEST party. Its register report cutoff was 2026-10-27; two invoice dates were 2026-11-20. Both generated reports intentionally filtered out those later invoices. There were no missing invoice dates, original amounts or configured contract amounts in that party. No dates or financial inputs were changed to force inclusion.

## Fix

- Report/register views show a prominent warning when saved invoices are later than As of. Generated views show included versus saved invoice counts.
- Show all saved invoices explicitly advances the selected customer's report date to the latest saved invoice date, persists that metadata, and rebuilds both reports. It does not rewrite invoice dates/amounts or affect other parties.
- Save explains later-dated exclusions and asks whether to advance the report date. Yes includes all saved invoices; No preserves the historical cutoff and leaves the warning/action available.
- Returning from either report to the register retains the selected cutoff. Existing valid-edit auto-save, party isolation, payment history protections, calculation settings and immutable imported reports remain unchanged.

## Verification

Desktop main and isolated builds: zero warnings/errors. Live self-test passed, including invoice save/edit reaching both tabs, explicit rejection of date advance with a visible exclusion warning, Show all including the future invoice in both tabs, persisted cutoff/return to register, and acceptance of a later save-time cutoff advance. Targeted backend tests verify coverage, historical exclusion, selected source calculations and metadata-only updates preserving source invoice inputs. Desktop acceptance evidence is retained alongside this report.

Operational note: invoices dated 20 November should be absent from a 27 October historical report. To view them now, choose Show all saved invoices or set As of to 20 November or later. The app does not silently falsify dates or include future invoices in an earlier report. Physical UI interaction is not claimed; automated WPF checks and source/data inspection were used.
