using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class CompanyReportTests:IDisposable
{
    private readonly PortableDatabase database=new(new(Path.Combine(AppContext.BaseDirectory,"ReportTests",Guid.NewGuid().ToString("N"))));
    public void Dispose()=>database.Dispose();
    private (WorkbookStore Store,Guid Party,string Agent) Fixture()
    {
        var store=new WorkbookStore(database);var party=store.CreateParty("Synthetic report buyer");var agent=store.CreateAgent("Synthetic intermediary");
        var id=store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"PI-01",new(2026,5,25),agent.Id));
        var snapshot=store.Load("form",party.Id);var row=snapshot.Rows.Single(r=>r.GetValueOrDefault("_id")==id);row["F9"]="INV-01";row["F10"]="2026-06-01";row["F5"]="18000";row["F6"]="12000";row["F7"]="11000";row["F17"]="2026-06-20";store.Save("form",snapshot.Revision,snapshot.Rows,party.Id);
        return(store,party.Id,agent.Id);
    }
    [Fact]public void ReferenceColumnsAndKnownAccountCalculationsDoNotInventAllocations()
    {
        var (store,party,agent)=Fixture();store.AddTransferReceipt(TransferReceiptKind.Party,party.ToString(),new(2026,7,1),5000,"Transfer to agent",agent);
        var snapshot=store.Load("form",party);var report=new CompanyReports(store).Build(CompanyReportKind.AccountDetails,party.ToString());var row=Assert.Single(report.Table.Rows);
        Assert.Equal(13,report.Table.Columns.Count);Assert.Equal("18000.00",row["C"]);Assert.Equal("12000.00",row["D"]);Assert.DoesNotContain(report.Table.Columns,c=>c.Key=="G");Assert.True(report.Table.Columns.FindIndex(c=>c.Key=="E")<report.Table.Columns.FindIndex(c=>c.Key=="O"));Assert.Equal("2026-09-18",row["N"]);Assert.Equal("90",row["M"]);
        Assert.Equal("5000.00",row["E"]);Assert.Equal("5000.00",row["F"]);Assert.Equal("0.00",row["J"]);foreach(var key in new[]{"H","I","K"})Assert.Equal("",row[key]);Assert.Contains("0.00",report.Summary["Unallocated receipts / advance"]);Assert.Equal(snapshot.Revision,store.Load("form",party).Revision);
    }
    [Fact]public void PartyLedgerDebitsInvoicesAndCreditsOnlyThatPartysPaymentBook()
    {
        var (store,party,agent)=Fixture();var other=store.CreateParty("Other buyer");store.AddTransferReceipt(TransferReceiptKind.Party,other.Id.ToString(),new(2026,6,2),999,"Other party",agent);
        store.AddTransferReceipt(TransferReceiptKind.Agent,agent,new(2026,7,3),4000,"Onward transfer");store.AddTransferReceipt(TransferReceiptKind.Party,party.ToString(),new(2026,7,1),5000,"First receipt",agent);store.AddTransferReceipt(TransferReceiptKind.Party,party.ToString(),new(2026,7,2),1000,"Second receipt",agent);
        var report=new CompanyReports(store).Build(CompanyReportKind.PartyLedger,party.ToString());Assert.Equal(8,report.Table.Columns.Count);Assert.Equal(3,report.Table.Rows.Count);Assert.Equal(new[]{"18000.00","13000.00","12000.00"},report.Table.Rows.Select(r=>r["E"]));Assert.Equal("USD 12000.00",report.Summary["Net recorded balance"]);
    }
    [Fact]public void AgentLedgerDoesNotGuessReceivingAgentOrZeroMissingCharges()
    {
        var (store,party,agent)=Fixture();var other=store.CreateAgent("Other agent");store.AddTransferReceipt(TransferReceiptKind.Party,party.ToString(),new(2026,7,1),5000,"Receiving agent",agent);
        store.AddTransferReceipt(TransferReceiptKind.Agent,agent,new(2026,7,3),4000,"Onward transfer");store.AddTransferReceipt(TransferReceiptKind.Agent,other.Id,new(2026,7,4),999,"Other agent");
        var report=new CompanyReports(store).Build(CompanyReportKind.AgentLedger,agent);Assert.Equal(7,report.Table.Columns.Count);Assert.Equal(2,report.Table.Rows.Count);Assert.Equal("5000.00",report.Table.Rows[0]["D"]);Assert.Equal("4000.00",report.Table.Rows[1]["G"]);foreach(var row in report.Table.Rows)foreach(var key in new[]{"E","F"})Assert.Equal("",row[key]);Assert.Equal("USD 1000.00",report.Summary["Pending with agent"]);Assert.Equal(new[]{"5000.00","1000.00"},report.Table.Rows.Select(r=>r["H"]));
        store.AddTransferReceipt(TransferReceiptKind.Agent,agent,new(2026,7,5),2000,"Further remittance");var updated=new CompanyReports(store).Build(CompanyReportKind.AgentLedger,agent);Assert.Equal("USD -1000.00",updated.Summary["Pending with agent"]);Assert.Equal("-1000.00",updated.Table.Rows.Last()["H"]);
        Assert.Throws<BusinessRuleException>(()=>new CompanyReports(store).Build(CompanyReportKind.AgentLedger,"unknown"));
    }
    [Fact]public void MasterAmountsFollowEachLedgerAndRetainAdvancesAndUnknownBalances()
    {
        var (store,party,agent)=Fixture();var reports=new CompanyReports(store);
        Assert.Equal(new MasterLedgerAmounts(0,18000,18000),reports.MasterAmounts(false,party.ToString()));Assert.Equal(new MasterLedgerAmounts(0,0,0),reports.MasterAmounts(true,agent));
        store.AddTransferReceipt(TransferReceiptKind.Party,party.ToString(),new(2026,7,1),20000,"Party paid with advance",agent);
        store.AddTransferReceipt(TransferReceiptKind.Agent,agent,new(2026,7,2),15000,"Company received");
        Assert.Equal(new MasterLedgerAmounts(20000,-2000,18000),reports.MasterAmounts(false,party.ToString()));Assert.Equal(new MasterLedgerAmounts(15000,5000,20000),reports.MasterAmounts(true,agent));
        store.CreateRegisterInvoice(party,store.Load("form",party).Revision,new("Pakistan","Synthetic intermediary","DRAFT",new(2026,7,3),agent));
        Assert.Null(reports.MasterAmounts(false,party.ToString()).Pending);Assert.Null(reports.MasterAmounts(false,party.ToString()).OriginalOrPartyReceipts);Assert.Equal(20000,reports.MasterAmounts(false,party.ToString()).Received);
        var empty=store.CreateParty("Empty buyer");Assert.Equal(new MasterLedgerAmounts(0,0,0),reports.MasterAmounts(false,empty.Id.ToString()));
    }
    [Fact]public void MissingInvoiceAmountMakesRunningBalanceUnavailable()
    {
        var (store,party,agent)=Fixture();store.CreateRegisterInvoice(party,store.Load("form",party).Revision,new("Pakistan","Synthetic intermediary","PI-DRAFT",new(2026,7,1),agent));
        var report=new CompanyReports(store).Build(CompanyReportKind.PartyLedger,party.ToString());Assert.Equal("",report.Table.Rows.Last()["E"]);Assert.Equal("Unavailable",report.Summary["Net recorded balance"]);
    }
}





