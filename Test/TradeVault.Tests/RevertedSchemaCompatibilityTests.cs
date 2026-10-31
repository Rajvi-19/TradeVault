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
