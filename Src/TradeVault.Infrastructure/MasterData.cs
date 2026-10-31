using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed record PartyMasterEntry(Guid Id,string Name,int Invoices);
public sealed record AgentMasterEntry(string Id,string Name,int Invoices)
{
    public override string ToString()=>Name;
}

public sealed partial class WorkbookStore
{
    internal static string MasterName(string name)=>string.Join(" ",(name??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    internal static string MasterKey(string name)=>MasterName(name).Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    private List<(Guid Party,Dictionary<string,string> Row)> MasterInvoices()
    {
        using var connection=database.Open();using var command=connection.CreateCommand();command.CommandText="SELECT party_id,data FROM party_worksheets WHERE name='form'";
        using var reader=command.ExecuteReader();var result=new List<(Guid,Dictionary<string,string>)>();
        while(reader.Read())foreach(var row in JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(1))!)
            if(!row.ContainsKey("_metadata")&&row.GetValueOrDefault("_blank")!="1")result.Add((Guid.Parse(reader.GetString(0)),row));
        return result;
    }
    public IReadOnlyList<PartyMasterEntry> GetPartyMaster()
    {
        var counts=MasterInvoices().GroupBy(entry=>entry.Party).ToDictionary(group=>group.Key,group=>group.Count());
        return GetParties().Where(p=>!p.IsUnassigned).Select(p=>new PartyMasterEntry(p.Id,p.Name,counts.GetValueOrDefault(p.Id))).ToList();
    }
    public IReadOnlyList<AgentMasterEntry> GetAgents()
    {
        var invoices=MasterInvoices();
        using var connection=database.Open();using var command=connection.CreateCommand();command.CommandText="SELECT id,name FROM worksheet_agents ORDER BY name COLLATE NOCASE";
        using var reader=command.ExecuteReader();var result=new List<AgentMasterEntry>();
        while(reader.Read())
        {
            var id=reader.GetString(0);var name=reader.GetString(1);var key=MasterKey(name);
            result.Add(new(id,name,invoices.Count(entry=>entry.Row.GetValueOrDefault("_agentId")==id||MasterKey(entry.Row.GetValueOrDefault("F27",""))==key)));
        }
        return result;
    }
    public AgentMasterEntry CreateAgent(string name)
    {
        name=MasterName(name);RegisterInvoiceSchema.Required(name,"Agent name",200);var key=MasterKey(name);
        using var connection=database.Open();using var transaction=connection.BeginTransaction();using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT COUNT(*) FROM worksheet_agents WHERE name_key=$key";command.Parameters.AddWithValue("$key",key);
        if(Convert.ToInt32(command.ExecuteScalar())>0)throw new BusinessRuleException("An agent with this name already exists.");
        var id=Guid.NewGuid().ToString();command.CommandText="INSERT INTO worksheet_agents VALUES($id,$name,$key)";command.Parameters.AddWithValue("$id",id);command.Parameters.AddWithValue("$name",name);command.ExecuteNonQuery();
        Log(connection,transaction,id,"AgentCreated:Local workspace");transaction.Commit();return new(id,name,0);
    }
    internal AgentMasterEntry RequireAgent(string name,string? id=null)
    {
        var key=MasterKey(name);var match=GetAgents().SingleOrDefault(agent=>(id==null||agent.Id==id)&&MasterKey(agent.Name)==key);
        return match??throw new BusinessRuleException("Select an existing agent from Agent Master. Create new agents on the Agent Master tab.");
    }
    internal static void UpgradeAgentMaster(SqliteConnection connection,SqliteTransaction transaction)
    {
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="CREATE TABLE IF NOT EXISTS worksheet_agents(id TEXT PRIMARY KEY,name TEXT NOT NULL,name_key TEXT NOT NULL UNIQUE) STRICT";command.ExecuteNonQuery();
        command.CommandText="SELECT id,name FROM worksheet_agents";var agents=new Dictionary<string,string>();
        using(var reader=command.ExecuteReader())while(reader.Read())agents.TryAdd(MasterKey(reader.GetString(1)),reader.GetString(0));
        foreach(var table in new[]{"worksheets","party_worksheets"})
        {
            command.Parameters.Clear();command.CommandText=$"SELECT rowid,data FROM {table} WHERE name='form'";
            var updates=new List<(long Id,List<Dictionary<string,string>> Rows)>();
            using(var reader=command.ExecuteReader())while(reader.Read())updates.Add((reader.GetInt64(0),JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(1))!));
            foreach(var update in updates)
            {
                foreach(var row in update.Rows.Where(row=>!row.ContainsKey("_metadata")&&row.GetValueOrDefault("_blank")!="1"))
                {
                    var name=MasterName(row.GetValueOrDefault("F27",""));if(name.Length==0)continue;var key=MasterKey(name);
                    if(!agents.TryGetValue(key,out var id))
                    {
                        id=Guid.NewGuid().ToString();command.Parameters.Clear();command.CommandText="INSERT INTO worksheet_agents VALUES($id,$name,$key)";command.Parameters.AddWithValue("$id",id);command.Parameters.AddWithValue("$name",name);command.Parameters.AddWithValue("$key",key);command.ExecuteNonQuery();agents.Add(key,id);
                    }
                    row["_agentId"]=id;
                }
                command.Parameters.Clear();command.CommandText=$"UPDATE {table} SET data=$data WHERE rowid=$id";command.Parameters.AddWithValue("$data",JsonSerializer.Serialize(update.Rows));command.Parameters.AddWithValue("$id",update.Id);command.ExecuteNonQuery();
            }
        }
    }
}
