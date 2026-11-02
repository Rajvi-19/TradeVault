using System.Globalization;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;
public sealed record ExpenseReceipt(Guid Id,Guid PartyId,string InvoiceId,string AgentId,DateOnly Date,decimal Amount,string Description,DateTimeOffset RecordedAt);
public sealed record ExpenseReceiptBook(List<ExpenseReceipt> Entries);
public sealed record ExpenseInvoiceChoice(string Id,string Number,string AgentId,DateOnly Date);
public sealed partial class WorkbookStore
{
    public IReadOnlyList<ExpenseReceipt> GetExpenseReceipts()=>(Document<ExpenseReceiptBook>("expense_receipts","all")?.Entries??[]).OrderByDescending(r=>r.Date).ThenByDescending(r=>r.RecordedAt).ToList();
    public IReadOnlyList<ExpenseInvoiceChoice> GetExpenseInvoices(Guid party)
    {
        if(!GetParties().Any(p=>p.Id==party&&!p.IsUnassigned))throw new BusinessRuleException("Select a party from Party Master.");
        return Load("form",party).Rows.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1")
            .Where(r=>DateOnly.TryParseExact(r.GetValueOrDefault("F4"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            .Select(r=>new ExpenseInvoiceChoice(ReportWorkflow.InvoiceKey(party,r),r.GetValueOrDefault("F3",""),r.GetValueOrDefault("_agentId",""),DateOnly.ParseExact(r["F4"],"yyyy-MM-dd",CultureInfo.InvariantCulture))).OrderBy(r=>r.Date).ThenBy(r=>r.Number).ToList();
    }
    public ExpenseReceipt AddExpenseReceipt(Guid party,string invoiceId,DateOnly date,decimal amount,string? description)
    {
        var invoice=GetExpenseInvoices(party).SingleOrDefault(r=>r.Id==invoiceId)??throw new BusinessRuleException("Select an invoice belonging to this party.");
        if(!GetAgents().Any(a=>a.Id==invoice.AgentId))throw new BusinessRuleException("This invoice needs a valid agent before an expense can be recorded.");
        if(date<invoice.Date)throw new BusinessRuleException("Expense date cannot be before the proforma invoice date.");
        if(amount<=0||amount>1_000_000_000_000m||decimal.Round(amount,2)!=amount)throw new BusinessRuleException("Enter a positive expense in USD with at most two decimal places.");
        description=(description??"").Trim();if(description.Length>500)throw new BusinessRuleException("Description must be 500 characters or fewer.");
        var receipt=new ExpenseReceipt(Guid.NewGuid(),party,invoice.Id,invoice.AgentId,date,amount,description,DateTimeOffset.UtcNow);
        Mutate<ExpenseReceiptBook>("expense_receipts","all",book=>new([..book?.Entries??[],receipt]));return receipt;
    }
}
