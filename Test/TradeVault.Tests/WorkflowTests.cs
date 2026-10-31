using System.IO.Compression;
using System.Text;
using System.Text.Json;
using TradeVault.Application;
using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class WorkflowTests
{
    [Fact]public void DeletedRecordIsRemovedAndStaysRemovedAfterReopen()
    {
        using var f=new Fixture();var snapshot=f.Store.Load("form",f.Party);
        snapshot.Rows.Add(Fixture.Invoice("INV-003","three"));snapshot.Rows.Add(Fixture.Invoice("INV-004","four"));
        var saved=f.Store.Save("form",snapshot.Revision,snapshot.Rows,f.Party);
        var cleared=f.Store.DeleteWorksheetRecord("form",f.Party,saved,2,"three");
        var slots=f.Store.Load("form",f.Party).Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
        Assert.Equal(3,slots.Count);Assert.Equal("four",slots[2]["_id"]);
        Assert.Equal(3,f.Workflow.Invoices(f.Party).Count);Assert.Equal(45000m,f.Workflow.BuildCurrent(f.Party).Tables[1].Totals["E"]);
        var restored=f.Store.RestoreWorksheetRecord("form",f.Party,cleared,2,Fixture.Invoice("INV-003","three"));
        Assert.Equal(4,f.Workflow.Invoices(f.Party).Count);
        f.Store.DeleteWorksheetRecord("form",f.Party,restored,2,"three");f.Database.Dispose();
        using var reopened=new PortableDatabase(new(f.Root));var store=new WorkbookStore(reopened);
        slots=store.Load("form",f.Party).Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
        Assert.Equal(3,slots.Count);Assert.Equal("four",slots[2]["_id"]);Assert.Equal(3,new ReportWorkflow(store).Invoices(f.Party).Count);
    }
    [Fact]public void RemovedRecordRestoreIsTargetedRevisionCheckedAndValidated()
    {
        using var f=new Fixture();var original=f.Store.Load("form",f.Party);var record=new Dictionary<string,string>(original.Rows.Single(r=>r.GetValueOrDefault("_id")=="one"));
        var deleted=f.Store.DeleteWorksheetRecord("form",f.Party,original.Revision,0,"one");
        Assert.Throws<BusinessRuleException>(()=>f.Store.RestoreWorksheetRecord("form",f.Party,original.Revision,0,record));
        var restored=f.Store.RestoreWorksheetRecord("form",f.Party,deleted,0,record);
        var rows=f.Workflow.Invoices(f.Party);Assert.Equal(new[]{"one","two"},rows.Select(r=>r["_id"]));Assert.Equal("15000",rows[1]["F5"]);
        Assert.Throws<BusinessRuleException>(()=>f.Store.RestoreWorksheetRecord("form",f.Party,restored,0,record));
        f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"Protect after undo",[new("one","Bank",100)]);
        Assert.Throws<BusinessRuleException>(()=>f.Store.DeleteWorksheetRecord("form",f.Party,restored,0,"one"));
        Assert.Equal(2,f.Workflow.Invoices(f.Party).Count);
    }
    private static ReportBundle ReadExportSnapshot(string path)
    {
        using var zip=ZipFile.OpenRead(path);using var input=zip.GetEntry("customXml/tradevault.xml")!.Open();
        var xml=System.Xml.Linq.XDocument.Load(input);
        return JsonSerializer.Deserialize<ReportBundle>(Convert.FromBase64String(xml.Root!.Value))!;
    }

    [Fact]public void ImmediateRecordDeletionPersistsAndRejectsStaleOrProtectedChanges()
    {
        using var f=new Fixture();var snapshot=f.Store.Load("form",f.Party);var revision=f.Store.DeleteWorksheetRecord("form",f.Party,snapshot.Revision,1,"two");
        Assert.Equal(snapshot.Revision+1,revision);Assert.Single(f.Workflow.Invoices(f.Party));Assert.Equal("one",f.Workflow.Invoices(f.Party)[0]["_id"]);
        Assert.Throws<BusinessRuleException>(()=>f.Store.DeleteWorksheetRecord("form",f.Party,snapshot.Revision,0,"one"));Assert.Single(f.Workflow.Invoices(f.Party));
        f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"PROTECT",[new("one","Bank",100)]);
        Assert.Throws<BusinessRuleException>(()=>f.Store.DeleteWorksheetRecord("form",f.Party,revision,0,"one"));Assert.Single(f.Workflow.Invoices(f.Party));
        f.Database.Dispose();using var reopened=new PortableDatabase(new(f.Root));var store=new WorkbookStore(reopened);Assert.Single(new ReportWorkflow(store).Invoices(f.Party));
    }
    [Fact]public void DeletionPreflightProtectsInvoicesWithRecordedPaymentHistory()
    {
        using var f=new Fixture();var invoices=f.Workflow.Invoices(f.Party);f.Workflow.EnsureInvoiceCanDelete(f.Party,invoices[0]);
        f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"DELETE GUARD",[new("one","Bank",100)]);
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.EnsureInvoiceCanDelete(f.Party,invoices[0]));f.Workflow.EnsureInvoiceCanDelete(f.Party,invoices[1]);
    }
    [Fact]public void CurrentReportsIncludeAllSavedInvoicesAndExportSameSnapshot()
    {
        using var f=new Fixture();var snapshot=f.Store.Load("form",f.Party);var future=Fixture.Invoice("FUTURE","future");future["F10"]="2090-11-20";future["F17"]="2090-12-01";snapshot.Rows.Add(future);f.Store.Save("form",snapshot.Revision,snapshot.Rows,f.Party);
        Assert.Equal(2,f.Workflow.Build(f.Party,new(2026,10,28),true).Tables[0].Rows.Count);
        var current=f.Workflow.BuildCurrent(f.Party);Assert.Equal(3,current.Tables[0].Rows.Count);Assert.Equal(DateOnly.FromDateTime(DateTime.Today),current.AsOf);Assert.Equal("All saved records",current.Mode);Assert.Contains(current.Tables[1].Rows,r=>r["B"].Contains("FUTURE"));
        var exported=f.Workflow.ExportExcel(f.Party,current.AsOf,true,true);var preview=ReadExportSnapshot(exported);Assert.Equal(3,preview.Tables[0].Rows.Count);Assert.Equal(current.Mode,preview.Mode);
    }
    [Fact]public void FutureInvoiceCoverageAndExplicitCutoffAdvancePreserveInvoiceInputs()
    {
        using var f=new Fixture();var before=f.Workflow.Invoices(f.Party);var scope=f.Workflow.Coverage(f.Party,new(2026,5,31));
        Assert.Equal(2,scope.AfterDate);Assert.Equal(0,scope.Included);Assert.Equal(new DateOnly(2026,6,1),scope.LatestDate);
        Assert.Empty(f.Workflow.Build(f.Party,new(2026,5,31),true).Tables[0].Rows);
        f.Workflow.SetReportDate(f.Party,new(2026,6,1));Assert.Equal(2,f.Workflow.Build(f.Party,new(2026,6,1),true).Tables[0].Rows.Count);
        var after=f.Workflow.Invoices(f.Party);Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before),System.Text.Json.JsonSerializer.Serialize(after));
        Assert.Equal("2026-06-01",f.Store.Load("form",f.Party).Rows.Single(r=>r.ContainsKey("_metadata"))["asOf"]);
    }
    [Fact]public void PaymentScreenBalancesAreDatedAndAllowReplacingOnlyAnActiveReceipt()
    {
        using var f=new Fixture();Assert.Empty(f.Workflow.PaymentBalances(f.Party,new(2026,5,1)));
        var receipt=f.Workflow.RecordReceipt(f.Party,new(2026,7,1),3200,"SCREEN",[new("one","Bank",3000),new("one","Difference",200)]);
        var balance=f.Workflow.PaymentBalances(f.Party,new(2026,7,1)).Single(b=>b.Id=="one");
        Assert.Equal(7000,balance.BankUnpaid);Assert.Equal(4800,balance.DifferenceUnpaid);Assert.Equal(11800,balance.TotalUnpaid);
        Assert.Equal(10300,ReportWorkflow.RemainingAfterPayment(balance,1000,500));Assert.Throws<BusinessRuleException>(()=>ReportWorkflow.RemainingAfterPayment(balance,7001,0));
        Assert.Equal(15000,f.Workflow.PaymentBalances(f.Party,new(2026,7,1),receipt.Id).Single(b=>b.Id=="one").TotalUnpaid);
        f.Workflow.ReverseReceipt(f.Party,receipt.Id,new(2026,8,1),"TEST");Assert.Throws<BusinessRuleException>(()=>f.Workflow.PaymentBalances(f.Party,new(2026,8,1),receipt.Id));
    }
    [Fact]public void NewPartyReportsStartAtZeroWithoutSetup()
    {
        using var f=new Fixture();var party=f.Store.CreateParty("New party");
        var report=f.Workflow.BuildCurrent(party.Id);Assert.Empty(report.Tables[0].Rows);
        Assert.Equal(0,report.Tables[0].Totals["K"]);Assert.Equal(0,report.Tables[1].Totals["E"]);
    }
    [Fact]public void FutureReversalDoesNotPermitHistoricalOverpayment()
    {
        using var f=new Fixture();var first=f.Workflow.RecordReceipt(f.Party,new(2026,7,1),10000,"Original settlement",[new("one","Bank",10000)]);
        f.Workflow.ReverseReceipt(f.Party,first.Id,new(2026,9,1),"Later reversal");
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.RecordReceipt(f.Party,new(2026,8,1),10000,"Backdated duplicate",[new("one","Bank",10000)]));
        Assert.Equal(2,f.Workflow.Receipts(f.Party).Count);
        Assert.Equal("0.00",f.Workflow.Build(f.Party,new(2026,8,31),true).Tables[0].Rows[0]["F"]);
        f.Workflow.RecordReceipt(f.Party,new(2026,9,1),10000,"Same-day replacement",[new("one","Bank",10000)]);
    }
    [Fact]public void DuplicateStableInvoiceIdsAreRejectedAtomically()
    {
        using var f=new Fixture();var saved=f.Store.Load("form",f.Party);var rows=saved.Rows;
        rows.Single(r=>r.GetValueOrDefault("F9")=="INV-002")["_id"]="one";
        Assert.Throws<BusinessRuleException>(()=>f.Store.Save("form",saved.Revision,rows,f.Party));
        Assert.Equal(saved.Revision,f.Store.Load("form",f.Party).Revision);
    }
    [Fact]public void ExcelExportContainsOnlyTwoWorkbooksWithoutTrailingNotes()
    {
        using var f=new Fixture();var file=f.Workflow.ExportExcel(f.Party,new(2026,10,27),true);
        Assert.Contains("SYNTHETIC TEST PARTY - Account Reports - 2026-10-27 - Revision 1.xlsx",file);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(file)!));
        using var zip=ZipFile.OpenRead(file);
        foreach(var name in new[]{"xl/worksheets/sheet1.xml","xl/worksheets/sheet2.xml"})
        {
            using var reader=new StreamReader(zip.GetEntry(name)!.Open());var xml=reader.ReadToEnd();
            Assert.Contains("TOTAL / CLOSING",xml);Assert.DoesNotContain("Historical results",xml);
        }
    }

    [Fact]public void RegisterDifferenceAlwaysUsesPakistanCustomsAndNeedsNoMapping()
    {
        using var f=new Fixture();var row=Fixture.Invoice("INV-DIFF","diff");
        Assert.Equal(6000m,f.Workflow.RegisterDifference(row)); // 15000 - 9000, not our customs 10000.
        row["F6"]="500";Assert.Equal(6000m,f.Workflow.RegisterDifference(row));
        row["F5"]="16000";Assert.Equal(7000m,f.Workflow.RegisterDifference(row));

        Assert.Equal(7000m,f.Workflow.RegisterDifference(row));
        row["F7"]="0";Assert.Equal(16000m,f.Workflow.RegisterDifference(row));
        row.Remove("F7");Assert.Null(f.Workflow.RegisterDifference(row));
        row["F7"]="9000";row.Remove("F5");Assert.Null(f.Workflow.RegisterDifference(row));
        var unconfigured=f.Store.CreateParty("UNCONFIGURED");Assert.Empty(f.Workflow.BuildCurrent(unconfigured.Id).Tables[0].Rows);
        Assert.Equal(6000m,f.Workflow.RegisterDifference(Fixture.Invoice("NO-MAPPING","plain")));
    }
    private sealed class Fixture:IDisposable
    {
        public string Root{get;}=Path.Combine(AppContext.BaseDirectory,"WorkflowData",Guid.NewGuid().ToString("N"));
        public PortableDatabase Database{get;} public WorkbookStore Store{get;} public Guid Party{get;}
        public ReportWorkflow Workflow{get;}
        public Fixture()
        {
            Database=new(new(Root));Store=new(Database);Party=Store.CreateParty("SYNTHETIC TEST PARTY").Id;Workflow=new(Store);
            Store.Save("form",0,[new(){["_metadata"]="1",["terms"]="90",["opening"]="0"},Invoice("INV-001","one"),Invoice("INV-002","two")],Party);

        }
        public static Dictionary<string,string> Invoice(string number,string id)=>new(){["_id"]=id,["_terms"]="90",["F1"]="SYNTHETIC TEST PARTY",["F9"]=number,["F10"]="2026-06-01",["F5"]="15000",["F6"]="10000",["F7"]="9000",["F18"]="3800",["F17"]="2026-06-17",["F22"]="0",["F23"]="0"};
        public void Dispose(){Database.Dispose();} // Keep isolated test evidence; never delete company data.
    }
    [Fact]public void GeneratedReportsUseFixedSourcesAndReconcile()
    {
        using var f=new Fixture();var report=f.Workflow.BuildCurrent(f.Party);
        Assert.Equal("10000.00",report.Tables[0].Rows[0]["D"]);
        Assert.Equal("5000.00",report.Tables[0].Rows[0]["I"]);
        Assert.DoesNotContain(report.Tables[0].Columns,c=>c.Key=="H");
        Assert.Equal(20000m,report.Tables[0].Totals["F"]);
        Assert.Equal(10000m,report.Tables[0].Totals["K"]);
        Assert.Equal(30000m,report.Tables[1].Totals["E"]);
        Assert.Equal("2026-09-15",report.Tables[0].Rows[0]["N"]);
    }
    [Fact]public void SplitPartialReceiptsAdvancesAndReversalAreHistorical()
    {
        using var f=new Fixture();var receipt=f.Workflow.RecordReceipt(f.Party,new(2026,7,1),13000,"TEST RECEIPT",[new("one","Bank",8000),new("one","Difference",1200),new("two","Bank",3000)]);
        var before=f.Workflow.Build(f.Party,new(2026,6,30),true);Assert.Equal(20000,before.Tables[0].Totals["F"]);
        var after=f.Workflow.Build(f.Party,new(2026,7,1),true);Assert.Equal(9000,after.Tables[0].Totals["F"]);Assert.Equal(17000,after.Tables[1].Totals["E"]);
        f.Workflow.ReverseReceipt(f.Party,receipt.Id,new(2026,8,1),"Test correction");
        Assert.Equal(17000,f.Workflow.Build(f.Party,new(2026,7,31),true).Tables[1].Totals["E"]);
        Assert.Equal(30000,f.Workflow.Build(f.Party,new(2026,8,1),true).Tables[1].Totals["E"]);
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.ReverseReceipt(f.Party,receipt.Id,new(2026,8,2),"Duplicate reversal"));
    }
    [Fact]public void SettledInvoicesAreNotOverdueAndFutureInvoicesAreExcluded()
    {
        using var f=new Fixture();f.Workflow.RecordReceipt(f.Party,new(2026,7,1),15000,"Settlement",[new("one","Bank",10000),new("one","Difference",5000)]);
        var items=f.Workflow.Reminders(f.Party,new(2026,10,27));Assert.Equal("Settled",items[0].Status);Assert.Equal(0,items[0].OverdueDays);Assert.True(items[1].OverdueDays>0);
        Assert.Empty(f.Workflow.Build(f.Party,new(2026,5,1),true).Tables[0].Rows);
    }
    [Fact]public void AllocationAndLinkedInvoiceGuardsAreAtomic()
    {
        using var f=new Fixture();
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"Too much",[new("one","Bank",200)]));
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.RecordReceipt(f.Party,new(2026,7,1),20000,"Overpayment",[new("one","Bank",11000)]));
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.RecordReceipt(f.Party,new(2026,5,1),100,"Early",[new("one","Bank",100)]));
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"Cross party",[new("unknown","Bank",100)]));
        Assert.Empty(f.Workflow.Receipts(f.Party));
        f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"Posted",[new("one","Bank",100)]);
        var saved=f.Store.Load("form",f.Party);saved.Rows.RemoveAt(1);Assert.Throws<BusinessRuleException>(()=>f.Store.Save("form",saved.Revision,saved.Rows,f.Party));
    }


    [Fact]public void ExportedExcelIsOpenXmlWithMetadataNumbersAndNoFormulaInjection()
    {
        using var f=new Fixture();var package=f.Workflow.Export(f.Party,new(2026,10,27),true);var folder=Path.GetDirectoryName(package)!;
        foreach(var name in new[]{"xl/worksheets/sheet1.xml","xl/worksheets/sheet2.xml"})
        {using var zip=ZipFile.OpenRead(package);Assert.NotNull(zip.GetEntry("[Content_Types].xml"));using var reader=new StreamReader(zip.GetEntry(name)!.Open());var xml=reader.ReadToEnd();Assert.Contains("SYNTHETIC TEST PARTY",xml);Assert.Contains("TOTAL / CLOSING",xml);Assert.Contains("<v>",xml);Assert.DoesNotContain("<f>",xml);}
        Assert.Single(Directory.GetFiles(folder));
    }
    [Fact]public void DuplicateReferencesRejected()
    {
        using var f=new Fixture();var saved=f.Store.Load("form",f.Party);saved.Rows.Add(Fixture.Invoice("INV-001","three"));Assert.Throws<BusinessRuleException>(()=>f.Store.Save("form",saved.Revision,saved.Rows,f.Party));
    }
    
    [Fact]public void AdvanceCanBeAllocatedLaterThroughAtomicCorrection()
    {
        using var f=new Fixture();var advance=f.Workflow.RecordReceipt(f.Party,new(2026,5,1),5000,"Advance",[]);
        Assert.Equal(30000,f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[0].Totals["F"]+f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[0].Totals["K"]);
        f.Workflow.CorrectReceipt(f.Party,advance.Id,new(2026,7,1),5000,"Allocate advance",[new("one","Bank",5000)]);
        Assert.Equal(15000,f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[0].Totals["F"]);
        Assert.Equal(25000,f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[1].Totals["E"]);
        Assert.Equal(3,f.Workflow.Receipts(f.Party).Count);
    }
    [Fact]public void FailedCorrectionDoesNotReverseOriginalOrAppendAnything()
    {
        using var f=new Fixture();var posted=f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"Receipt",[new("one","Bank",100)]);
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.CorrectReceipt(f.Party,posted.Id,new(2026,7,2),20000,"Invalid correction",[new("one","Bank",20000)]));
        Assert.Single(f.Workflow.Receipts(f.Party));Assert.Equal(29900,f.Workflow.Build(f.Party,new(2026,7,2),true).Tables[1].Totals["E"]);
    }

    
    
}
