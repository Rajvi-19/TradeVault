using Microsoft.Data.Sqlite;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class FixedReportingTests
{
    [Fact]
    public void ReportsUseOurCustomsAndDatedNemichemReceiptsWithoutSetup()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"FixedReporting",Guid.NewGuid().ToString("N"));
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);var party=store.CreateParty("Fixed source test").Id;
        store.Save("form",0,[new(){["_id"]="fixed",["F1"]="Fixed source test",["F9"]="INV-FIXED",["F10"]="2026-06-01",["F5"]="15000",["F6"]="10000",["F7"]="9000",["F18"]="3800",["F22"]="9000",["F25"]="3000",["F26"]="2026-07-01",["F23"]="200",["F24"]="2026-08-01"}],party);
        var service=new ReportWorkflow(store);
        Assert.Equal("0.00",service.Build(party,new(2026,6,30),true).Tables[0].Rows[0]["E"]);
        var july=service.Build(party,new(2026,7,31),true).Tables[0].Rows[0];
        Assert.Equal("10000.00",july["D"]);Assert.Equal("3000.00",july["E"]);Assert.Equal("7000.00",july["F"]);
        Assert.False(july.ContainsKey("H"));Assert.Equal("5000.00",july["I"]);Assert.Equal("0.00",july["J"]);
        var august=service.Build(party,new(2026,8,1),true);
        Assert.Equal("200.00",august.Tables[0].Rows[0]["J"]);Assert.Equal(11800m,august.Tables[1].Totals["E"]);
        Assert.DoesNotContain(august.Tables[1].Rows,r=>r["B"].Contains("Opening")||r["B"].Contains("shipping"));
        Assert.Equal(6000m,service.RegisterDifference(service.Invoices(party).Single()));
        service.RecordReceipt(party,new(2026,8,2),500,"New receipt",[new("fixed","Bank",500)]);
        Assert.Equal("6500.00",service.BuildCurrent(party).Tables[0].Rows[0]["F"]);
    }

    [Fact]
    public void OldSetupIsRemovedWhileInvoicesAndReceiptsRemainUnchanged()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"FixedReporting",Guid.NewGuid().ToString("N"));var paths=new PortablePaths(root);Guid party;string before;
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);party=store.CreateParty("Upgrade test").Id;
            store.Save("form",0,[new(){["_id"]="upgrade",["F9"]="INV-UPGRADE",["F1"]="Upgrade test",["F10"]="2026-06-01",["F5"]="15000",["F6"]="10000",["F7"]="9000"}],party);
            var service=new ReportWorkflow(store);service.RecordReceipt(party,new(2026,7,1),100,"Keep payment",[new("upgrade","Bank",100)]);
            before=System.Text.Json.JsonSerializer.Serialize(new{Rows=store.Load("form",party),Receipts=service.Receipts(party)});
        }
        using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=paths.Database,Pooling=false}.ToString()))
        {
            c.Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO workflow_documents VALUES('mapping',$party,1,'{\"ContractField\":\"F7\",\"BankReceivedField\":\"F22\",\"DeductRegisteredShipping\":true,\"OpeningDate\":\"2026-01-01\",\"OpeningBank\":100,\"OpeningDifference\":200}'); DELETE FROM schema_migrations WHERE version>=8";cmd.Parameters.AddWithValue("$party",party.ToString());cmd.ExecuteNonQuery();
        }
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var service=new ReportWorkflow(store);
            Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(new{Rows=store.Load("form",party),Receipts=service.Receipts(party)}));
            Assert.Equal(14900m,service.BuildCurrent(party).Tables[1].Totals["E"]);
        }
        using var check=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=paths.Database,Pooling=false}.ToString());check.Open();using var sql=check.CreateCommand();
        sql.CommandText="SELECT COUNT(*) FROM workflow_documents WHERE kind='mapping'";Assert.Equal(0L,sql.ExecuteScalar());
    }
}
