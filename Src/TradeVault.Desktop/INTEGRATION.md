> Historical prototype proposal. Current application uses WorkbookStore/ReportWorkflow without authentication or roles. See Docs/Workflow-Guide.md for current behavior.

# Desktop integration proposal — awaiting Developer B agreement

This is a Desktop-local prototype contract, not a change to shared Application types.
No solution, Domain, Application, Infrastructure or backend-test files were changed.

`IPrototypeService` is injected into `MainWindow`. `MockPrototypeService` is the sole
synthetic data provider. `GridRow` and `Field` are presentation types; do not persist
them or make backend projects reference Desktop. Replace the mock with a Desktop
adapter mapping agreed Application DTOs into these presentation models.

Before integration, agree typed request/response DTOs for invoice/shipment loading,
draft defaults, validation, save, receipt allocation, report snapshots, submission
preview/import, review status and notes. Agree cancellation/asynchronous behavior,
stable IDs, row versions, field-level error codes and date/decimal/null mapping.
The UI currently uses invariant USD input strings and ISO date strings; the adapter
must convert without embedding accounting or persistence rules in views.

Validation responses must identify row ID, field key and message. Invoice validation
and saves must return calculated fields; the UI never calculates due dates or balances.
Receipt responses must supply receipt, allocated and unallocated totals together.
Report sorting must not recalculate running balances. Imported snapshots remain
read-only and must not load into the owner's operational invoice editor.

Current mock save is memory only. Calculated fixture values are deliberately static
and marked as fixtures; editing inputs never represents a real recalculation. Authentication
is replaced by an explicit demo role selector, not password verification.

No backend developer session was available to negotiate these contracts during this
work session. Do not treat this proposal as agreed; review jointly before integration.
