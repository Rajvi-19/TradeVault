using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed record DemoRefreshResult(int RemovedParties,int RemovedAgents,int RemovedInvoices);

public sealed partial class WorkbookStore
{
    private static bool DemoMarked(string? value)
    {
        value=(value??"").Trim();return new[]{"[TEST]","[DEMO]","TEST-","TEST ","DEMO-","QA-","QA ","SYNTHETIC "}.Any(prefix=>value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
    }
    private static bool DemoRow(Dictionary<string,string> row)=>new[]{"F1","F3","F9","B"}.Any(k=>DemoMarked(row.GetValueOrDefault(k)));
    public DemoRefreshResult RefreshDemoData(DateOnly today,bool replaceAll=false)
    {
        var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(database.Paths.Root));var stage=Path.Combine(root,".demo-refresh-"+Guid.NewGuid().ToString("N"));
        try
        {
            using(var staged=new PortableDatabase(new(stage)))BuildFreshDemo(new(staged),today);
            using var connection=database.Open();using var command=connection.CreateCommand();command.CommandText="ATTACH DATABASE $file AS fresh_demo";command.Parameters.AddWithValue("$file",Path.Combine(stage,"Data","tradevault.db"));command.ExecuteNonQuery();command.Parameters.Clear();
            using var transaction=connection.BeginTransaction();command.Transaction=transaction;
            void Execute(string sql,params (string Key,object Value)[] args){command.Parameters.Clear();command.CommandText=sql;foreach(var arg in args)command.Parameters.AddWithValue(arg.Key,arg.Value);command.ExecuteNonQuery();}
            var allParties=0;var allAgents=0;var allInvoices=0;
            if(replaceAll)
            {
                allParties=GetParties().Count(p=>!p.IsUnassigned);allAgents=GetAgents().Count;
                allInvoices=GetParties().Sum(p=>Load("form",p.Id).Rows.Count(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"));
                Execute("DELETE FROM workflow_documents");Execute("DELETE FROM party_worksheets");Execute("DELETE FROM worksheets");
                Execute("DELETE FROM worksheet_parties WHERE is_unassigned=0");Execute("DELETE FROM worksheet_agents");
            }
            var parties=new HashSet<string>();command.CommandText="SELECT id,name FROM worksheet_parties WHERE is_unassigned=0";using(var reader=command.ExecuteReader())while(reader.Read())if(DemoMarked(reader.GetString(1)))parties.Add(reader.GetString(0));
            var agents=new HashSet<string>();command.CommandText="SELECT id,name FROM worksheet_agents";using(var reader=command.ExecuteReader())while(reader.Read())if(DemoMarked(reader.GetString(1)))agents.Add(reader.GetString(0));
            command.CommandText="SELECT party_id,name,revision,data FROM party_worksheets";var sheets=new List<(string Party,string Name,int Revision,List<Dictionary<string,string>> Rows)>();using(var reader=command.ExecuteReader())while(reader.Read())sheets.Add((reader.GetString(0),reader.GetString(1),reader.GetInt32(2),JsonSerializer.Deserialize<List<Dictionary<string,string>>>(reader.GetString(3))!));
            var removedIds=sheets.Where(s=>s.Name=="form").SelectMany(s=>s.Rows.Where(r=>!r.ContainsKey("_metadata")&&(parties.Contains(s.Party)||DemoRow(r))).Select(r=>r.GetValueOrDefault("_id",ReportWorkflow.InvoiceKey(Guid.Parse(s.Party),r)))).ToHashSet();
            var removedInvoices=removedIds.Count;
            command.CommandText="SELECT kind,id,data FROM workflow_documents";var documents=new List<(string Kind,string Id,string Data)>();using(var reader=command.ExecuteReader())while(reader.Read())documents.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));
            foreach(var doc in documents)
            {
                if(parties.Contains(doc.Id)){Execute("DELETE FROM workflow_documents WHERE kind=$k AND id=$id",("$k",doc.Kind),("$id",doc.Id));continue;}
                var node=JsonNode.Parse(doc.Data)!.AsObject();
                if(doc.Kind is "party_transfer_receipts" or "agent_transfer_receipts" or "receipts")
                {
                    var key=doc.Kind=="receipts"?"Receipts":"Entries";if(node[key] is not JsonArray entries)continue;var originalCount=entries.Count;
                    foreach(var item in entries.ToList())
                    {
                        var master=item?["MasterId"]?.GetValue<string>();var name=item?["Name"]?.GetValue<string>();var note=item?["Description"]?.GetValue<string>()??item?["Reference"]?.GetValue<string>();
                        var remove=parties.Contains(master??"")||doc.Kind=="agent_transfer_receipts"&&agents.Contains(master??"")||DemoMarked(name)||DemoMarked(note);
                        if(remove){entries.Remove(item);continue;}
                        var allocations=item?["Allocations"] as JsonArray;
                        if(allocations?.Any(a=>removedIds.Contains(a?["InvoiceId"]?.GetValue<string>()??""))==true)throw new BusinessRuleException("A receipt not clearly marked as test data refers to a test invoice. Nothing was reset; review that receipt first.");
                        if(doc.Kind=="party_transfer_receipts")agents.Remove(item?["ReceivingAgentId"]?.GetValue<string>()??"");
                        if(doc.Kind=="agent_transfer_receipts")agents.Remove(master??"");
                    }
                    if(entries.Count==0)Execute("DELETE FROM workflow_documents WHERE kind=$k AND id=$id",("$k",doc.Kind),("$id",doc.Id));
                    else if(entries.Count!=originalCount)Execute("UPDATE workflow_documents SET data=$data,revision=revision+1 WHERE kind=$k AND id=$id",("$data",node.ToJsonString()),("$k",doc.Kind),("$id",doc.Id));
                }
                else if(doc.Kind=="export"&&parties.Contains(node["PartyId"]?.GetValue<string>()??""))Execute("DELETE FROM workflow_documents WHERE kind=$k AND id=$id",("$k",doc.Kind),("$id",doc.Id));
            }
            foreach(var sheet in sheets)
            {
                if(parties.Contains(sheet.Party))continue;
                var kept=sheet.Rows.Where(r=>!DemoRow(r)).ToList();foreach(var row in kept.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"))agents.Remove(row.GetValueOrDefault("_agentId",""));
                if(kept.Count!=sheet.Rows.Count)Execute("UPDATE party_worksheets SET data=$data,revision=revision+1 WHERE party_id=$party AND name=$name",("$data",JsonSerializer.Serialize(kept)),("$party",sheet.Party),("$name",sheet.Name));
            }
            foreach(var party in parties){Execute("DELETE FROM party_worksheets WHERE party_id=$id",("$id",party));Execute("DELETE FROM worksheet_parties WHERE id=$id",("$id",party));}
            foreach(var agent in agents)Execute("DELETE FROM worksheet_agents WHERE id=$id",("$id",agent));
            // All staged IDs are fresh; existing non-demo masters and their records are retained.
            Execute("INSERT INTO worksheet_parties SELECT * FROM fresh_demo.worksheet_parties WHERE is_unassigned=0");Execute("INSERT INTO worksheet_agents SELECT * FROM fresh_demo.worksheet_agents");Execute("INSERT INTO party_worksheets SELECT * FROM fresh_demo.party_worksheets WHERE party_id<>'00000000-0000-0000-0000-000000000000'");
            command.Parameters.Clear();command.CommandText="SELECT kind,id,data FROM fresh_demo.workflow_documents";var fresh=new List<(string Kind,string Id,string Data)>();using(var reader=command.ExecuteReader())while(reader.Read())fresh.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));
            foreach(var doc in fresh)
            {
                command.Parameters.Clear();command.CommandText="SELECT data FROM workflow_documents WHERE kind=$k AND id=$id";command.Parameters.AddWithValue("$k",doc.Kind);command.Parameters.AddWithValue("$id",doc.Id);var old=command.ExecuteScalar() as string;var data=doc.Data;
                if(old!=null){var book=JsonNode.Parse(old)!;foreach(var entry in JsonNode.Parse(doc.Data)!["Entries"]!.AsArray())book["Entries"]!.AsArray().Add(entry!.DeepClone());data=book.ToJsonString();}
                Execute("INSERT INTO workflow_documents VALUES($k,$id,1,$data) ON CONFLICT(kind,id) DO UPDATE SET data=$data,revision=revision+1",("$k",doc.Kind),("$id",doc.Id),("$data",data));
            }
            var info=JsonSerializer.Serialize(new{ReplaceAll=replaceAll,RemovedParties=parties.Count+allParties,RemovedAgents=agents.Count+allAgents,RemovedInvoices=removedInvoices+allInvoices,AddedInvoices=9,AddedPartyReceipts=4,AddedAgentReceipts=3});
            Execute("INSERT INTO workflow_audit VALUES($id,'Local workspace','DemoDataRefreshed','demo-dataset',NULL,$info,$time)",("$id",Guid.NewGuid().ToString()),("$info",info),("$time",DateTimeOffset.UtcNow.ToString("O")));
            command.Parameters.Clear();command.CommandText="PRAGMA foreign_key_check";using(var reader=command.ExecuteReader())if(reader.Read())throw new BusinessRuleException("Relationship validation failed. No changes were committed.");transaction.Commit();return new(parties.Count+allParties,agents.Count+allAgents,removedInvoices+allInvoices);
        }
        finally
        {
            var full=Path.GetFullPath(stage);if(Path.GetDirectoryName(full)==root&&Path.GetFileName(full).StartsWith(".demo-refresh-",StringComparison.Ordinal)&&Directory.Exists(full))Directory.Delete(full,true);
        }
    }
    private static void BuildFreshDemo(WorkbookStore store,DateOnly today)
    {
        var agents=new[]{store.CreateAgent("[DEMO] Meridian Colour Malaysia"),store.CreateAgent("[DEMO] Straits Dye Trading"),store.CreateAgent("[DEMO] Johor Textile Chemicals")};
        var parties=new[]{store.CreateParty("[DEMO] Faisalabad Textile Processors"),store.CreateParty("[DEMO] Multan Fabric Industries"),store.CreateParty("[DEMO] Chattogram Colour Mills")};
        var scenarios=new[]{(0,0,170,145,18000m,12000m),(0,0,130,105,24000m,16000m),(0,1,180,155,15000m,10000m),(1,1,100,75,12000m,8000m),(1,1,50,25,20000m,13000m),(1,1,25,0,9000m,6000m),(2,2,150,125,10000m,7000m),(2,2,70,45,15000m,10000m),(2,2,20,0,8000m,5000m)};
        var index=0;
        foreach(var (p,a,piDays,blDays,original,contract) in scenarios)
        {
            var number=$"DEMO-PI-{today.Year}-{++index:000}";var pi=today.AddDays(-piDays);var id=store.CreateRegisterInvoice(parties[p].Id,store.Load("form",parties[p].Id).Revision,new(p==2?"Bangladesh":"Pakistan",agents[a].Name,number,pi,agents[a].Id));
            var snapshot=store.Load("form",parties[p].Id);var row=snapshot.Rows.Single(r=>r.GetValueOrDefault("_id")==id);row["F5"]=original.ToString("0.00",CultureInfo.InvariantCulture);row["F6"]=contract.ToString("0.00",CultureInfo.InvariantCulture);row["F7"]=(contract-500).ToString("0.00",CultureInfo.InvariantCulture);row["F9"]=$"DEMO-EXP-{today.Year}-{index:000}";row["F10"]=pi.AddDays(10).ToString("yyyy-MM-dd");row["F11"]=$"DEMO-IND-CONT-{index:000}";row["F12"]=$"DEMO-IND-BL-{index:000}";row["F13"]=pi.AddDays(12).ToString("yyyy-MM-dd");row["F14"]=pi.AddDays(20).ToString("yyyy-MM-dd");row["F15"]=$"DEMO-MYS-CONT-{index:000}";row["F16"]=blDays==0?"":$"DEMO-MYS-BL-{index:000}";row["F17"]=blDays==0?"":today.AddDays(-blDays).ToString("yyyy-MM-dd");if(index==5)row["_dueOverride"]=today.AddDays(80).ToString("yyyy-MM-dd");store.Save("form",snapshot.Revision,snapshot.Rows,parties[p].Id);
        }
        store.AddTransferReceipt(TransferReceiptKind.Party,parties[0].Id.ToString(),today.AddDays(-20),21000,"[DEMO] Bulk remittance: full first invoice, partial second",agents[0].Id,"Sample: 1 USD = 280 PKR");
        store.AddTransferReceipt(TransferReceiptKind.Party,parties[0].Id.ToString(),today.AddDays(-10),1000,"[DEMO] Separate payment through Straits",agents[1].Id,"Sample: 1 USD = 281 PKR");
        store.AddTransferReceipt(TransferReceiptKind.Party,parties[1].Id.ToString(),today.AddDays(-5),5000,"[DEMO] Partial contract payment",agents[1].Id,"Sample: 1 USD = 280 PKR");
        store.AddTransferReceipt(TransferReceiptKind.Party,parties[2].Id.ToString(),today.AddDays(-1),35000,"[DEMO] Three invoices settled with USD 2,000 advance",agents[2].Id,"Sample: 1 USD = 122 BDT");
        store.AddTransferReceipt(TransferReceiptKind.Agent,agents[0].Id,today.AddDays(-15),16000,"[DEMO] Meridian bank remittance to company",usdValue:"Sample: 1 USD = 4.20 MYR");store.AddTransferReceipt(TransferReceiptKind.Agent,agents[1].Id,today.AddDays(-2),4000,"[DEMO] Straits bank remittance to company",usdValue:"Sample: 1 USD = 4.22 MYR");store.AddTransferReceipt(TransferReceiptKind.Agent,agents[2].Id,today,30000,"[DEMO] Johor bank remittance to company",usdValue:"Sample: 1 USD = 4.21 MYR");
        if(store.GetPartyMaster().Sum(p=>p.Invoices)!=9||store.GetTransferReceipts(TransferReceiptKind.Party).Sum(r=>r.UnallocatedUsd)!=2000)throw new BusinessRuleException("Fresh demo data failed reconciliation.");
        foreach(var party in parties)new CompanyReports(store).Build(CompanyReportKind.AccountDetails,party.Id.ToString());
    }
}



