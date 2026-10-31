using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;
public sealed class AgentReceiptFifoTests
{
    [Fact] public void AgentFifoCrossesPartiesSkipsOtherAgentsAndPersistsReportAmounts()
    {
        var paths=new PortablePaths(Path.Combine(AppContext.BaseDirectory,"AgentFifoTests",Guid.NewGuid().ToString("N")));
        string agentId;Guid partyId;string first,second;
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var agent=store.CreateAgent("Agent");agentId=agent.Id;var other=store.CreateAgent("Other agent");var p=store.CreateParty("Buyer one");partyId=p.Id;var q=store.CreateParty("Buyer two");
            string Invoice(Guid party,string agent,string name,DateOnly date,decimal original,decimal bank)
            {
                var snap=store.Load("form",party);var id=store.CreateRegisterInvoice(party,snap.Revision,new("Pakistan",agent==agentId?"Agent":"Other agent",name,date,agent));snap=store.Load("form",party);var row=snap.Rows.Single(r=>r.GetValueOrDefault("_id")==id);row["F5"]=original.ToString(System.Globalization.CultureInfo.InvariantCulture);row["F6"]=bank.ToString(System.Globalization.CultureInfo.InvariantCulture);store.Save("form",snap.Revision,snap.Rows,party);return ReportWorkflow.InvoiceKey(party,row);
            }
            second=Invoice(p.Id,agentId,"Second",new(2026,2,1),200,120);
            first=Invoice(q.Id,agentId,"First",new(2026,1,1),100,60);
            Invoice(p.Id,other.Id,"Other",new(2025,1,1),999,500);
            Invoice(p.Id,agentId,"Future",new(2027,1,1),999,500);
            var receipt=store.AddTransferReceipt(TransferReceiptKind.Agent,agentId,new(2026,3,1),150,"First remittance");
            Assert.Equal(new[]{first,second},receipt.Allocations!.Select(a=>a.InvoiceId));Assert.Equal(60,receipt.Allocations![0].Bank);Assert.Equal(40,receipt.Allocations![0].Difference);Assert.Equal(50,receipt.Allocations![1].Bank);
            receipt=store.AddTransferReceipt(TransferReceiptKind.Agent,agentId,new(2026,3,2),200,"Second remittance");Assert.Equal(150,receipt.AllocatedUsd);Assert.Equal(50,receipt.UnallocatedUsd);
            var report=new CompanyReports(store).Build(CompanyReportKind.AccountDetails,p.Id.ToString());Assert.Equal("200.00",report.Table.Rows.Single(r=>r["B"].Contains("Second"))["O"]);Assert.All(report.Table.Rows,r=>Assert.Equal("0.00",r["E"]));
            var snap=store.Load("form",p.Id);snap.Rows.Single(r=>r.GetValueOrDefault("F3")=="Second")["F5"]="201";Assert.Throws<TradeVault.Domain.BusinessRuleException>(()=>store.Save("form",snap.Revision,snap.Rows,p.Id));
        }
        using(var db=new PortableDatabase(paths)){var store=new WorkbookStore(db);Assert.Equal(300,store.GetTransferReceipts(TransferReceiptKind.Agent).Sum(r=>r.AllocatedUsd));Assert.Equal("200.00",new CompanyReports(store).Build(CompanyReportKind.AccountDetails,partyId.ToString()).Table.Rows.Single(r=>r["B"].Contains("Second"))["O"]);}
    }
}

