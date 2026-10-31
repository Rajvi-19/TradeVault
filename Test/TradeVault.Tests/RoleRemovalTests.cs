using Microsoft.Data.Sqlite;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class RoleRemovalTests
{
    [Fact]
    public void FreshDatabaseHasNoAccountsRolesOrImportedReports()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"RoleRemoval",Guid.NewGuid().ToString("N"));
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);
        Assert.Single(store.GetParties());Assert.Empty(store.Load("form").Rows);
        Verify(db.Paths.Database);
    }

    

    private static SqliteConnection Open(string path)
    {
        var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Pooling=false}.ToString());c.Open();return c;
    }
    private static void Verify(string path)
    {
        using var c=Open(path);using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name IN ('credentials','account_recovery','workspace_settings')";Assert.Equal(0L,cmd.ExecuteScalar());
        cmd.CommandText="SELECT COUNT(*) FROM workflow_documents WHERE kind='import'";Assert.Equal(0L,cmd.ExecuteScalar());
        cmd.CommandText="SELECT COUNT(*) FROM workflow_audit WHERE action='import'";Assert.Equal(0L,cmd.ExecuteScalar());
        cmd.CommandText="SELECT MAX(version) FROM schema_migrations";Assert.Equal(13L,cmd.ExecuteScalar());
        cmd.CommandText="PRAGMA integrity_check";Assert.Equal("ok",cmd.ExecuteScalar());
    }
}
