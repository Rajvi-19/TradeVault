# Business validation and report output

## When checks run
Row-level business rules run on Save and at the database save boundary. Required fields are not announced after every cell edit. Invalid saves do not change the stored worksheet or revision. Existing data is not rewritten automatically; correct reported errors before saving a worksheet with old inconsistent records.

## Register rules
- All entered dates must be complete valid dates. Unknown shipment dates may remain blank.
- Invoice date must be on or after proforma date when both are supplied.
- India BL must be on or after invoice date.
- Malaysia arrival must be on or after India BL.
- Malaysia BL must be on or after Malaysia arrival, India BL and invoice date when those dates are supplied.
- Payment dates F24/F26 must be on or after invoice date. Same-day payment is valid. Pre-invoice money belongs in Receipts as an unallocated advance, not as an invoice payment.
- Positive receipt amounts F22/F23 require invoice date and F24. Positive F25 requires invoice date and F26.
- Entered USD amounts must be non-negative, at most USD 1 trillion and have at most two decimal places.
- Terms must be 0–3650 calendar days; BL plus terms must fit the supported date range.
- Existing required party/invoice, party matching, duplicate invoice reference, receipt-history protection and concurrent-save checks remain.

No restriction was added requiring an invoice to be paid before its due date: late payment is valid. Future shipment dates are permitted. Independent customs amounts have not been equated or constrained against each other.

## Receipt book rules retained
Receipts require a positive USD total, positive unique invoice/component allocations, allocations no larger than the receipt total, and invoices belonging to the selected party. Allocations cannot predate their invoices. Unallocated advances may predate an invoice. Generated report checks prevent over-allocation of invoice components. Corrections and reversals preserve history, cannot predate the original receipt and cannot reverse the same receipt twice.

## Date controls
Removed the automatic date-entry instruction tooltip. Worksheet dates keep typed day-first entry. As of additionally has a calendar button; selecting a date refreshes calculations.

## Printing and PDF
Save PDF writes the paginated landscape report directly without Windows printing. It uses high-resolution rendered pages, including Unicode party names, repeated headings, totals and page numbers. The PDF text is an image and is not selectable/searchable.

Print now sends a real FixedDocument with a valid source to Windows. Landscape is requested before opening the print dialog. If Windows printing fails, the application offers direct PDF saving instead of exposing RuntimeWrappedException. Physical printer/driver interaction remains an environment-specific manual acceptance check; no claim is made that every installed printer driver has been tested.
