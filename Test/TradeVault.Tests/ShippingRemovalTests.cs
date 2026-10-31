using Microsoft.Data.Sqlite;
using TradeVault.Application;
using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class ShippingRemovalTests
{
    [Fact]
    public void UpgradeRemovesShippingPreservesReceiptsAndMakesRestorableBackup()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"ShippingMigrationTests",Guid.NewGuid().ToString("N"));
        var paths=new PortablePaths(root);Guid party,invoice;
        using(var db=new PortableDatabase(paths))
        {
            var service=new TradeVaultService(db);party=service.CreateParty(new("Synthetic buyer","IN")).Id;
            invoice=service.CreateInvoice(new(party,"MIG-1",new(2026,6,1),Usd.FromDecimal(15000),Usd.FromDecimal(10000))).Id;
            service.RecordReceipt(new(party,new(2026,7,1),Usd.FromDecimal(500),"synthetic receipt",[new(invoice,Usd.FromDecimal(500))]));
        }
        using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=paths.Database,Pooling=false}.ToString()))
        {
            c.Open();using var cmd=c.CreateCommand();cmd.CommandText="""
                ALTER TABLE invoices ADD COLUMN shipping_cents INTEGER NOT NULL DEFAULT 380000 CHECK(shipping_cents>=0);
                DELETE FROM schema_migrations WHERE version>=10;
                INSERT INTO party_worksheets VALUES('00000000-0000-0000-0000-000000000000','form',1,'[{"F9":"OLD-1","F18":"3800","F22":"100","F23":"200","F24":"2026-07-01","F25":"300","F26":"2026-07-02","_id":"kept"}]');
                INSERT INTO party_worksheets VALUES('00000000-0000-0000-0000-000000000000','details',1,'[{"_metadata":"1","opening":"100"},{"B":"OLD-1","C":"15000","D":"10000","H":"3800","I":"1200","J":"200","K":"1100"}]');
                """;cmd.ExecuteNonQuery();
        }
        using(var db=new PortableDatabase(paths))
        {
            var service=new TradeVaultService(db);Assert.Equal(Usd.FromDecimal(9500),Assert.Single(service.GetBankContractBalances(party,new(2026,8,1))).Outstanding);
            Assert.Equal(invoice,Assert.Single(service.GetInvoices(party)).Id);
            var store=new WorkbookStore(db);var row=Assert.Single(store.Load("form").Rows);
            Assert.False(row.ContainsKey("F18"));Assert.Equal("kept",row["_id"]);
            Assert.Equal("100",row["F22"]);Assert.Equal("200",row["F23"]);Assert.Equal("2026-07-01",row["F24"]);Assert.Equal("300",row["F25"]);Assert.Equal("2026-07-02",row["F26"]);
            var detail=store.Load("details").Rows.Single(row=>!row.ContainsKey("_metadata"));Assert.False(detail.ContainsKey("H"));Assert.Equal("5000.00",detail["I"]);Assert.Equal("4900.00",detail["K"]);
            var backup=Assert.Single(Directory.GetFiles(paths.Backups,"*.db"));
            store.RestoreBackup(backup);Assert.False(Assert.Single(store.Load("form").Rows).ContainsKey("F18"));
            Assert.Equal(Usd.FromDecimal(9500),Assert.Single(service.GetBankContractBalances(party,new(2026,8,1))).Outstanding);
        }
        using var check=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=paths.Database,Pooling=false}.ToString());check.Open();using var verify=check.CreateCommand();
        verify.CommandText="SELECT COUNT(*) FROM pragma_table_info('invoices') WHERE name='shipping_cents'";Assert.Equal(0L,verify.ExecuteScalar());
        verify.CommandText="PRAGMA integrity_check";Assert.Equal("ok",verify.ExecuteScalar());
        verify.CommandText="PRAGMA foreign_key_check";Assert.Null(verify.ExecuteScalar());
    }
}
