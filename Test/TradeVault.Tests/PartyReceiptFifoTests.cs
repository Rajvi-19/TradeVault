using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class PartyReceiptFifoTests:IDisposable
{
    private readonly PortableDatabase db=new(new(Path.Combine(AppContext.BaseDirectory,"FifoTests",Guid.NewGuid().ToString("N"))));
    public void Dispose()=>db.Dispose();
    private static string Invoice(WorkbookStore store,Guid party,AgentMasterEntry agent,string number,DateOnly proforma,decimal original=1000,decimal bank=600)
    {
        var id=store.CreateRegisterInvoice(party,store.Load("form",party).Revision,new("Pakistan",agent.Name,number,proforma,agent.Id));var snapshot=store.Load("form",party);var row=snapshot.Rows.Single(r=>r.GetValueOrDefault("_id")==id);row["F5"]=original.ToString(System.Globalization.CultureInfo.InvariantCulture);row["F6"]=bank.ToString(System.Globalization.CultureInfo.InvariantCulture);row["F7"]="500";row["F9"]=number+"-IN";row["F10"]=proforma.AddDays(2).ToString("yyyy-MM-dd");store.Save("form",snapshot.Revision,snapshot.Rows,party);return id;
    }
    
    [Fact]public void UnknownAgentRejectedFutureInvoicesAndIncompleteOldestStayAdvance()
    {
        var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");Invoice(store,party.Id,agent,"Future",new(2027,1,1));
        Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,7,1),100,"Missing agent"));
        var advance=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,7,1),100,"Advance",agent.Id);Assert.Empty(advance.Allocations!);
        store.CreateRegisterInvoice(party.Id,store.Load("form",party.Id).Revision,new("Pakistan",agent.Name,"Incomplete",new(2026,5,1),agent.Id));Invoice(store,party.Id,agent,"Complete later",new(2026,6,1));
        var blocked=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,7,2),100,"Still advance",agent.Id);Assert.Empty(blocked.Allocations!);
    }
    [Fact]public void SettledFifoInvoiceUpdatesRemindersAndLegacyReportProjectionOnce()
    {
        var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");var id=Invoice(store,party.Id,agent,"Invoice",new(2026,6,1));var snapshot=store.Load("form",party.Id);var row=snapshot.Rows.Single(r=>r.GetValueOrDefault("_id")==id);row["F17"]="2026-06-20";store.Save("form",snapshot.Revision,snapshot.Rows,party.Id);
        store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,7,1),1000,"Settlement",agent.Id);store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,7,2),600,"Remittance");
        var workflow=new ReportWorkflow(store);var reminder=Assert.Single(workflow.Reminders(party.Id,new(2026,10,31)));Assert.Equal("Settled",reminder.Status);Assert.Equal(0,reminder.Outstanding);Assert.Equal(0,workflow.SettlementAwareOverdue(party.Id,row,new(2026,10,31)));Assert.Equal(0,workflow.BuildCurrent(party.Id).Tables[1].Totals["E"]);
    }
    [Fact]public void FutureHistoricalReceiptThenReversalDoesNotAllowInterimOverpayment()
    {
        var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");var id=Invoice(store,party.Id,agent,"Invoice",new(2026,6,1),600,600);var workflow=new ReportWorkflow(store);
        var old=workflow.RecordReceipt(party.Id,new(2026,7,1),600,"Historical bank receipt",[new(id,"Bank",600)]);workflow.ReverseReceipt(party.Id,old.Id,new(2026,8,1),"Reversed later");
        var early=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,6,15),600,"Early advance",agent.Id);Assert.Empty(early.Allocations!);workflow.BuildCurrent(party.Id);
    }
    [Fact]public void OldUnlinkedReceiptIsPreservedUntilExplicitAgentAssignment()
    {
        var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");var invoice=Invoice(store,party.Id,agent,"Invoice",new(2026,6,1));var id=Guid.NewGuid();
        using(var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=db.Paths.Database,Pooling=false}.ToString()))
        {connection.Open();using var command=connection.CreateCommand();command.CommandText="INSERT INTO workflow_documents VALUES('party_transfer_receipts','all',1,$data)";command.Parameters.AddWithValue("$data",JsonSerializer.Serialize(new {Entries=new[]{new {Id=id,MasterId=party.Id.ToString(),Name=party.Name,PaymentDate=new DateOnly(2026,7,1),UsdAmount=800m,Description="Old receipt",RecordedAt=DateTimeOffset.UtcNow}}}));command.ExecuteNonQuery();}
        var old=Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Party));Assert.Null(old.ReceivingAgentId);Assert.Null(old.Allocations);
        var linked=store.AssignReceivingAgent(id,agent.Id);Assert.Equal(id,linked.Id);Assert.Equal(800,linked.UsdAmount);Assert.Equal("Old receipt",linked.Description);var split=Assert.Single(linked.Allocations!);Assert.Equal(invoice,split.InvoiceId);Assert.Equal(600,split.Bank);Assert.Equal(200,split.Difference);
    }
}
