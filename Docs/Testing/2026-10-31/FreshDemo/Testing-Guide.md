# Fresh demo testing guide

Reference date: 31 October 2026. Names, identifiers and exchange-value text are fictional. Master/record names are marked [DEMO].

| Buyer | Original invoices USD | Party receipts USD | Net recorded balance USD | Main cases |
|---|---:|---:|---:|---|
| Faisalabad Textile Processors | 57,000 | 22,000 | 35,000 | One settled, one partially paid overdue, one older invoice through another agent |
| Multan Fabric Industries | 41,000 | 5,000 | 36,000 | Due soon, editable custom due date, BL pending |
| Chattogram Colour Mills | 33,000 | 35,000 | -2,000 | All invoices paid, 2,000 unallocated advance |

| Agent | Invoices | Incoming party money USD | Remitted to company USD |
|---|---:|---:|---:|
| Atlas Colour Malaysia | 2 | 21,000 | 16,000 |
| Peninsula Trade Services | 4 | 6,000 | 4,000 |
| Selangor Chemical Trading | 3 | 35,000 | 30,000 |

1. Invoice Register: select a buyer. Complete input/date fields and use an unpaid record to test edits. Payment Due Date is editable; its default is BL + 90 calendar days. DEMO-EXP-2026-005 demonstrates a saved override. Clear its due date to return to BL + 90. Changes to a BL retain a custom override until cleared. Paid invoice amount fields are protected to preserve receipts; use an unpaid invoice for amount edits.
2. Reports: choose the buyer and compare Party Ledger to the first table. Account Details bank and difference allocations must reflect the receiving agent. Atlas's USD 21,000 clears its first USD 18,000 invoice then USD 3,000 of the next invoice; it does not pay Faisalabad's older Peninsula invoice. No Terms / Days columns or Refresh button are present.
3. Party Receipt: four new linked records appear, with informational USD value text and no Allocated/Unallocated columns. Add another receipt to exercise partial payment FIFO. Receipt amount remains USD and is never converted from the text field.
4. Agent Receipt: three onward remittances appear. They must not reduce buyer debt a second time.
5. Reminders: at the reference date there are 4 settled, 2 overdue, 1 due within 30 days, 1 not yet due and 1 BL-pending invoice. Overdue statuses change with the actual date. The advance is a party-level credit, not a negative invoice balance.
6. Shipping, bank charges and opening balances remain unavailable by prior instruction. Agent net deposit/pending and post-shipping account results intentionally remain blank; gross incoming/remitted figures reconcile as shown.

The empty unmarked Dhyan party is preserved. Recovery backup before reset: Backups/tradevault-20261031-121044-305c36524db54a928bc1506a6ca9ea5f.db beside the main executable. The reset affects active marked data, not historical audit/backup evidence or exported files. No automatic reseeding runs on normal startup.
