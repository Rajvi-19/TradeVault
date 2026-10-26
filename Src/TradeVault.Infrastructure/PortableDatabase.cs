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
        lease = new FileStream(Path.Combine(paths.Data, ".instance.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
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
        using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "PRAGMA journal_mode=DELETE;";
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
        if (version > 1) throw new InvalidOperationException("This database requires a newer TradeVault application.");
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
}
