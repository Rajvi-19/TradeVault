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
                if(previous is >0 and <13)CreateBackup();
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
        if (version > 13 || version==10&&!IsCompatibleRevertedSchema(connection,transaction))
            throw new InvalidOperationException("This database requires a newer TradeVault application.");
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
        if(version<11)
        {
            command.Parameters.Clear();command.CommandText="PRAGMA table_info(invoices)";
            bool hasShipping=false;
            using(var reader=command.ExecuteReader())while(reader.Read())if(reader.GetString(1)=="shipping_cents")hasShipping=true;
            if(hasShipping){command.CommandText="ALTER TABLE invoices DROP COLUMN shipping_cents";command.ExecuteNonQuery();}
            foreach(var table in new[]{"worksheets","party_worksheets"})
            {
                command.CommandText=$"SELECT rowid,name,data FROM {table} WHERE name IN ('form','details')";
                var updates=new List<(long Id,string Data)>();
                using(var reader=command.ExecuteReader())while(reader.Read())
                {
                    var rows=System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(2))!;
                    var key=reader.GetString(1)=="form"?"F18":"H";
                    if(rows.Any(row=>row.ContainsKey(key)))
                    {
                        foreach(var row in rows)row.Remove(key);
                        if(key=="H")
                        {
                            decimal running=0;bool known=true;
                            var metadata=rows.FirstOrDefault(row=>row.ContainsKey("_metadata"));
                            if(metadata!=null)decimal.TryParse(metadata.GetValueOrDefault("opening"),System.Globalization.NumberStyles.Number,CultureInfo.InvariantCulture,out running);
                            foreach(var row in rows.Where(row=>!row.ContainsKey("_metadata")&&row.GetValueOrDefault("_blank")!="1"))
                            {
                                if(decimal.TryParse(row.GetValueOrDefault("C"),System.Globalization.NumberStyles.Number,CultureInfo.InvariantCulture,out var original)&&decimal.TryParse(row.GetValueOrDefault("D"),System.Globalization.NumberStyles.Number,CultureInfo.InvariantCulture,out var contract))
                                {
                                    var difference=original-contract;row["I"]=difference.ToString("0.00",CultureInfo.InvariantCulture);
                                    var receiptText=row.GetValueOrDefault("J","");
                                    if(receiptText.Length==0)receiptText="0";
                                    if(!decimal.TryParse(receiptText,System.Globalization.NumberStyles.Number,CultureInfo.InvariantCulture,out var received))known=false;
                                    if(known)running+=difference-received;
                                }
                                else {row["I"]="";known=false;}
                                row["K"]=known?running.ToString("0.00",CultureInfo.InvariantCulture):"";
                            }
                        }
                        updates.Add((reader.GetInt64(0),System.Text.Json.JsonSerializer.Serialize(rows)));
                    }
                }
                foreach(var update in updates){command.Parameters.Clear();command.CommandText=$"UPDATE {table} SET data=$data WHERE rowid=$id";command.Parameters.AddWithValue("$data",update.Data);command.Parameters.AddWithValue("$id",update.Id);command.ExecuteNonQuery();}
            }
            command.Parameters.Clear();command.CommandText="INSERT INTO schema_migrations VALUES(11,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";command.ExecuteNonQuery();
        }
        if(version<12)
        {
            foreach(var table in new[]{"worksheets","party_worksheets"})
            {
                command.Parameters.Clear();command.CommandText=$"SELECT rowid,data FROM {table} WHERE name='form'";
                var updates=new List<(long Id,string Data)>();
                using(var reader=command.ExecuteReader())while(reader.Read())
                {
                    var rows=System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(1))!;
                    foreach(var row in rows.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"))
                    {
                        foreach(var key in RegisterInvoiceSchema.VisibleKeys)row.TryAdd(key,"");
                        row.TryAdd("_dueOverride","");
                        // Preserve any previously configured nonstandard deadline as an explicit override.
                        if(row["_dueOverride"].Length==0&&DateOnly.TryParseExact(row["F19"],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var oldDue))
                        {
                            DateOnly? automatic=DateOnly.TryParseExact(row["F17"],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var bl)&&bl.DayNumber<=DateOnly.MaxValue.DayNumber-90?bl.AddDays(90):null;
                            if(oldDue!=automatic)row["_dueOverride"]=row["F19"];
                        }
                        var due=RegisterInvoiceSchema.DueDate(row);
                        row["F19"]=due?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??"";
                        row["F20"]=due is {} deadline&&deadline.DayNumber>=30?deadline.AddDays(-30).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):"";
                    }
                    updates.Add((reader.GetInt64(0),System.Text.Json.JsonSerializer.Serialize(rows)));
                }
                foreach(var update in updates){command.Parameters.Clear();command.CommandText=$"UPDATE {table} SET data=$data WHERE rowid=$id";command.Parameters.AddWithValue("$data",update.Data);command.Parameters.AddWithValue("$id",update.Id);command.ExecuteNonQuery();}
            }
            command.Parameters.Clear();command.CommandText="INSERT INTO schema_migrations VALUES(12,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";command.ExecuteNonQuery();
        }
        if(version<13)
        {
            WorkbookStore.UpgradeAgentMaster(connection,transaction);
            command.Parameters.Clear();command.CommandText="INSERT INTO schema_migrations VALUES(13,strftime('%Y-%m-%dT%H:%M:%fZ','now'))";command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    /// <summary>Recognizes only the additive table from the reverted version-10 feature.</summary>
    internal static bool IsCompatibleRevertedSchema(SqliteConnection connection,SqliteTransaction? transaction=null)
    {
        // The reverted version-10 change only added this unused master table. Keep it and its
        // records intact; never lower the migration version or accept arbitrary future schemas.
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT COUNT(*) FROM schema_migrations WHERE version=9";
        if(Convert.ToInt32(command.ExecuteScalar())!=1)return false;
        command.CommandText="PRAGMA table_info(worksheet_agents)";
        using var reader=command.ExecuteReader();var columns=new List<string>();
        while(reader.Read())columns.Add($"{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetInt32(3)}|{reader.GetInt32(5)}");
        return columns.SequenceEqual(new[]{"id|TEXT|1|1","name|TEXT|1|0","name_key|TEXT|1|0"});
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
