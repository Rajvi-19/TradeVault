using TradeVault.Infrastructure;
using Xunit;
namespace TradeVault.Tests;
public sealed class MasterRenameTests
{
    [Fact]public void RenameUpdatesInvoicesReceiptsReportsAndReopensWithoutChangingAllocations()
    {
        var paths=new PortablePaths(Path.Combine(AppContext.BaseDirectory,"RenameTests",Guid.NewGuid().ToString("N")));Guid partyId;string agentId;
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var party=store.CreateParty("Old buyer");partyId=party.Id;var agent=store.CreateAgent("Old agent");agentId=agent.Id;
            store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"PI-1",new(2026,1,1),agent.Id));var sheet=store.Load("form",party.Id);sheet.Rows[0]["F5"]="1000";sheet.Rows[0]["F6"]="600";store.Save("form",sheet.Revision,sheet.Rows,party.Id);
            var receipt=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,2,1),500,"Payment",agent.Id);store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,2,2),300,"Remittance");
            store.RenameParty(party.Id,"  New   buyer  ");store.RenameAgent(agent.Id,"New agent");
            Assert.Equal("New buyer",store.GetParties().Single(p=>p.Id==party.Id).Name);Assert.Equal("New agent",Assert.Single(store.GetAgents()).Name);var row=store.Load("form",party.Id).Rows[0];Assert.Equal("New buyer",row["F1"]);Assert.Equal("New agent",row["F27"]);Assert.Equal(agent.Id,row["_agentId"]);
            var saved=Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Party));Assert.Equal("New buyer",saved.Name);Assert.Equal(receipt.AllocatedUsd,saved.AllocatedUsd);Assert.Equal("New agent",Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Agent)).Name);
            Assert.Equal("New buyer",new CompanyReports(store).Build(CompanyReportKind.PartyLedger,party.Id.ToString()).Subject);Assert.Equal("New agent",new CompanyReports(store).Build(CompanyReportKind.AgentLedger,agent.Id).Subject);
            store.CreateParty("Other buyer");store.CreateAgent("Other agent");Assert.Throws<TradeVault.Domain.BusinessRuleException>(()=>store.RenameParty(party.Id,"other buyer"));Assert.Throws<TradeVault.Domain.BusinessRuleException>(()=>store.RenameAgent(agent.Id,"other agent"));Assert.Throws<TradeVault.Domain.BusinessRuleException>(()=>store.RenameParty(party.Id," "));Assert.Throws<TradeVault.Domain.BusinessRuleException>(()=>store.RenameParty(Guid.Empty,"Unassigned"));
            Assert.Equal("New buyer",store.GetParties().Single(p=>p.Id==party.Id).Name);
        }
        using(var db=new PortableDatabase(paths)){var store=new WorkbookStore(db);Assert.Equal("New buyer",store.GetParties().Single(p=>p.Id==partyId).Name);Assert.Equal("New agent",store.GetAgents().Single(a=>a.Id==agentId).Name);}
    }
}
