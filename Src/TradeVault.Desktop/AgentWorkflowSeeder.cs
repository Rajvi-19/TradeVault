using System.Globalization;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

/// <summary>Explicit, repeatable fictional workflow fixtures; never executed by normal startup.</summary>
internal static class AgentWorkflowSeeder
{
    internal static void Run(WorkbookStore store,PortableDatabase database)
    {
        var backup=database.CreateBackup();var invoices=0;var receipts=0;
        var samples=new[]{
            (Agent:"[TEST] Meridian Trading Malaysia",Buyer:"[TEST] Karachi Textile Colours",Country:"Pakistan",Original:18000m,Customs:12000m),
            (Agent:"[TEST] Pacific Colour Logistics",Buyer:"[TEST] Lahore Fabric Processors",Country:"Pakistan",Original:24000m,Customs:16000m),
            (Agent:"[TEST] Straits Export Partners",Buyer:"[TEST] Dhaka Dyeing Mills",Country:"Bangladesh",Original:15000m,Customs:10000m)};
        for(var i=0;i<samples.Length;i++)
        {
            var sample=samples[i];var agent=store.GetAgents().FirstOrDefault(a=>a.Name==sample.Agent)??store.CreateAgent(sample.Agent);
            var party=store.GetParties().FirstOrDefault(p=>p.Name==sample.Buyer)??store.CreateParty(sample.Buyer);
            for(var j=0;j<2;j++)
            {
                var number=$"TEST-AGFLOW-{i+1}-{j+1}";var snapshot=store.Load("form",party.Id);
                if(snapshot.Rows.Any(row=>row.GetValueOrDefault("F3")==number))continue;
                var day=j==0?new DateOnly(2026,6,1):new DateOnly(2026,9,1);
                var id=store.CreateRegisterInvoice(party.Id,snapshot.Revision,new(sample.Country,agent.Name,number,day.AddDays(-7),agent.Id));
                snapshot=store.Load("form",party.Id);var row=snapshot.Rows.Single(row=>row.GetValueOrDefault("_id")==id);
                row["F5"]=sample.Original.ToString("0.00",CultureInfo.InvariantCulture);row["F6"]=sample.Customs.ToString("0.00",CultureInfo.InvariantCulture);row["F7"]=(sample.Customs-500).ToString("0.00",CultureInfo.InvariantCulture);
                row["F9"]=number+"-IN";row["F10"]=day.ToString("yyyy-MM-dd");row["F11"]=$"TEST-IN-CONT-{i+1}{j+1}";row["F12"]=$"TEST-IN-BL-{i+1}{j+1}";row["F13"]=day.AddDays(2).ToString("yyyy-MM-dd");row["F14"]=day.AddDays(12).ToString("yyyy-MM-dd");row["F15"]=$"TEST-MY-CONT-{i+1}{j+1}";row["F16"]=$"TEST-MY-BL-{i+1}{j+1}";row["F17"]=day.AddDays(19).ToString("yyyy-MM-dd");
                store.Save("form",snapshot.Revision,snapshot.Rows,party.Id);invoices++;
            }
            var partyNote=$"[TEST AGFLOW {i+1}] {sample.Buyer} payment to {agent.Name}; fictional shipment payment";
            if(!store.GetTransferReceipts(TransferReceiptKind.Party).Any(r=>r.MasterId==party.Id.ToString()&&r.Description==partyNote))
            {store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,10,20),sample.Customs,partyNote,agent.Id);receipts++;}
            var agentNote=$"[TEST AGFLOW {i+1}] {agent.Name} remittance to company; fictional bank transfer MY-{i+1:000}";
            if(!store.GetTransferReceipts(TransferReceiptKind.Agent).Any(r=>r.MasterId==agent.Id&&r.Description==agentNote))
            {store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,10,25),sample.Customs-2000,agentNote);receipts++;}
        }
        Console.WriteLine($"Agent workflow samples: {invoices} invoices, {receipts} receipts added. Backup: {backup}");
    }
    internal static void Check(WorkbookStore store,PortableDatabase database)
    {
        Run(store,database);var invoices=store.GetParties().Sum(p=>store.Load("form",p.Id).Rows.Count(r=>r.GetValueOrDefault("F3","").StartsWith("TEST-AGFLOW-")));
        var partyReceipts=store.GetTransferReceipts(TransferReceiptKind.Party).Count;var agentReceipts=store.GetTransferReceipts(TransferReceiptKind.Agent).Count;
        Run(store,database);
        if(invoices!=6||store.GetParties().Sum(p=>store.Load("form",p.Id).Rows.Count(r=>r.GetValueOrDefault("F3","").StartsWith("TEST-AGFLOW-")))!=6||store.GetTransferReceipts(TransferReceiptKind.Party).Count!=partyReceipts||store.GetTransferReceipts(TransferReceiptKind.Agent).Count!=agentReceipts)throw new InvalidOperationException("Agent sample repeatability failed.");
        foreach(var agent in store.GetAgents().Where(a=>a.Name.StartsWith("[TEST]")))
            if(agent.Invoices<2||!store.GetTransferReceipts(TransferReceiptKind.Agent).Any(r=>r.MasterId==agent.Id))throw new InvalidOperationException("Agent fixtures are missing invoice or receipt references.");
    }
}
