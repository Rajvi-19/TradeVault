using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class WorkbookStore
{
    private void GuardRegister(LoginSession session,Guid party,List<Dictionary<string,string>> candidate)
    {
        var rows=candidate.Where(r=>!r.ContainsKey("_metadata")).ToList();
        if(rows.Where(r=>!string.IsNullOrWhiteSpace(r.GetValueOrDefault("_id"))).GroupBy(r=>r["_id"],StringComparer.Ordinal).Any(g=>g.Count()>1))throw new BusinessRuleException("Duplicate internal invoice identifiers. Create a new row instead of copying internal identifiers.");
        for(int i=0;i<rows.Count;i++)
        {
            var issues=InvoiceRules.Validate(rows[i]);
            if(issues.Count>0)throw new BusinessRuleException($"Row {i+1}: "+string.Join("\n",issues.Select(issue=>issue.Message)));
        }
        if(rows.Where(r=>!string.IsNullOrWhiteSpace(r.GetValueOrDefault("F9"))).GroupBy(r=>r["F9"].Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new BusinessRuleException("Duplicate invoice reference for this party.");
        var receipts=Document<TradeVault.Application.ReceiptBook>(session,"receipts",party.ToString())?.Receipts??[];
        if(receipts.Count==0)return;
        var old=Load(session,"form",party).Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
        foreach(var id in receipts.SelectMany(r=>r.Allocations).Select(a=>a.InvoiceId).Distinct())
        {
            var previous=old.Single(r=>ReportWorkflow.InvoiceKey(party,r)==id);var next=rows.SingleOrDefault(r=>ReportWorkflow.InvoiceKey(party,r)==id)??throw new BusinessRuleException("Invoices with receipt history cannot be deleted.");
            foreach(var field in new[]{"F5","F6","F7","F22","F23","F25","F24","F26","F18","F10"})
                if(previous.GetValueOrDefault(field,"")!=next.GetValueOrDefault(field,""))throw new BusinessRuleException("Invoice amounts/dates with receipt history are protected. Use a documented correction workflow rather than changing historical inputs.");
        }
    }
    internal PortableDatabase Database=>database;
    internal void CheckSession(LoginSession session,bool owner=false)=>Authorize(session,owner?"operator-reports":"form");
    internal T? Document<T>(LoginSession session,string kind,string id)
    {
        CheckSession(session,kind=="import");using var c=database.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT data FROM workflow_documents WHERE kind=$k AND id=$i";cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",id);
        return cmd.ExecuteScalar() is string json?JsonSerializer.Deserialize<T>(json):default;
    }
    internal List<T> Documents<T>(LoginSession session,string kind)
    {
        CheckSession(session,kind=="import");using var c=database.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT data FROM workflow_documents WHERE kind=$k ORDER BY id";cmd.Parameters.AddWithValue("$k",kind);
        using var r=cmd.ExecuteReader();var result=new List<T>();while(r.Read())result.Add(JsonSerializer.Deserialize<T>(r.GetString(0))!);return result;
    }
    internal T Mutate<T>(LoginSession session,string kind,string id,Func<T?,T> transform)
    {
        CheckSession(session,kind=="import");using var c=database.Open();using var t=c.BeginTransaction(deferred:false);using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText="SELECT data FROM workflow_documents WHERE kind=$k AND id=$i";cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",id);
        var before=cmd.ExecuteScalar() as string;var next=transform(before==null?default:JsonSerializer.Deserialize<T>(before));var after=JsonSerializer.Serialize(next);
        cmd.CommandText="INSERT INTO workflow_documents VALUES($k,$i,1,$d) ON CONFLICT(kind,id) DO UPDATE SET revision=revision+1,data=$d";cmd.Parameters.AddWithValue("$d",after);cmd.ExecuteNonQuery();
        cmd.Parameters.Clear();cmd.CommandText="INSERT INTO workflow_audit VALUES($id,$actor,$action,$entity,$before,$after,$time)";
        cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$actor",session.Username);cmd.Parameters.AddWithValue("$action",kind);cmd.Parameters.AddWithValue("$entity",id);cmd.Parameters.AddWithValue("$before",(object?)AuditContent(kind,before)??DBNull.Value);cmd.Parameters.AddWithValue("$after",AuditContent(kind,after)!);cmd.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();t.Commit();return next;
    }
    private static string? AuditContent(string kind,string? json)
    {
        if(json==null||kind!="import")return json;
        var item=JsonSerializer.Deserialize<TradeVault.Application.ImportedSubmission>(json)!;
        return JsonSerializer.Serialize(new{item.Report.SourceId,item.Report.SubmissionId,item.Report.Revision,item.PackageHash,item.Status,item.ReviewNote,item.ImportedAt});
    }
    public string CreateBackup(LoginSession session){CheckSession(session);return database.CreateBackup();}
    public IReadOnlyList<string> Backups(LoginSession session){CheckSession(session);return Directory.GetFiles(database.Paths.Backups,"*.db").OrderDescending().ToArray();}
    public void ChangePassword(LoginSession session,string current,string replacement,bool resetOperator=false)
    {
        CheckSession(session,resetOperator);if(replacement.Length<6||replacement.Length>128)throw new BusinessRuleException("Use 6–128 characters for the new password.");
        using var c=database.Open();using var t=c.BeginTransaction();using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText="SELECT salt,hash FROM credentials WHERE username=$u";cmd.Parameters.AddWithValue("$u",session.Username);
        using(var r=cmd.ExecuteReader()) {if(!r.Read()||!CryptographicOperations.FixedTimeEquals(Rfc2898DeriveBytes.Pbkdf2(current,(byte[])r[0],600000,HashAlgorithmName.SHA256,32),(byte[])r[1]))throw new BusinessRuleException("Current password is incorrect.");}
        var salt=RandomNumberGenerator.GetBytes(16);var hash=Rfc2898DeriveBytes.Pbkdf2(replacement,salt,600000,HashAlgorithmName.SHA256,32);
        cmd.Parameters.Clear();cmd.CommandText="UPDATE credentials SET salt=$s,hash=$h WHERE username=$u";cmd.Parameters.AddWithValue("$s",salt);cmd.Parameters.AddWithValue("$h",hash);cmd.Parameters.AddWithValue("$u",resetOperator?"operator":session.Username);cmd.ExecuteNonQuery();
        cmd.CommandText="UPDATE account_recovery SET salt=NULL,hash=NULL,failures=0,blocked_until=NULL WHERE username=$u";cmd.ExecuteNonQuery();
        Log(c,t,session.Username,resetOperator?"OperatorPasswordReset":"PasswordChanged");t.Commit();
        foreach(var token in sessions.Where(p=>p.Value.Username==(resetOperator?"operator":session.Username)).Select(p=>p.Key).ToArray())sessions.Remove(token);
    }
    public void RestoreBackup(LoginSession session,string path)
    {
        CheckSession(session,true);var full=Path.GetFullPath(path);var parent=Path.GetFullPath(database.Paths.Backups)+Path.DirectorySeparatorChar;
        if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!File.Exists(full))throw new BusinessRuleException("Choose a backup from this application's Backups folder.");
        using(var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=full,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()))
        {
            source.Open();using var cmd=source.CreateCommand();cmd.CommandText="PRAGMA integrity_check";if(!Equals(cmd.ExecuteScalar(),"ok"))throw new BusinessRuleException("Backup integrity check failed.");
            cmd.CommandText="SELECT MAX(version) FROM schema_migrations";var version=Convert.ToInt32(cmd.ExecuteScalar());if(version is <2 or >5)throw new BusinessRuleException("Backup version is not supported by this application.");
            cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table'";var tables=new HashSet<string>();using(var reader=cmd.ExecuteReader())while(reader.Read())tables.Add(reader.GetString(0));
            var required=new List<string>{"schema_migrations","app_instance","credentials","worksheets","audit_events","parties","invoices","receipts","receipt_allocations"};if(version>=3)required.AddRange(["worksheet_parties","party_worksheets"]);if(version>=4)required.AddRange(["workflow_documents","workflow_audit"]);
            if(required.Any(name=>!tables.Contains(name)))throw new BusinessRuleException("Backup schema is incomplete.");
            if(version>=5&&!tables.Contains("account_recovery"))throw new BusinessRuleException("Backup recovery schema is incomplete.");
            cmd.CommandText="PRAGMA foreign_key_check";using(var reader=cmd.ExecuteReader())if(reader.Read())throw new BusinessRuleException("Backup contains broken relationships.");
            cmd.CommandText="SELECT COUNT(*) FROM credentials";if(Convert.ToInt32(cmd.ExecuteScalar())!=2)throw new BusinessRuleException("Backup credentials are incomplete.");
        }
        database.RestoreVerified(full);
        sessions.Clear();
    }
}
