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

    [Fact]
    public void MigrationAndLegacyRestoreRemoveOnlyObsoleteRoleAndImportData()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"RoleRemoval",Guid.NewGuid().ToString("N"));var paths=new PortablePaths(root);
        Guid party;
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);party=store.CreateParty("Existing company party").Id;
            store.Save("details",0,[new(){{"B","Existing invoice"},{"C","100"}}],party);
        }
        using(var c=Open(paths.Database))
        {
            using var cmd=c.CreateCommand();cmd.CommandText="""
                CREATE TABLE workspace_settings(name TEXT PRIMARY KEY,value TEXT NOT NULL);
                INSERT INTO workspace_settings VALUES('role','Operator');
                INSERT INTO workflow_documents VALUES('import','legacy-submission',1,'{}');
                INSERT INTO workflow_documents VALUES('export','preserve',1,'{}');
                INSERT INTO workflow_audit VALUES('old-import','Operator workspace','import','legacy',NULL,'{}','2026-10-29');
                INSERT INTO workflow_audit VALUES('old-receipt','Operator workspace','receipts','preserve',NULL,'{}','2026-10-29');
                DELETE FROM schema_migrations WHERE version>=7;
                """;cmd.ExecuteNonQuery();
        }
        var backup=Path.Combine(paths.Backups,"legacy-schema6.db");File.Copy(paths.Database,backup);
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);AssertPreserved(store);Verify(paths.Database);
            store.CreateParty("After upgrade");store.RestoreBackup(backup);
            Assert.DoesNotContain(store.GetParties(),p=>p.Name=="After upgrade");AssertPreserved(store);Verify(paths.Database);
        }
        void AssertPreserved(WorkbookStore store)
        {
            Assert.Contains(store.GetParties(),p=>p.Id==party);
            Assert.Equal("Existing invoice",Assert.Single(store.Load("details",party).Rows)["B"]);
            using var c=Open(paths.Database);using var cmd=c.CreateCommand();
            cmd.CommandText="SELECT COUNT(*) FROM workflow_documents WHERE kind='export' AND id='preserve'";Assert.Equal(1L,cmd.ExecuteScalar());
            cmd.CommandText="SELECT COUNT(*) FROM workflow_audit WHERE id='old-receipt'";Assert.Equal(1L,cmd.ExecuteScalar());
        }
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
        cmd.CommandText="SELECT MAX(version) FROM schema_migrations";Assert.Equal(9L,cmd.ExecuteScalar());
        cmd.CommandText="PRAGMA integrity_check";Assert.Equal("ok",cmd.ExecuteScalar());
    }
}
