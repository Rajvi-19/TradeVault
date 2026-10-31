using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TradeVault.Infrastructure;

public sealed record AgentTestDataResult(int AgentsCreated,int InvoicesAssigned);

public sealed partial class WorkbookStore
{
    /// <summary>Explicit test-data maintenance; never runs during normal startup or invoice Save.</summary>
    public AgentTestDataResult SeedAgentTestData()
    {
        
        using var connection=database.Open();using var transaction=connection.BeginTransaction();using var command=connection.CreateCommand();command.Transaction=transaction;
        var agents=new List<(string Id,string Name)>();var created=0;
        foreach(var name in new[]{"[TEST] Meridian Trading Malaysia","[TEST] Pacific Colour Logistics","[TEST] Straits Export Partners"})
        {
            command.Parameters.Clear();command.CommandText="SELECT id FROM worksheet_agents WHERE name_key=$key";command.Parameters.AddWithValue("$key",MasterKey(name));var id=command.ExecuteScalar() as string;
            if(id==null)
            {
                id=Guid.NewGuid().ToString();command.CommandText="INSERT INTO worksheet_agents VALUES($id,$name,$key)";command.Parameters.AddWithValue("$id",id);command.Parameters.AddWithValue("$name",name);command.ExecuteNonQuery();created++;Log(connection,transaction,id,"TestAgentCreated:Local workspace");
            }
            agents.Add((id,name));
        }
        command.Parameters.Clear();command.CommandText="SELECT party_id,revision,data FROM party_worksheets WHERE name='form' ORDER BY party_id";
        var sheets=new List<(string Party,int Revision,string Data)>();using(var reader=command.ExecuteReader())while(reader.Read())sheets.Add((reader.GetString(0),reader.GetInt32(1),reader.GetString(2)));
        var assigned=0;
        foreach(var sheet in sheets)
        {
            var rows=JsonSerializer.Deserialize<List<Dictionary<string,string>>>(sheet.Data)!;var changed=false;
            foreach(var row in rows.Where(row=>!row.ContainsKey("_metadata")&&row.GetValueOrDefault("_blank")!="1"&&string.IsNullOrWhiteSpace(row.GetValueOrDefault("F27"))))
            {
                var agent=agents[assigned%agents.Count];row["F27"]=agent.Name;row["_agentId"]=agent.Id;assigned++;changed=true;
            }
            if(!changed)continue;
            var after=JsonSerializer.Serialize(rows);command.Parameters.Clear();command.CommandText="UPDATE party_worksheets SET data=$data,revision=$revision WHERE party_id=$party AND name='form'";command.Parameters.AddWithValue("$data",after);command.Parameters.AddWithValue("$revision",checked(sheet.Revision+1));command.Parameters.AddWithValue("$party",sheet.Party);command.ExecuteNonQuery();
            Log(connection,transaction,sheet.Party,"TestAgentsAssignedToBlankInvoices:Local workspace");
            command.Parameters.Clear();command.CommandText="INSERT INTO workflow_audit VALUES($id,'Local workspace','TestAgentsAssigned',$entity,$before,$after,$time)";command.Parameters.AddWithValue("$id",Guid.NewGuid().ToString());command.Parameters.AddWithValue("$entity",sheet.Party+":form");command.Parameters.AddWithValue("$before",sheet.Data);command.Parameters.AddWithValue("$after",after);command.Parameters.AddWithValue("$time",DateTimeOffset.UtcNow.ToString("O"));command.ExecuteNonQuery();
        }
        transaction.Commit();return new(created,assigned);
    }
}
