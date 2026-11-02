using TradeVault.Infrastructure;
using TradeVault.Domain;
using Xunit;
namespace TradeVault.Tests;
public sealed class ExpenseReceiptTests
{
    [Fact]public void ExpensePersistsAndAddsToBankChargeWithoutDoubleChargingParty()
    {
        var paths=new PortablePaths(Path.Combine(AppContext.BaseDirectory,"ExpenseTests",Guid.NewGuid().ToString("N")));Guid partyId;string invoiceId,agentId;
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");partyId=party.Id;var agent=store.CreateAgent("Agent");agentId=agent.Id;
            store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"PI-1",new(2026,1,1),agent.Id));var snap=store.Load("form",party.Id);snap.Rows[0]["F5"]="1000";snap.Rows[0]["F6"]="600";store.Save("form",snap.Revision,snap.Rows,party.Id);invoiceId=Assert.Single(store.GetExpenseInvoices(party.Id)).Id;
            store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,2,1),1000,"Payment",agent.Id,bankTransferCharge:10);store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,2,2),500,"Remittance");store.AddExpenseReceipt(party.Id,invoiceId,new(2026,2,3),100,"Handling");store.AddExpenseReceipt(party.Id,invoiceId,new(2026,2,4),25,"Transport");
            var reports=new CompanyReports(store);var row=Assert.Single(reports.Build(CompanyReportKind.AccountDetails,party.Id.ToString()).Table.Rows);Assert.Equal("135.00",row["H"]);Assert.Equal("365.00",row["F"]);
            var ledger=reports.Build(CompanyReportKind.AgentLedger,agent.Id);Assert.Equal(-125m,ledger.Table.Rows.Where(r=>r["C"].StartsWith("Expense")).Sum(r=>decimal.Parse(r["D"],System.Globalization.CultureInfo.InvariantCulture)));Assert.Equal("USD 365.00",ledger.Summary["Pending with agent"]);Assert.Equal(865m,reports.MasterAmounts(true,agent.Id).OriginalOrPartyReceipts);Assert.Equal(0m,reports.MasterAmounts(false,party.Id.ToString()).Pending);
            var other=store.CreateParty("Other");Assert.Empty(store.GetExpenseInvoices(other.Id));Assert.Throws<BusinessRuleException>(()=>store.AddExpenseReceipt(other.Id,invoiceId,new(2026,2,5),5,"Wrong party"));Assert.Throws<BusinessRuleException>(()=>store.AddExpenseReceipt(party.Id,invoiceId,new(2025,1,1),5,"Early"));foreach(var amount in new[]{0m,-1m,0.001m})Assert.Throws<BusinessRuleException>(()=>store.AddExpenseReceipt(party.Id,invoiceId,new(2026,2,5),amount,"Invalid"));Assert.Equal(2,store.GetExpenseReceipts().Count);
        }
        using(var db=new PortableDatabase(paths)){var store=new WorkbookStore(db);Assert.Equal(125,store.GetExpenseReceipts().Sum(r=>r.Amount));Assert.Equal("135.00",Assert.Single(new CompanyReports(store).Build(CompanyReportKind.AccountDetails,partyId.ToString()).Table.Rows)["H"]);Assert.All(store.GetExpenseReceipts(),r=>Assert.Equal(agentId,r.AgentId));}
    }
    [Fact]public void ExpenseOnlyInvoiceCannotBeRemovedOrRelinked()
    {
        using var db=new PortableDatabase(new(Path.Combine(AppContext.BaseDirectory,"ExpenseTests",Guid.NewGuid().ToString("N"))));var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"PI-1",new(2026,1,1),agent.Id));var invoice=Assert.Single(store.GetExpenseInvoices(party.Id));store.AddExpenseReceipt(party.Id,invoice.Id,new(2026,2,1),10,"Expense");var snap=store.Load("form",party.Id);Assert.Throws<BusinessRuleException>(()=>store.Save("form",snap.Revision,[],party.Id));
        var other=store.CreateAgent("Other");snap.Rows[0]["_agentId"]=other.Id;snap.Rows[0]["F27"]=other.Name;Assert.Throws<BusinessRuleException>(()=>store.Save("form",snap.Revision,snap.Rows,party.Id));
    }
}
