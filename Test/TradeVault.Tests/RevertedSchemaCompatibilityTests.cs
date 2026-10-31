using Microsoft.Data.Sqlite;
using TradeVault.Infrastructure;
using TradeVault.Domain;
using Xunit;

namespace TradeVault.Tests;

public sealed class RevertedSchemaCompatibilityTests
{
    private static string NewRoot()=>Path.Combine(AppContext.BaseDirectory,"RevertedSchemaTests",Guid.NewGuid().ToString("N"));
    private static void Sql(string root,string text)
    {
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(root,"Data","tradevault.db"),Pooling=false}.ToString());connection.Open();using var command=connection.CreateCommand();command.CommandText=text;command.ExecuteNonQuery();
    }
    private static void AddKnownMigration(string root)=>Sql(root,"""
        DELETE FROM schema_migrations WHERE version>=10;
        DROP TABLE IF EXISTS worksheet_agents;
        CREATE TABLE worksheet_agents(id TEXT PRIMARY KEY,name TEXT NOT NULL,name_key TEXT NOT NULL UNIQUE) STRICT;
        INSERT INTO worksheet_agents VALUES('kept-agent','SYNTHETIC retained agent','SYNTHETIC RETAINED AGENT');
        INSERT INTO schema_migrations VALUES(10,'synthetic-test');
        """);
    [Fact]public void KnownAdditiveSchemaOpensAndRestoresWithoutDroppingData()
    {
        var root=NewRoot();Guid party;
        using(var database=new PortableDatabase(new(root)))
        {
            var store=new WorkbookStore(database);party=store.CreateParty("SYNTHETIC retained buyer").Id;
            store.Save("form",0,[new(){{"F9","KEEP-001"},{"_id","keep-id"},{"F27","SYNTHETIC retained agent"},{"F18","3800.00"}}],party);
        }
        AddKnownMigration(root);
        using(var database=new PortableDatabase(new(root)))
        {
            var store=new WorkbookStore(database);var row=Assert.Single(store.Load("form",party).Rows);Assert.Equal("KEEP-001",row["F9"]);Assert.Equal("SYNTHETIC retained agent",row["F27"]);Assert.False(row.ContainsKey("F18"));
            var backup=store.CreateBackup();store.CreateParty("After backup");store.RestoreBackup(backup);Assert.DoesNotContain(store.GetParties(),p=>p.Name=="After backup");
        }
        using var check=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(root,"Data","tradevault.db"),Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());check.Open();using var command=check.CreateCommand();
        command.CommandText="SELECT MAX(version) FROM schema_migrations";Assert.Equal(13L,command.ExecuteScalar());command.CommandText="SELECT name FROM worksheet_agents";Assert.Equal("SYNTHETIC retained agent",command.ExecuteScalar());
    }
    [Theory][InlineData(10)][InlineData(14)]public void UnknownSchemaIsStillRejected(int version)
    {
        var root=NewRoot();using(var db=new PortableDatabase(new(root))){}
        Sql(root,$"DROP TABLE IF EXISTS worksheet_agents; DELETE FROM schema_migrations WHERE version>=10; INSERT INTO schema_migrations VALUES({version},'unsupported-test')");
        Assert.Throws<InvalidOperationException>(()=>new PortableDatabase(new(root)));
    }
    [Fact]public void NewDatabasesUseVersionThirteen()
    {
        var root=NewRoot();using(var db=new PortableDatabase(new(root))){}
        using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path.Combine(root,"Data","tradevault.db"),Pooling=false}.ToString());connection.Open();using var command=connection.CreateCommand();command.CommandText="SELECT MAX(version) FROM schema_migrations";Assert.Equal(13L,command.ExecuteScalar());
    }
}
