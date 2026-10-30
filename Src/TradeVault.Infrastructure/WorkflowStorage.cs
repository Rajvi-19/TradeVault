using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class WorkbookStore
{
    private void GuardRegister(Guid party,List<Dictionary<string,string>> candidate)
    {
        var rows=candidate.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1").ToList();
        if(rows.Where(r=>!string.IsNullOrWhiteSpace(r.GetValueOrDefault("_id"))).GroupBy(r=>r["_id"],StringComparer.Ordinal).Any(g=>g.Count()>1))throw new BusinessRuleException("Duplicate internal invoice identifiers. Create a new row instead of copying internal identifiers.");
        for(int i=0;i<rows.Count;i++)
        {
            var issues=InvoiceRules.Validate(rows[i]);
            if(issues.Count>0)throw new BusinessRuleException($"Row {i+1}: "+string.Join("\n",issues.Select(issue=>issue.Message)));
        }
        if(rows.Where(r=>!string.IsNullOrWhiteSpace(r.GetValueOrDefault("F9"))).GroupBy(r=>r["F9"].Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new BusinessRuleException("Duplicate invoice reference for this party.");
        var receipts=Document<TradeVault.Application.ReceiptBook>("receipts",party.ToString())?.Receipts??[];
        if(receipts.Count==0)return;
        var old=Load("form",party).Rows.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1").ToList();
        foreach(var id in receipts.SelectMany(r=>r.Allocations).Select(a=>a.InvoiceId).Distinct())
        {
            var previous=old.Single(r=>ReportWorkflow.InvoiceKey(party,r)==id);var next=rows.SingleOrDefault(r=>ReportWorkflow.InvoiceKey(party,r)==id)??throw new BusinessRuleException("Invoices with receipt history cannot be deleted.");
            foreach(var field in new[]{"F5","F6","F7","F22","F23","F25","F24","F26","F18","F10"})
                if(previous.GetValueOrDefault(field,"")!=next.GetValueOrDefault(field,""))throw new BusinessRuleException("Invoice amounts/dates with receipt history are protected. Use a documented correction workflow rather than changing historical inputs.");
        }
    }
    internal PortableDatabase Database=>database;
    public int DeleteWorksheetRecord(string name,Guid party,int expectedRevision,int rowIndex,string? invoiceId)
    {
        var snapshot=Load(name,party);
        if(snapshot.Revision!=expectedRevision)throw new TradeVault.Domain.BusinessRuleException("Records changed. Reload before deleting; nothing was deleted.");
        var records=snapshot.Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
        var target=name=="form"?records.SingleOrDefault(r=>ReportWorkflow.InvoiceKey(party,r)==invoiceId):records.ElementAtOrDefault(rowIndex);
        if(target==null||target.GetValueOrDefault("_blank")=="1")return snapshot.Revision; // Unsaved draft: there is no stored record to delete.
        snapshot.Rows[snapshot.Rows.IndexOf(target)]=new(){{"_blank","1"}};return Save(name,snapshot.Revision,snapshot.Rows,party);
    }
    public int RestoreWorksheetRecord(string name,Guid party,int expectedRevision,int rowIndex,Dictionary<string,string> record)
    {
        var snapshot=Load(name,party);
        if(snapshot.Revision!=expectedRevision)throw new BusinessRuleException("Records changed. Reload before restoring; nothing was restored.");
        if(record.ContainsKey("_metadata")||record.GetValueOrDefault("_blank")=="1")throw new BusinessRuleException("Only worksheet records can be restored.");
        var records=snapshot.Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
        if(name=="form"&&records.Any(r=>ReportWorkflow.InvoiceKey(party,r)==ReportWorkflow.InvoiceKey(party,record)))throw new BusinessRuleException("This invoice already exists. Nothing was restored.");
        var index=Math.Clamp(rowIndex,0,records.Count);
        if(index<records.Count&&records[index].GetValueOrDefault("_blank")=="1")snapshot.Rows[snapshot.Rows.IndexOf(records[index])]=new(record);
        else snapshot.Rows.Insert(index<records.Count?snapshot.Rows.IndexOf(records[index]):snapshot.Rows.Count,new(record));
        return Save(name,snapshot.Revision,snapshot.Rows,party);
    }
    internal T? Document<T>(string kind,string id)
    {
        using var c=database.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT data FROM workflow_documents WHERE kind=$k AND id=$i";cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",id);
        return cmd.ExecuteScalar() is string json?JsonSerializer.Deserialize<T>(json):default;
    }
    internal T Mutate<T>(string kind,string id,Func<T?,T> transform)
    {
        using var c=database.Open();using var t=c.BeginTransaction(deferred:false);using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText="SELECT data FROM workflow_documents WHERE kind=$k AND id=$i";cmd.Parameters.AddWithValue("$k",kind);cmd.Parameters.AddWithValue("$i",id);
        var before=cmd.ExecuteScalar() as string;var next=transform(before==null?default:JsonSerializer.Deserialize<T>(before));var after=JsonSerializer.Serialize(next);
        cmd.CommandText="INSERT INTO workflow_documents VALUES($k,$i,1,$d) ON CONFLICT(kind,id) DO UPDATE SET revision=revision+1,data=$d";cmd.Parameters.AddWithValue("$d",after);cmd.ExecuteNonQuery();
        cmd.Parameters.Clear();cmd.CommandText="INSERT INTO workflow_audit VALUES($id,$actor,$action,$entity,$before,$after,$time)";
        cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$actor","Local workspace");cmd.Parameters.AddWithValue("$action",kind);cmd.Parameters.AddWithValue("$entity",id);cmd.Parameters.AddWithValue("$before",(object?)before??DBNull.Value);cmd.Parameters.AddWithValue("$after",after);cmd.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();t.Commit();return next;
    }
    public string CreateBackup(){return database.CreateBackup();}
    public IReadOnlyList<string> Backups(){return Directory.GetFiles(database.Paths.Backups,"*.db").OrderDescending().ToArray();}
    public void RestoreBackup(string path)
    {
        var full=Path.GetFullPath(path);var parent=Path.GetFullPath(database.Paths.Backups)+Path.DirectorySeparatorChar;
        if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!File.Exists(full))throw new BusinessRuleException("Choose a backup from this application's Backups folder.");
        using(var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=full,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()))
        {
            source.Open();using var cmd=source.CreateCommand();cmd.CommandText="PRAGMA integrity_check";if(!Equals(cmd.ExecuteScalar(),"ok"))throw new BusinessRuleException("Backup integrity check failed.");
            cmd.CommandText="SELECT MAX(version) FROM schema_migrations";var version=Convert.ToInt32(cmd.ExecuteScalar());if(version is <2 or >9)throw new BusinessRuleException("Backup version is not supported by this application.");
            cmd.CommandText="SELECT name FROM sqlite_master WHERE type='table'";var tables=new HashSet<string>();using(var reader=cmd.ExecuteReader())while(reader.Read())tables.Add(reader.GetString(0));
            var required=new List<string>{"schema_migrations","app_instance","worksheets","audit_events","parties","invoices","receipts","receipt_allocations"};if(version>=3)required.AddRange(["worksheet_parties","party_worksheets"]);if(version>=4)required.AddRange(["workflow_documents","workflow_audit"]);
            if(required.Any(name=>!tables.Contains(name)))throw new BusinessRuleException("Backup schema is incomplete.");
            cmd.CommandText="PRAGMA foreign_key_check";using(var reader=cmd.ExecuteReader())if(reader.Read())throw new BusinessRuleException("Backup contains broken relationships.");
        }
        database.RestoreVerified(full);
    }
}
