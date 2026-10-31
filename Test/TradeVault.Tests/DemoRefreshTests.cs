using System.Text.Json;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class DemoRefreshTests
{
    [Fact]public void ExplicitFullResetRemovesUnmarkedUserRecordsAndSeedsAgentAllocations()
    {
        using var db=new PortableDatabase(new(Path.Combine(AppContext.BaseDirectory,"DemoRefreshTests",Guid.NewGuid().ToString("N"))));var store=new WorkbookStore(db);
        var party=store.CreateParty("User entered buyer");var agent=store.CreateAgent("User entered agent");store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"USER-1",new(2026,1,1),agent.Id));store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,2,1),50,"User receipt");
        var result=store.RefreshDemoData(new(2026,10,31),true);Assert.Equal(1,result.RemovedParties);Assert.Equal(1,result.RemovedAgents);Assert.Equal(1,result.RemovedInvoices);
        Assert.Equal(3,store.GetParties().Count(p=>!p.IsUnassigned));Assert.Equal(3,store.GetAgents().Count);Assert.DoesNotContain(store.GetParties(),p=>p.Id==party.Id);Assert.Equal(9,store.GetPartyMaster().Sum(p=>p.Invoices));Assert.Equal(4,store.GetTransferReceipts(TransferReceiptKind.Party).Count);Assert.Equal(3,store.GetTransferReceipts(TransferReceiptKind.Agent).Count);Assert.Equal(50000,store.GetTransferReceipts(TransferReceiptKind.Agent).Sum(r=>r.AllocatedUsd));
        Assert.Equal(50000,store.GetParties().Where(p=>!p.IsUnassigned).SelectMany(p=>new CompanyReports(store).Build(CompanyReportKind.AccountDetails,p.Id.ToString()).Table.Rows).Sum(r=>decimal.Parse(r["O"],System.Globalization.CultureInfo.InvariantCulture)));
        store.RefreshDemoData(new(2026,10,31),true);Assert.Equal(9,store.GetPartyMaster().Sum(p=>p.Invoices));Assert.Empty(Directory.GetDirectories(db.Paths.Root,".demo-refresh-*"));
    }
    [Fact]public void AmbiguousFinancialLinksAbortEntireReset()
    {
        using var db=new PortableDatabase(new(Path.Combine(AppContext.BaseDirectory,"DemoRefreshTests",Guid.NewGuid().ToString("N"))));var store=new WorkbookStore(db);var party=store.CreateParty("Actual company");var agent=store.CreateAgent("Actual agent");var id=store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"TEST-QUESTIONABLE",new(2026,5,1),agent.Id));var sheet=store.Load("form",party.Id);sheet.Rows[0]["F5"]="1000";sheet.Rows[0]["F6"]="600";sheet.Rows[0]["F9"]="TEST-QUESTIONABLE-IN";sheet.Rows[0]["F10"]="2026-05-02";store.Save("form",sheet.Revision,sheet.Rows,party.Id);new ReportWorkflow(store).RecordReceipt(party.Id,new(2026,6,1),500,"Unmarked bank payment",[new(id,"Bank",500)]);
        var before=JsonSerializer.Serialize(store.Load("form",party.Id));Assert.Throws<TradeVault.Domain.BusinessRuleException>(()=>store.RefreshDemoData(new(2026,10,31)));Assert.Equal(before,JsonSerializer.Serialize(store.Load("form",party.Id)));Assert.DoesNotContain(store.GetParties(),p=>p.Name.StartsWith("[DEMO]"));Assert.Single(new ReportWorkflow(store).Receipts(party.Id));
    }
    [Fact]public void ReplacesMarkedFixturesPreservesRealRecordsAndReconcilesNewWorkflow()
    {
        using var db=new PortableDatabase(new(Path.Combine(AppContext.BaseDirectory,"DemoRefreshTests",Guid.NewGuid().ToString("N"))));var store=new WorkbookStore(db);var party=store.CreateParty("Preserved company");var agent=store.CreateAgent("Preserved intermediary");
        store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"REAL-001",new(2026,5,1),agent.Id));store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,6,1),100,"Genuine advance",agent.Id,"User text");
        var before=JsonSerializer.Serialize(store.Load("form",party.Id));var receiptBefore=JsonSerializer.Serialize(store.GetTransferReceipts(TransferReceiptKind.Party).Single());
        var copied=store.CreateParty("Unmarked buyer");store.CreateRegisterInvoice(copied.Id,0,new("Pakistan",agent.Name,"TEST-COPIED",new(2026,5,1),agent.Id));
        var old=store.CreateParty("[TEST] Old buyer");var oldAgent=store.CreateAgent("[TEST] Old agent");store.CreateRegisterInvoice(old.Id,0,new("Pakistan",oldAgent.Name,"TEST-OLD",new(2026,5,1),oldAgent.Id));store.AddTransferReceipt(TransferReceiptKind.Party,old.Id.ToString(),new(2026,6,1),500,"Old dummy receipt",oldAgent.Id);
        var result=store.RefreshDemoData(new(2026,10,31));Assert.Equal(2,result.RemovedInvoices);Assert.Equal(before,JsonSerializer.Serialize(store.Load("form",party.Id)));Assert.Equal(receiptBefore,JsonSerializer.Serialize(store.GetTransferReceipts(TransferReceiptKind.Party).Single(r=>r.MasterId==party.Id.ToString())));
        Assert.DoesNotContain(store.GetParties(),p=>p.Name==old.Name);Assert.DoesNotContain(store.GetAgents(),a=>a.Name==oldAgent.Name);Assert.Contains(store.GetParties(),p=>p.Id==copied.Id);Assert.DoesNotContain(store.Load("form",copied.Id).Rows,r=>!r.ContainsKey("_metadata"));
        var demos=store.GetParties().Where(p=>p.Name.StartsWith("[DEMO]")).ToList();Assert.Equal(3,demos.Count);Assert.Equal(9,demos.Sum(p=>store.GetPartyMaster().Single(m=>m.Id==p.Id).Invoices));Assert.Equal(3,store.GetAgents().Count(a=>a.Name.StartsWith("[DEMO]")));
        Assert.Equal(2000,store.GetTransferReceipts(TransferReceiptKind.Party).Where(r=>r.Name.StartsWith("[DEMO]")).Sum(r=>r.UnallocatedUsd));
        var reports=new CompanyReports(store);Assert.Equal("USD 35000.00",reports.Build(CompanyReportKind.PartyLedger,demos.Single(p=>p.Name.Contains("Faisalabad")).Id.ToString()).Summary["Net recorded balance"]);Assert.Equal("USD 36000.00",reports.Build(CompanyReportKind.PartyLedger,demos.Single(p=>p.Name.Contains("Multan")).Id.ToString()).Summary["Net recorded balance"]);Assert.Equal("USD -2000.00",reports.Build(CompanyReportKind.PartyLedger,demos.Single(p=>p.Name.Contains("Chattogram")).Id.ToString()).Summary["Net recorded balance"]);
        var reminders=demos.SelectMany(p=>new ReportWorkflow(store).Reminders(p.Id,new(2026,10,31))).ToList();Assert.Contains(reminders,r=>r.Status=="Settled");Assert.Contains(reminders,r=>r.Status=="Overdue");Assert.Contains(reminders,r=>r.Status=="Due within 30 days");Assert.Contains(reminders,r=>r.Status=="BL pending");
        store.RefreshDemoData(new(2026,10,31));Assert.Equal(9,store.GetPartyMaster().Where(p=>p.Name.StartsWith("[DEMO]")).Sum(p=>p.Invoices));Assert.Equal(before,JsonSerializer.Serialize(store.Load("form",party.Id)));Assert.Empty(Directory.GetDirectories(db.Paths.Root,".demo-refresh-*"));
    }
}


