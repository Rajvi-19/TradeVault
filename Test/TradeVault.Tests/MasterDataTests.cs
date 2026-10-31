using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class MasterDataTests:IDisposable
{
    private readonly PortableDatabase database=new(new(Path.Combine(AppContext.BaseDirectory,"MasterTests",Guid.NewGuid().ToString("N"))));
    public void Dispose()=>database.Dispose();
    
    [Fact]public void BlankDuplicateAndUnregisteredAgentNamesAreRejected()
    {
        var store=new WorkbookStore(database);var party=store.CreateParty("Synthetic buyer");
        Assert.Throws<BusinessRuleException>(()=>store.CreateAgent(" "));Assert.Throws<BusinessRuleException>(()=>store.CreateAgent(new string('A',201)));
        var agent=store.CreateAgent("  Synthetic   Agent  ");Assert.Equal("Synthetic Agent",agent.Name);
        Assert.Throws<BusinessRuleException>(()=>store.CreateAgent("synthetic agent"));
        Assert.Throws<BusinessRuleException>(()=>store.CreateRegisterInvoice(party.Id,0,new("India","Unknown agent","PI",new(2026,5,1))));
        Assert.Throws<BusinessRuleException>(()=>store.CreateRegisterInvoice(party.Id,0,new("India",agent.Name,"PI",new(2026,5,1),"unknown-id")));
        Assert.Empty(store.Load("form",party.Id).Rows);
        store.CreateRegisterInvoice(party.Id,0,new("India","synthetic agent","PI",new(2026,5,1),agent.Id));
        var row=Assert.Single(store.Load("form",party.Id).Rows);Assert.Equal(agent.Id,row["_agentId"]);Assert.Equal(agent.Name,row["F27"]);
        Assert.Throws<BusinessRuleException>(()=>store.CreateParty(" synthetic buyer "));
    }
    [Fact]public void MigrationCarriesLegacyAgentsIntoMasterWithoutChangingInvoiceValues()
    {
        var paths=database.Paths;var store=new WorkbookStore(database);var party=store.CreateParty("Synthetic migration buyer");var existing=store.CreateAgent("Existing agent");
        store.Save("form",0,[new(){{"_id","kept-1"},{"F9","OLD-1"},{"F27",existing.Name},{"_agentId","old-unused-reference"}},new(){{"_id","kept-2"},{"F9","OLD-2"},{"F27","Other legacy agent"}}],party.Id);
        database.Dispose();
        using(var connection=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=paths.Database,Pooling=false}.ToString()))
        {
            connection.Open();using var command=connection.CreateCommand();command.CommandText="DELETE FROM schema_migrations WHERE version=13";command.ExecuteNonQuery();
        }
        using var upgraded=new PortableDatabase(paths);var next=new WorkbookStore(upgraded);var agents=next.GetAgents();Assert.Equal(2,agents.Count);Assert.All(agents,agent=>Assert.Equal(1,agent.Invoices));
        Assert.Equal(existing.Id,agents.Single(agent=>agent.Name==existing.Name).Id);
        var rows=next.Load("form",party.Id).Rows;Assert.Equal("kept-1",rows[0]["_id"]);Assert.Equal("OLD-1",rows[0]["F9"]);Assert.Equal(existing.Name,rows[0]["F27"]);Assert.Equal(existing.Id,rows[0]["_agentId"]);
        Assert.Equal("Other legacy agent",rows[1]["F27"]);Assert.Equal(agents.Single(agent=>agent.Name=="Other legacy agent").Id,rows[1]["_agentId"]);
    }
    [Fact]public void TestAgentSeedingOnlyFillsBlanksPreservesPaymentsAndCanBeRepeated()
    {
        var store=new WorkbookStore(database);var party=store.CreateParty("Synthetic seed buyer");var existing=store.CreateAgent("Existing agent");Assert.Equal(existing.Name,existing.ToString());
        Dictionary<string,string> Row(string id,string agent)=>new(){{"_id",id},{"F1",party.Name},{"F9",id},{"F10","2026-06-01"},{"F5","15000"},{"F6","10000"},{"F7","9000"},{"F27",agent}};
        store.Save("form",0,[Row("SEED-1",""),Row("SEED-2",existing.Name)],party.Id);
        var workflow=new ReportWorkflow(store);workflow.RecordReceipt(party.Id,new(2026,7,1),500,"Synthetic payment",[new("SEED-1","Bank",500)]);
        var before=store.Load("form",party.Id);var payments=System.Text.Json.JsonSerializer.Serialize(workflow.Receipts(party.Id));var seed=store.SeedAgentTestData();
        Assert.Equal(3,seed.AgentsCreated);Assert.Equal(1,seed.InvoicesAssigned);
        var after=store.Load("form",party.Id);Assert.Equal(before.Revision+1,after.Revision);Assert.StartsWith("[TEST]",after.Rows[0]["F27"]);Assert.Equal(existing.Name,after.Rows[1]["F27"]);
        for(int i=0;i<before.Rows.Count;i++)foreach(var cell in before.Rows[i].Where(cell=>cell.Key is not("F27" or "_agentId")))Assert.Equal(cell.Value,after.Rows[i][cell.Key]);
        Assert.Equal(payments,System.Text.Json.JsonSerializer.Serialize(workflow.Receipts(party.Id)));
        var again=store.SeedAgentTestData();Assert.Equal(0,again.AgentsCreated);Assert.Equal(0,again.InvoicesAssigned);Assert.Equal(after.Revision,store.Load("form",party.Id).Revision);
        after.Rows[0]["F27"]=existing.Name;Assert.Throws<BusinessRuleException>(()=>store.Save("form",after.Revision,after.Rows,party.Id));
    }
}
