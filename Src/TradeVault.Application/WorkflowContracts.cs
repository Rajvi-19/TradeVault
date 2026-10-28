namespace TradeVault.Application;

public sealed record ReportColumn(string Key,string Label,string Type="text");
public sealed record ReportTable(string Name,List<ReportColumn> Columns,List<Dictionary<string,string>> Rows,Dictionary<string,decimal> Totals);
public sealed record ReportBundle(int FormatVersion,string CalculationVersion,Guid SourceId,Guid PartyId,string PartyName,DateOnly AsOf,DateTimeOffset CreatedAt,Guid SubmissionId,int Revision,string Mode,List<ReportTable> Tables,List<string> Notes);
public sealed record ReportMapping(string ContractField,string BankReceivedField,bool DeductRegisteredShipping,DateOnly OpeningDate,decimal OpeningBank=0,decimal OpeningDifference=0);
public sealed record ReceiptSplit(string InvoiceId,string Component,decimal Amount);
public sealed record PartyReceipt(Guid Id,DateOnly Date,decimal Amount,string Reference,List<ReceiptSplit> Allocations,Guid? Reverses=null);
public sealed record ReceiptBook(List<PartyReceipt> Receipts);
public sealed record InvoicePaymentBalance(string Id,string Invoice,DateOnly? Due,decimal BankUnpaid,decimal DifferenceUnpaid)
{public decimal TotalUnpaid=>BankUnpaid+DifferenceUnpaid;}
public sealed record ImportedSubmission(ReportBundle Report,string PackageHash,string PackageBase64,string Status,string ReviewNote,DateTimeOffset ImportedAt);
public sealed record PackagePreview(ReportBundle Report,string Hash);
public sealed record ReminderItem(string Invoice,DateOnly? Due,decimal Outstanding,int OverdueDays,string Status);
public sealed record ReportCoverage(int Saved,int Included,int AfterDate,int MissingDates,DateOnly? LatestDate);
