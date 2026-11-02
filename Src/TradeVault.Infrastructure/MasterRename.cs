using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class WorkbookStore
{
    public void RenameParty(Guid id,string name)=>RenameMaster(false,id.ToString(),name);
    public void RenameAgent(string id,string name)=>RenameMaster(true,id,name);
    private void RenameMaster(bool agent,string id,string name)
    {
        name=MasterName(name);RegisterInvoiceSchema.Required(name,agent?"Agent name":"Party name",200);
        using var connection=database.Open();using var transaction=connection.BeginTransaction();using var cmd=connection.CreateCommand();cmd.Transaction=transaction;
        var table=agent?"worksheet_agents":"worksheet_parties";
        void Execute(string sql,params (string,object)[] values){cmd.Parameters.Clear();cmd.CommandText=sql;foreach(var (key,value) in values)cmd.Parameters.AddWithValue(key,value);cmd.ExecuteNonQuery();}
        cmd.CommandText=$"SELECT name FROM {table} WHERE id=$id"+(agent?"":" AND is_unassigned=0");cmd.Parameters.AddWithValue("$id",id);var before=cmd.ExecuteScalar() as string??throw new BusinessRuleException("Master record not found. Reload the page.");
        cmd.Parameters.Clear();cmd.CommandText=$"SELECT COUNT(*) FROM {table} WHERE name_key=$key AND id<>$id";cmd.Parameters.AddWithValue("$key",MasterKey(name));cmd.Parameters.AddWithValue("$id",id);if(Convert.ToInt32(cmd.ExecuteScalar())>0)throw new BusinessRuleException("That name already exists. Choose a different name.");
        if(before==name)return;
        Execute($"UPDATE {table} SET name=$name,name_key=$key WHERE id=$id",("$name",name),("$key",MasterKey(name)),("$id",id));
        foreach(var sheets in new[]{"party_worksheets","worksheets"})
        {
            cmd.Parameters.Clear();cmd.CommandText=$"SELECT rowid,{(sheets=="party_worksheets"?"party_id":"''")},data FROM {sheets}";
            var updates=new List<(long Id,string Party,List<Dictionary<string,string>> Rows)>();using(var reader=cmd.ExecuteReader())while(reader.Read())updates.Add((reader.GetInt64(0),reader.GetString(1),JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(2))!));
            foreach(var update in updates)
            {
                var changed=false;
                foreach(var row in update.Rows.Where(r=>!r.ContainsKey("_metadata")))
                {
                    if(agent&&(row.GetValueOrDefault("_agentId")==id||string.IsNullOrEmpty(row.GetValueOrDefault("_agentId"))&&MasterKey(row.GetValueOrDefault("F27",""))==MasterKey(before))){row["F27"]=name;changed=true;}
                    if(!agent&&update.Party==id&&row.ContainsKey("F1")){row["F1"]=name;changed=true;}
                }
                if(changed)Execute($"UPDATE {sheets} SET data=$data,revision=revision+1 WHERE rowid=$row",("$data",JsonSerializer.Serialize(update.Rows)),("$row",update.Id));
            }
        }
        var kind=agent?"agent_transfer_receipts":"party_transfer_receipts";
        cmd.Parameters.Clear();cmd.CommandText="SELECT data FROM workflow_documents WHERE kind=$kind AND id='all'";cmd.Parameters.AddWithValue("$kind",kind);
        if(cmd.ExecuteScalar() is string data)
        {
            var book=JsonSerializer.Deserialize<TransferReceiptBook>(data)!;var entries=book.Entries.Select(r=>r.MasterId==id?r with{Name=name}:r).ToList();
            Execute("UPDATE workflow_documents SET data=$data,revision=revision+1 WHERE kind=$kind AND id='all'",("$data",JsonSerializer.Serialize(new TransferReceiptBook(entries))),("$kind",kind));
        }
        Log(connection,transaction,id,$"{(agent?"Agent":"Party")}Renamed:{before} -> {name}");transaction.Commit();
    }
}
