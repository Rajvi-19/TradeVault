# Party workspaces — implemented phase

## Using it

1. Sign in and click **Create party** beside the worksheet title.
2. Enter a party name. Names are trimmed; repeated whitespace/case variants cannot create duplicates.
3. The app switches to that party's empty worksheet. Select between Invoice register, Account details and Debit & credit as before.
4. Enter records and save. Each party/worksheet has independent rows, revision, opening balance, payment terms and as-of date. New invoice input rows default PARTY NAME to the selected party; Save rejects a different party name in that workspace.
5. Switching party or creating another party prompts before discarding unsaved changes. Selecting a party does not merge, copy or synchronize any data.

Excel headers and existing formula behaviour remain unchanged. Display dates use DD-MM-YYYY while storage uses ISO. As-of persistence does not introduce historical filtering; it still controls overdue-day arithmetic.

## Existing records

Database schema migration 3 automatically creates a consistent, integrity-checked backup under the same application's `Backups/` folder before modifying a version-1/2 database. If backup creation fails, migration stops.

Migration copies existing worksheet data and revisions exactly into the system bucket **Unassigned records**. It does not invent party ownership. The original `worksheets` table is retained as legacy evidence; the app now reads/writes `party_worksheets`.

To assign an existing sheet:

- Create the destination party, then return to Unassigned records and the relevant worksheet.
- Save or reload pending edits; click **Assign worksheet** and select the destination.
- Confirm only if every row belongs to that party. The move includes metadata/opening balance, is transactional and cannot overwrite any saved destination worksheet.
- Invoice input rows with different/missing PARTY NAME values cannot be assigned wholesale. Keep mixed records unassigned until manually separated. Report sheets have no party-name column, so their ownership requires explicit user confirmation.
- Assignment moves one entire worksheet, not all three. Assign the other sheets separately only when their ownership is correct. No automatic splitting/merging is implemented.

## Schema and safeguards

- `worksheet_parties`: UUID, display name, normalized unique name key, system-bucket flag.
- `party_worksheets`: party UUID + worksheet name composite key, revision, JSON data.
- Authenticated party APIs validate existence; stale saves use party-scoped revisions.
- Assignment retains an incremented source revision with empty rows to block stale saves from resurrecting moved records.
- User actions write audit events. Party datasets are independent of the older normalized invoice/receipt service; receipt allocation UI remains a later phase.

## Verification

Automated tests cover two parties across all three sheets, independent metadata/revisions, reopening databases, duplicate/invalid names, authorization, unknown party IDs, safe assignment, overwrite rejection, stale source rejection, mixed invoice ownership, exact version-2 migration and readable pre-upgrade backup.

Desktop live checks also invoke party switching and real Save/Reload handlers, selected-party defaulting, and persisted terms/as-of values. Render checks cover normal and minimum window sizes. No real company entries are seeded by tests.
