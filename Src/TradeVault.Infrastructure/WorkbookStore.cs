using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed record WorksheetSnapshot(int Revision, List<Dictionary<string, string>> Rows);
public sealed record WorksheetParty(Guid Id,string Name,bool IsUnassigned)
{
    public override string ToString()=>Name;
}

/// <summary>Persistence for independent Excel-shaped worksheets, without authentication.</summary>
public sealed partial class WorkbookStore
{
    private readonly PortableDatabase database;
    public WorkbookStore(PortableDatabase database) => this.database = database;
    public IReadOnlyList<WorksheetParty> GetParties()
    {
        ValidateSheet("form");
        using var c=database.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT id,name,is_unassigned FROM worksheet_parties ORDER BY is_unassigned DESC,name COLLATE NOCASE";
        using var reader=cmd.ExecuteReader();var parties=new List<WorksheetParty>();
        while(reader.Read())parties.Add(new(Guid.Parse(reader.GetString(0)),reader.GetString(1),reader.GetInt32(2)==1));
        return parties;
    }
    public WorksheetParty CreateParty(string name)
    {
        ValidateSheet("form");
        name=string.Join(" ",(name??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
        if(name.Length is <1 or >200)throw new BusinessRuleException("Party name must contain 1–200 characters.");
        var key=name.Normalize(System.Text.NormalizationForm.FormKC).ToUpperInvariant();
        using var c=database.Open();using var transaction=c.BeginTransaction();using var cmd=c.CreateCommand();cmd.Transaction=transaction;
        cmd.CommandText="SELECT COUNT(*) FROM worksheet_parties WHERE name_key=$key";cmd.Parameters.AddWithValue("$key",key);
        if(Convert.ToInt32(cmd.ExecuteScalar())>0)throw new BusinessRuleException("A party with this name already exists.");
        var id=Guid.NewGuid();cmd.CommandText="INSERT INTO worksheet_parties VALUES($id,$name,$key,0)";
        cmd.Parameters.AddWithValue("$id",id.ToString());cmd.Parameters.AddWithValue("$name",name);cmd.ExecuteNonQuery();
        Log(c,transaction,id.ToString(),$"PartyCreated:{"Local workspace"}");transaction.Commit();return new(id,name,false);
    }
    private static void EnsureParty(SqliteConnection c,SqliteTransaction? transaction,Guid partyId)
    {
        using var cmd=c.CreateCommand();cmd.Transaction=transaction;
        cmd.CommandText="SELECT COUNT(*) FROM worksheet_parties WHERE id=$id";cmd.Parameters.AddWithValue("$id",partyId.ToString());
        if(Convert.ToInt32(cmd.ExecuteScalar())!=1)throw new BusinessRuleException("Select an existing party before accessing records.");
    }
    private static void Log(SqliteConnection c,SqliteTransaction transaction,string entity,string action)
    {
        using var cmd=c.CreateCommand();cmd.Transaction=transaction;cmd.CommandText="INSERT INTO audit_events VALUES($id,$entity,$action,$now)";
        cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$entity",entity);cmd.Parameters.AddWithValue("$action",action);cmd.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();
    }
    private static void ValidateSheet(string name)
    {
        if(name is not ("form" or "details" or "ledger"))throw new BusinessRuleException("Unknown worksheet.");
    }
    public WorksheetSnapshot Load(string name,Guid? partyId=null)
    {
        ValidateSheet(name);
        using var c = database.Open(); using var cmd = c.CreateCommand();
        var party=partyId??Guid.Empty;EnsureParty(c,null,party);
        cmd.CommandText = "SELECT revision,data FROM party_worksheets WHERE name=$name AND party_id=$party"; cmd.Parameters.AddWithValue("$name", name);cmd.Parameters.AddWithValue("$party",party.ToString());
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? new(reader.GetInt32(0), JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(1))!) : new(0, []);
    }
    public int Save(string name, int expectedRevision, List<Dictionary<string,string>> rows,Guid? partyId=null)
    {
        ValidateSheet(name);
        if(name=="form")GuardRegister(partyId??Guid.Empty,rows);
        using var c = database.Open(); using var t = c.BeginTransaction();
        var party=partyId??Guid.Empty;EnsureParty(c,t,party);
        using var cmd = c.CreateCommand(); cmd.Transaction = t;
        cmd.CommandText = "SELECT COALESCE((SELECT revision FROM party_worksheets WHERE name=$name AND party_id=$party),0)";
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$party",party.ToString());
        if (Convert.ToInt32(cmd.ExecuteScalar()) != expectedRevision) throw new BusinessRuleException("Records changed. Reload before saving.");
        cmd.CommandText="SELECT data FROM party_worksheets WHERE name=$name AND party_id=$party";var before=cmd.ExecuteScalar() as string;var after=JsonSerializer.Serialize(rows);
        cmd.CommandText = "INSERT INTO party_worksheets VALUES($party,$name,$revision,$data) ON CONFLICT(party_id,name) DO UPDATE SET revision=$revision,data=$data";
        cmd.Parameters.AddWithValue("$revision", checked(expectedRevision + 1)); cmd.Parameters.AddWithValue("$data", after); cmd.ExecuteNonQuery();
        cmd.Parameters.Clear(); cmd.CommandText = "INSERT INTO audit_events VALUES($id,$entity,$action,$now)";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString()); cmd.Parameters.AddWithValue("$entity", party+":"+name);
        cmd.Parameters.AddWithValue("$action", $"WorksheetSaved:{"Local workspace"}"); cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); cmd.ExecuteNonQuery();
        cmd.Parameters.Clear();cmd.CommandText="INSERT INTO workflow_audit VALUES($id,$actor,'WorksheetSaved',$entity,$before,$after,$time)";
        cmd.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());cmd.Parameters.AddWithValue("$actor","Local workspace");cmd.Parameters.AddWithValue("$entity",party+":"+name);cmd.Parameters.AddWithValue("$before",(object?)before??DBNull.Value);cmd.Parameters.AddWithValue("$after",after);cmd.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O"));cmd.ExecuteNonQuery();
        t.Commit(); return expectedRevision + 1;
    }
    /// <summary>Move a whole legacy sheet only to an empty destination; never infer row ownership.</summary>
    public void AssignUnassigned(string name,Guid destination,int expectedRevision)
    {
        ValidateSheet(name);
        if(destination==Guid.Empty)throw new BusinessRuleException("Choose a named destination party.");
        using var c=database.Open();using var t=c.BeginTransaction();EnsureParty(c,t,destination);
        using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText="SELECT revision FROM party_worksheets WHERE party_id=$empty AND name=$name";
        cmd.Parameters.AddWithValue("$empty",Guid.Empty.ToString());cmd.Parameters.AddWithValue("$name",name);
        var revision=cmd.ExecuteScalar();
        if(revision==null||Convert.ToInt32(revision)!=expectedRevision)throw new BusinessRuleException("Unassigned records changed. Reload before assigning.");
        cmd.CommandText="SELECT COUNT(*) FROM party_worksheets WHERE party_id=$target AND name=$name";cmd.Parameters.AddWithValue("$target",destination.ToString());
        if(Convert.ToInt32(cmd.ExecuteScalar())>0)throw new BusinessRuleException("The destination already has a saved worksheet. Assignment cannot overwrite it.");
        if(name=="form")
        {
            cmd.CommandText="SELECT name FROM worksheet_parties WHERE id=$target";
            var destinationName=cmd.ExecuteScalar()!.ToString()!;
            cmd.CommandText="SELECT data FROM party_worksheets WHERE party_id=$empty AND name=$name";
            var source=JsonSerializer.Deserialize<List<Dictionary<string,string>>>(cmd.ExecuteScalar()!.ToString()!)!;
            if(source.Where(row=>!row.ContainsKey("_metadata")&&row.GetValueOrDefault("_blank")!="1").Any(row=>!string.Equals(row.GetValueOrDefault("F1","").Trim(),destinationName,StringComparison.OrdinalIgnoreCase)))
                throw new BusinessRuleException("Invoice rows contain a different or missing party name. Keep mixed records unassigned; assignment never guesses ownership.");
        }
        cmd.CommandText="""
            INSERT INTO party_worksheets SELECT $target,name,revision,data FROM party_worksheets WHERE party_id=$empty AND name=$name;
            UPDATE party_worksheets SET revision=revision+1,data='[]' WHERE party_id=$empty AND name=$name;
            """;
        cmd.ExecuteNonQuery();Log(c,t,destination+":"+name,$"LegacyWorksheetAssigned:{"Local workspace"}");t.Commit();
    }
}
