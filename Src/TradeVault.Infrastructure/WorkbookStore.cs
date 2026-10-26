using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed record LoginSession(string Username, string Role, Guid Token);
public sealed record WorksheetSnapshot(int Revision, List<Dictionary<string, string>> Rows);

/// <summary>Authenticated persistence for independent Excel-shaped worksheets.</summary>
public sealed class WorkbookStore
{
    private readonly PortableDatabase database;
    private readonly Dictionary<Guid, LoginSession> sessions = new();
    private int failedAttempts;
    private DateTimeOffset blockedUntil;
    public WorkbookStore(PortableDatabase database)
    {
        this.database = database;
        using var c = database.Open();
        using var t = c.BeginTransaction();
        foreach (var (user, role, password) in new[] { ("owner", "Owner", "Owner@123"), ("operator", "Operator", "Operator@123") })
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
            using var cmd = c.CreateCommand(); cmd.Transaction = t;
            cmd.CommandText = "INSERT OR IGNORE INTO credentials VALUES($user,$role,$salt,$hash)";
            cmd.Parameters.AddWithValue("$user", user); cmd.Parameters.AddWithValue("$role", role);
            cmd.Parameters.AddWithValue("$salt", salt); cmd.Parameters.AddWithValue("$hash", hash); cmd.ExecuteNonQuery();
        }
        t.Commit();
    }
    public LoginSession Login(string username, string password)
    {
        if (DateTimeOffset.UtcNow < blockedUntil) throw new BusinessRuleException("Too many attempts. Try again in 30 seconds.");
        using var c = database.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT username,role,salt,hash FROM credentials WHERE username=$name";
        cmd.Parameters.AddWithValue("$name", username.Trim().ToLowerInvariant());
        using var reader = cmd.ExecuteReader();
        var found = reader.Read();
        var salt = found ? (byte[])reader[2] : new byte[16];
        var candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
        if (!found || !CryptographicOperations.FixedTimeEquals(candidate, (byte[])reader[3]))
        {
            if (++failedAttempts >= 5) { blockedUntil = DateTimeOffset.UtcNow.AddSeconds(30); failedAttempts = 0; }
            throw new BusinessRuleException("Invalid username or password.");
        }
        failedAttempts = 0;
        var session = new LoginSession(reader.GetString(0), reader.GetString(1), Guid.NewGuid());
        sessions.Add(session.Token, session); return session;
    }
    public void Logout(LoginSession session) => sessions.Remove(session.Token);
    private void Authorize(LoginSession session, string name)
    {
        if (!sessions.TryGetValue(session.Token, out var actual) || actual != session)
            throw new BusinessRuleException("Sign in before accessing records.");
        if (name is not ("form" or "details" or "ledger" or "operator-reports")) throw new BusinessRuleException("Unknown worksheet.");
        if (name == "operator-reports" && actual.Role != "Owner") throw new BusinessRuleException("Owner access required.");
    }
    public WorksheetSnapshot Load(LoginSession session, string name)
    {
        Authorize(session, name);
        using var c = database.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT revision,data FROM worksheets WHERE name=$name"; cmd.Parameters.AddWithValue("$name", name);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? new(reader.GetInt32(0), JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(1))!) : new(0, []);
    }
    public int Save(LoginSession session, string name, int expectedRevision, List<Dictionary<string,string>> rows)
    {
        Authorize(session, name);
        if (name == "operator-reports") throw new BusinessRuleException("Submitted reports cannot be edited.");
        using var c = database.Open(); using var t = c.BeginTransaction();
        using var cmd = c.CreateCommand(); cmd.Transaction = t;
        cmd.CommandText = "SELECT COALESCE((SELECT revision FROM worksheets WHERE name=$name),0)";
        cmd.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt32(cmd.ExecuteScalar()) != expectedRevision) throw new BusinessRuleException("Records changed. Reload before saving.");
        cmd.CommandText = "INSERT INTO worksheets VALUES($name,$revision,$data) ON CONFLICT(name) DO UPDATE SET revision=$revision,data=$data";
        cmd.Parameters.AddWithValue("$revision", checked(expectedRevision + 1)); cmd.Parameters.AddWithValue("$data", JsonSerializer.Serialize(rows)); cmd.ExecuteNonQuery();
        cmd.Parameters.Clear(); cmd.CommandText = "INSERT INTO audit_events VALUES($id,$entity,$action,$now)";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString()); cmd.Parameters.AddWithValue("$entity", name);
        cmd.Parameters.AddWithValue("$action", $"WorksheetSaved:{session.Username}"); cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); cmd.ExecuteNonQuery();
        t.Commit(); return expectedRevision + 1;
    }
}
