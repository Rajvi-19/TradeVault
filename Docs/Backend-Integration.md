# Developer A integration contract — first milestone

## Scope

Implemented contracts are in `Src/TradeVault.Application/Contracts.cs`; models are in `Src/TradeVault.Domain/Models.cs`.
This milestone is a local storage backend, not a deployable/authenticated application.
Do not expose it as production-ready: login, roles, database encryption, full reports and owner report import are not implemented yet.

## Composition

The Desktop project references Application and Infrastructure. Keep a single database owner alive for the application lifetime:

```csharp
var paths = PortablePaths.FromApplicationDirectory();
var database = new PortableDatabase(paths);
ITradeVaultService service = new TradeVaultService(database);
// Inject service into view models. Dispose database on application shutdown.
```

In production the application directory must be on the intended USB. Explicit roots are allowed for development/tests; removable-drive enforcement is not implemented.
The data-folder lock rejects a second cooperating application instance. Do not bypass it by opening SQLite directly.
Pooling is disabled so database file handles close after each operation. SQLite journals stay next to the database and SQL temporary storage is in memory.

These APIs are synchronous. Queue database work away from the WPF dispatcher, serialize UI saves, disable duplicate submissions while saving, and marshal results back to the UI thread.
Show `BusinessRuleException.Message` as input feedback. Translate IO, lock and database failures into friendly save errors; do not claim success before the call returns.
There is no automatic retry/idempotency token for receipt creation yet: after an uncertain save, reload receipts before retrying.

## Implemented operations

- `CreateParty`, `GetParties`.
- `CreateInvoice`, `UpdateInvoice`, `GetInvoices`.
- `RecordReceipt`, `GetReceipts`.
- `GetBankContractBalances(partyId, asOf)`.
- `PortableDatabase.CreateBackup()` creates and integrity-checks a consistent snapshot under the USB's `Backups` folder.

Invoice updates require the loaded `Version` in `ExpectedVersion`. A stale update fails instead of overwriting newer input.
Invoice numbers are case-insensitively unique within a party; include the financial year in the entered identifier.

## Field dictionary

| Field | Type | Rule |
| --- | --- | --- |
| Party name | text | Required, trimmed, maximum 200 characters |
| Country code | text | Two ASCII letters; uppercase on save; full ISO membership validation deferred |
| Default terms | integer | 90 by default; 0–3650 calendar days |
| Invoice number | text | Required, trimmed, maximum 100 characters |
| Invoice date | DateOnly | Required |
| Original amount | Usd | Nonnegative; stored, not used in difference calculations yet |
| Bank contract amount | Usd | Nonnegative; allocation ceiling |
| Final-leg BL date | nullable DateOnly | Missing date means unknown due date |
| Invoice terms | nullable integer on create | Missing uses party default; saved explicitly per invoice |
| Shipping charge | nullable Usd on create | Missing defaults to USD 3,800; explicit zero preserved |
| Due date | calculated nullable DateOnly | Final-leg BL date plus terms |
| Receipt date | DateOnly | Allocated invoices cannot be later than receipt |
| Receipt amount | Usd | Positive; two decimal places maximum |
| Receipt reference | text | Optional, maximum 200 characters; not a unique transaction key |
| Allocation | invoice UUID + Usd | Positive, same party, one row per invoice per receipt |
| Unallocated amount | calculated Usd | Receipt minus allocations; preserved as visible unused funds |
| Outstanding contract | calculated Usd | Contract minus receipts dated on/before as-of date |

`Usd.FromDecimal` rejects sub-cent precision instead of silently rounding. Its constructor accepts integer cents. No currency selector is exposed.
Display with an explicit USD label and two decimal places; never persist formatted currency strings.

## Calculation examples (synthetic)

- BL 2026-06-17 + 90 days = 2026-09-15; changing terms to 60 yields 2026-08-16.
- Invoice contract USD 10,000; receipts USD 3,000 and USD 6,000 produce USD 1,000 outstanding.
- Receipt USD 8,000 allocated USD 6,000 + USD 1,500 leaves USD 500 unallocated.
- Reports before a receipt date exclude that receipt. Reports before an invoice date exclude that invoice.
- A partial unpaid balance remains overdue after the due date; a fully settled invoice has zero current overdue days.

## Deliberate boundaries

- No difference-component allocation, shipping deduction, general ledger, opening balance or rounding adjustment yet. Shipping is an editable stored default only.
- One final-leg BL date per invoice in this milestone. No full shipment/container model or customs fields yet.
- No receipt editing, reallocation of advances or reversal yet. Earlier receipts can be stored unallocated; a later allocation workflow needs an effective date and will follow after policy confirmation.
- Historical queries filter transaction dates but do not reconstruct invoice versions before later edits. Final exports must freeze snapshots.
- Audit events currently record action/entity/time only, not actor or before/after state. Full audit requires authentication and posting workflows.
- The SQLite database is not encrypted. Use synthetic data until security and recovery are implemented.
- No `.tvreport` parser/exporter or Excel output yet; do not implement competing formats in Desktop.
- Backup restore is tested by reopening a copied snapshot. No end-user restore screen or pre-upgrade backup flow yet.

## Build

Requires a .NET 10 SDK. From the project root run `powershell -NoProfile -ExecutionPolicy Bypass -File ./build-backend.ps1` (bypass is limited to that child process).
Initial dependency restore needs network access on the development computer. Deployed offline software will carry runtime dependencies.
For an already restored workspace, pass `-NoRestore`. Development build caches live under ignored `.build/`.
