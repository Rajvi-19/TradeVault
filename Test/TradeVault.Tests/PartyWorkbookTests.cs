using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;
namespace TradeVault.Tests;

public sealed class PartyWorkbookTests:IDisposable
{
    private readonly string root=Path.Combine(AppContext.BaseDirectory,"TestData",Guid.NewGuid().ToString("N"));
    private static List<Dictionary<string,string>> Data(string marker,string opening="0")=>[new(){["_metadata"]="1",["opening"]=opening,["terms"]="90"},new(){["B"]=marker,["C"]="100"}];
    [Fact]
    public void PartiesHaveIndependentWorksheetsRevisionsAndMetadataAcrossReopen()
    {
        Guid aId,bId;
        using(var db=new PortableDatabase(new(root)))
        {
            var store=new WorkbookStore(db);
            Assert.Single(store.GetParties());
            var a=store.CreateParty("Party A");var b=store.CreateParty("Party B");aId=a.Id;bId=b.Id;
            foreach(var sheet in new[]{"form","details","ledger"})
            {
                Assert.Empty(store.Load(sheet,b.Id).Rows);
                Assert.Equal(1,store.Save(sheet,0,Data("A","100"),a.Id));
                Assert.Equal(1,store.Save(sheet,0,Data("B","500"),b.Id));
                Assert.Equal("100",store.Load(sheet,a.Id).Rows[0]["opening"]);
                Assert.Equal("500",store.Load(sheet,b.Id).Rows[0]["opening"]);
                Assert.Empty(store.Load(sheet).Rows);
            }
        }
        using var reopen=new PortableDatabase(new(root));var saved=new WorkbookStore(reopen);
        Assert.Equal("A",saved.Load("ledger",aId).Rows[1]["B"]);Assert.Equal("B",saved.Load("ledger",bId).Rows[1]["B"]);
    }
    [Fact]
    public void InvalidAndDuplicatePartyOperationsAreRejected()
    {
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);
        store.CreateParty("  Party   A ");
        Assert.Throws<BusinessRuleException>(()=>store.CreateParty("party a"));
        Assert.Throws<BusinessRuleException>(()=>store.CreateParty(" "));
        Assert.Throws<BusinessRuleException>(()=>store.CreateParty("Unassigned records"));
        Assert.Throws<BusinessRuleException>(()=>store.Load("ledger",Guid.NewGuid()));
        Assert.Throws<BusinessRuleException>(()=>store.Save("ledger",0,Data("bad"),Guid.NewGuid()));
    }
    [Fact]
    public void AssignmentMovesMetadataAtomicallyAndRejectsOverwriteAndStaleSaves()
    {
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);
        var a=store.CreateParty("A");var b=store.CreateParty("B");
        store.Save("ledger",0,Data("legacy","42"));
        store.Save("ledger",0,Data("keep","900"),b.Id);
        Assert.Throws<BusinessRuleException>(()=>store.AssignUnassigned("ledger",b.Id,1));
        Assert.Equal("legacy",store.Load("ledger").Rows[1]["B"]);
        store.AssignUnassigned("ledger",a.Id,1);
        Assert.Equal("42",store.Load("ledger",a.Id).Rows[0]["opening"]);
        Assert.Empty(store.Load("ledger").Rows);
        Assert.Throws<BusinessRuleException>(()=>store.Save("ledger",1,Data("stale")));
        Assert.Equal("keep",store.Load("ledger",b.Id).Rows[1]["B"]);
    }
    [Fact]
    public void MixedInvoiceOwnershipCannotBeSilentlyAssigned()
    {
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);var a=store.CreateParty("A");
        store.Save("form",0,[new(){["F1"]="B",["F9"]="LEGACY"}]);
        Assert.Throws<BusinessRuleException>(()=>store.AssignUnassigned("form",a.Id,1));
        Assert.Empty(store.Load("form",a.Id).Rows);Assert.Single(store.Load("form").Rows);
    }
    [Fact]
    public void VersionTwoMigrationKeepsExactLegacyDataAndMakesUsableBackup()
    {
        var paths=new PortablePaths(root);
        using(var created=new PortableDatabase(paths)){}
        var legacy=JsonSerializer.Serialize(Data("legacy","123.45"));
        using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=paths.Database,Pooling=false}.ToString()))
        {
            c.Open();using var command=c.CreateCommand();
            command.CommandText="DROP TABLE IF EXISTS account_recovery; DROP TABLE party_worksheets; DROP TABLE worksheet_parties; DROP TABLE workflow_documents; DROP TABLE workflow_audit; DELETE FROM schema_migrations WHERE version>=3; INSERT INTO worksheets VALUES('details',7,$data)";
            command.Parameters.AddWithValue("$data",legacy);command.ExecuteNonQuery();
        }
        using(var migrated=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(migrated);var snapshot=store.Load("details");
            Assert.Equal(7,snapshot.Revision);Assert.Equal(legacy,JsonSerializer.Serialize(snapshot.Rows));Assert.Single(store.GetParties());
        }
        var backup=Assert.Single(Directory.GetFiles(paths.Backups,"*.db"));
        using var check=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=backup,Pooling=false}.ToString());check.Open();using var sql=check.CreateCommand();
        sql.CommandText="SELECT MAX(version) FROM schema_migrations";Assert.Equal(2L,sql.ExecuteScalar());
        sql.CommandText="SELECT data FROM worksheets WHERE name='details'";Assert.Equal(legacy,sql.ExecuteScalar());
        sql.CommandText="PRAGMA integrity_check";Assert.Equal("ok",sql.ExecuteScalar());
    }
    public void Dispose(){Directory.Delete(root,true);}
}
