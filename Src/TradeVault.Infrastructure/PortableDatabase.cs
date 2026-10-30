using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TradeVault.Infrastructure;

/// <summary>Owns the data-folder lease. Dispose before ejecting the drive.</summary>
public sealed class PortableDatabase : IDisposable
{
    private readonly FileStream lease;
    private bool disposed;
    public PortablePaths Paths { get; }

    public PortableDatabase(PortablePaths paths)
    {
        Paths = paths;
        paths.EnsureDirectories();
        try{lease = new FileStream(Path.Combine(paths.Data, ".instance.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);}
        catch(IOException ex)when((ex.HResult&0xFFFF) is 32 or 33)
        {throw new IOException("TradeVault is already open from this application folder. Close its other window before opening it again.",ex);}
        try { Initialize(); }
        catch { lease.Dispose(); throw; }
    }

    internal SqliteConnection Open()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Paths.Database, Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false, ForeignKeys = true, DefaultTimeout = 5
        }.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            // No business-data sort/temp files on the host disk. No WAL sidecar lifecycle.
            command.CommandText = "PRAGMA temp_store=MEMORY; PRAGMA synchronous=FULL;";
            command.ExecuteNonQuery();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    private void Initialize()
    {
        using var connection = Open();
        // Snapshot an existing database before upgrading its schema.
        using (var inspect = connection.CreateCommand())
        {
            inspect.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_migrations'";
            if(Convert.ToInt32(inspect.ExecuteScalar())>0)
            {
                inspect.CommandText="SELECT COALESCE(MAX(version),0) FROM schema_migrations";
                var previous=Convert.ToInt32(inspect.ExecuteScalar());
                if(previous is >0 and <9)CreateBackup();
            }
        }
        using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA secure_delete=ON;";
            setup.ExecuteScalar();
        }
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY, applied_at TEXT NOT NULL
            ) STRICT;
            SELECT COALESCE(MAX(version), 0) FROM schema_migrations;
            """;
        var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (version > 9) throw new InvalidOperationException("This database requires a newer TradeVault application.");
        if (version == 0)
        {
            using var stream = typeof(PortableDatabase).Assembly.GetManifestResourceStream(
                "TradeVault.Infrastructure.Migrations.001_Initial.sql")
                ?? throw new InvalidOperationException("Initial database migration is missing.");
            using var reader = new StreamReader(stream);
            command.CommandText = reader.ReadToEnd();
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO schema_migrations VALUES (1, $now); INSERT INTO app_instance VALUES ($id, $now);";
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.ExecuteNonQuery();
        }
        if (version < 2)
        {
            command.Parameters.Clear();
            command.CommandText = """
                CREATE TABLE worksheets(name TEXT PRIMARY KEY, revision INTEGER NOT NULL, data TEXT NOT NULL) STRICT;
                INSERT INTO schema_migrations VALUES(2, strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
        }
        if(version<3)
        {
            command.Parameters.Clear();
            command.CommandText="""
                CREATE TABLE worksheet_parties(
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    name_key TEXT NOT NULL UNIQUE,
                    is_unassigned INTEGER NOT NULL CHECK(is_unassigned IN (0,1))
                ) STRICT;
                INSERT INTO worksheet_parties VALUES('00000000-0000-0000-0000-000000000000','Unassigned records','UNASSIGNED RECORDS',1);
                CREATE TABLE party_worksheets(
                    party_id TEXT NOT NULL REFERENCES worksheet_parties(id),
                    name TEXT NOT NULL,
                    revision INTEGER NOT NULL CHECK(revision>0),
                    data TEXT NOT NULL,
                    PRIMARY KEY(party_id,name)
                ) STRICT;
                INSERT INTO party_worksheets SELECT '00000000-0000-0000-0000-000000000000',name,revision,data FROM worksheets;
                INSERT INTO schema_migrations VALUES(3,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
        }
        if(version<4)
        {
            command.Parameters.Clear();
            command.CommandText="""
                CREATE TABLE workflow_documents(kind TEXT NOT NULL,id TEXT NOT NULL,revision INTEGER NOT NULL,data TEXT NOT NULL,PRIMARY KEY(kind,id)) STRICT;
                CREATE TABLE workflow_audit(id TEXT PRIMARY KEY,actor TEXT NOT NULL,action TEXT NOT NULL,entity TEXT NOT NULL,before_json TEXT,after_json TEXT,created_at TEXT NOT NULL) STRICT;
                INSERT INTO schema_migrations VALUES(4,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            command.ExecuteNonQuery();
        }
        if(version<5)
        {
            command.Parameters.Clear();command.CommandText="""
                INSERT INTO schema_migrations VALUES(5,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;command.ExecuteNonQuery();
        }
        if(version<6)
        {
            command.Parameters.Clear();command.CommandText="""
                DROP TABLE IF EXISTS account_recovery;
                DROP TABLE IF EXISTS credentials;
                INSERT INTO schema_migrations VALUES(6,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;command.ExecuteNonQuery();
        }
        if(version<7)
        {
            command.Parameters.Clear();command.CommandText="""
                DROP TABLE IF EXISTS workspace_settings;
                DELETE FROM workflow_documents WHERE kind='import';
                DELETE FROM workflow_audit WHERE action='import';
                DELETE FROM party_worksheets WHERE name='operator-reports';
                DELETE FROM worksheets WHERE name='operator-reports';
                INSERT INTO schema_migrations VALUES(7,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;command.ExecuteNonQuery();
        }
        if(version<8)
        {
            command.Parameters.Clear();command.CommandText="""
                DELETE FROM workflow_documents WHERE kind='mapping';
                DELETE FROM workflow_audit WHERE action='mapping';
                INSERT INTO schema_migrations VALUES(8,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;command.ExecuteNonQuery();
        }
        if(version<9)
        {
            command.Parameters.Clear();command.CommandText="INSERT INTO schema_migrations VALUES(9,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>Consistent snapshot under this USB's Backups directory; never raw-copy a live DB.</summary>
    public string CreateBackup()
    {
        var destinationPath = Path.Combine(Paths.Backups,
            $"tradevault-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        using var source = Open();
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = destinationPath, Pooling = false }.ToString());
        destination.Open();
        source.BackupDatabase(destination);
        using var check = destination.CreateCommand();
        check.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal))
            throw new IOException("The backup failed its integrity check.");
        return destinationPath;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lease.Dispose();
    }
    internal void RestoreVerified(string path)
    {
        var rollback=CreateBackup();
        void Copy(string sourcePath){using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=sourcePath,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());source.Open();using var destination=Open();source.BackupDatabase(destination);}
        try{Copy(path);Initialize();}
        catch{Copy(rollback);throw;}
    }
}
