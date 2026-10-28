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
    [Fact]public void RegisterDifferenceRequiresMappingAndTracksChosenContract()
    {
        using var f=new Fixture();var row=Fixture.Invoice("INV-DIFF","diff");
        Assert.Equal(5000m,f.Workflow.RegisterDifference(f.Party,row));
        row["F5"]="16000";Assert.Equal(6000m,f.Workflow.RegisterDifference(f.Party,row));
        f.Workflow.Configure(f.Party,new("F7","F22",false,new(2026,1,1)));
        Assert.Equal(7000m,f.Workflow.RegisterDifference(f.Party,row));
        row.Remove("F7");Assert.Null(f.Workflow.RegisterDifference(f.Party,row));
        var unconfigured=f.Store.CreateParty(f.Owner,"UNCONFIGURED");Assert.Null(f.Workflow.RegisterDifference(unconfigured.Id,row));
    }
    private sealed class Fixture:IDisposable
    {
        public string Root{get;}=Path.Combine(AppContext.BaseDirectory,"WorkflowData",Guid.NewGuid().ToString("N"));
        public PortableDatabase Database{get;} public WorkbookStore Store{get;} public LoginSession Owner{get;} public LoginSession Operator{get;} public Guid Party{get;}
        public ReportWorkflow Workflow{get;}
        public Fixture()
        {
            Database=new(new(Root));Store=new(Database);Owner=Store.Login("owner","Owner@123");Operator=Store.Login("operator","Operator@123");Party=Store.CreateParty(Owner,"SYNTHETIC TEST PARTY").Id;Workflow=new(Store,Operator);
            Store.Save(Owner,"form",0,[new(){["_metadata"]="1",["terms"]="90",["opening"]="0"},Invoice("INV-001","one"),Invoice("INV-002","two")],Party);
            Workflow.Configure(Party,new("F6","F22",true,new(2026,1,1)));
        }
        public static Dictionary<string,string> Invoice(string number,string id)=>new(){["_id"]=id,["_terms"]="90",["F1"]="SYNTHETIC TEST PARTY",["F9"]=number,["F10"]="2026-06-01",["F5"]="15000",["F6"]="10000",["F7"]="9000",["F18"]="3800",["F17"]="2026-06-17",["F22"]="0",["F23"]="0"};
        public void Dispose(){Database.Dispose();} // Keep isolated test evidence; never delete company data.
    }
    [Fact]public void GeneratedReportsUseChosenSourcesAndReconcile()
    {
        using var f=new Fixture();var report=f.Workflow.Build(f.Party,new(2026,10,27),true);
        Assert.Equal("10000.00",report.Tables[0].Rows[0]["D"]);Assert.Equal("1200.00",report.Tables[0].Rows[0]["I"]);
        Assert.Equal(20000m,report.Tables[0].Totals["F"]);Assert.Equal(2400m,report.Tables[0].Totals["K"]);Assert.Equal(22400m,report.Tables[1].Totals["E"]);
        Assert.Equal("2026-09-15",report.Tables[0].Rows[0]["N"]);
        f.Workflow.Configure(f.Party,new("F7","F22",false,new(2026,1,1)));report=f.Workflow.Build(f.Party,new(2026,10,27),true);
        Assert.Equal("9000.00",report.Tables[0].Rows[0]["D"]);Assert.Equal("0.00",report.Tables[0].Rows[0]["H"]);
    }
    [Fact]public void SplitPartialReceiptsAdvancesAndReversalAreHistorical()
    {
        using var f=new Fixture();var receipt=f.Workflow.RecordReceipt(f.Party,new(2026,7,1),13000,"TEST RECEIPT",[new("one","Bank",8000),new("one","Difference",1200),new("two","Bank",3000)]);
        var before=f.Workflow.Build(f.Party,new(2026,6,30),true);Assert.Equal(20000,before.Tables[0].Totals["F"]);
        var after=f.Workflow.Build(f.Party,new(2026,7,1),true);Assert.Equal(9000,after.Tables[0].Totals["F"]);Assert.Equal(9400,after.Tables[1].Totals["E"]);
        f.Workflow.ReverseReceipt(f.Party,receipt.Id,new(2026,8,1),"Test correction");
        Assert.Equal(9400,f.Workflow.Build(f.Party,new(2026,7,31),true).Tables[1].Totals["E"]);
        Assert.Equal(22400,f.Workflow.Build(f.Party,new(2026,8,1),true).Tables[1].Totals["E"]);
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.ReverseReceipt(f.Party,receipt.Id,new(2026,8,2),"Duplicate reversal"));
    }
    [Fact]public void SettledInvoicesAreNotOverdueAndFutureInvoicesAreExcluded()
    {
        using var f=new Fixture();f.Workflow.RecordReceipt(f.Party,new(2026,7,1),11200,"Settlement",[new("one","Bank",10000),new("one","Difference",1200)]);
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
        var saved=f.Store.Load(f.Owner,"form",f.Party);saved.Rows.RemoveAt(1);Assert.Throws<BusinessRuleException>(()=>f.Store.Save(f.Owner,"form",saved.Revision,saved.Rows,f.Party));
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.Configure(f.Party,new("F7","F22",true,new(2026,1,1))));
    }
    [Fact]public void OfflineHandoverRetainsRevisionsReviewAndIsolationAfterReopen()
    {
        using var source=new Fixture();var first=source.Workflow.Export(source.Party,new(2026,10,27),true);source.Workflow.RecordReceipt(source.Party,new(2026,7,1),100,"Revision correction",[new("one","Bank",100)]);var second=source.Workflow.Export(source.Party,new(2026,10,27),true);
        var targetRoot=Path.Combine(source.Root,"OwnerUSB");
        using(var target=new PortableDatabase(new(targetRoot)))
        {
            var store=new WorkbookStore(target);var owner=store.Login("owner","Owner@123");var inbox=new ReportWorkflow(store,owner);
            var preview=inbox.PreviewPackage(first);Assert.Equal("SYNTHETIC TEST PARTY",preview.Report.PartyName);Assert.Equal(1,preview.Report.Revision);
            var imported=inbox.Import(first,preview.Hash);Assert.Throws<BusinessRuleException>(()=>inbox.Import(first));
            inbox.Import(second);Assert.Equal(2,inbox.Imports().Count);Assert.Equal(2,inbox.Imports().Max(i=>i.Report.Revision));
            Assert.Equal(20000,inbox.Imports().Single(i=>i.Report.Revision==1).Report.Tables[0].Totals["F"]);Assert.Equal(19900,inbox.Imports().Single(i=>i.Report.Revision==2).Report.Tables[0].Totals["F"]);
            inbox.Review(imported,"Reviewed","Checked in test");Assert.Single(store.GetParties(owner));Assert.Empty(store.Load(owner,"form").Rows);
            var op=store.Login("operator","Operator@123");Assert.Throws<BusinessRuleException>(()=>new ReportWorkflow(store,op).Imports());
        }
        File.Move(first,first+".removed");File.Move(second,second+".removed");
        using var reopened=new PortableDatabase(new(targetRoot));var finalStore=new WorkbookStore(reopened);var finalInbox=new ReportWorkflow(finalStore,finalStore.Login("owner","Owner@123"));
        Assert.Equal(2,finalInbox.Imports().Count);Assert.Contains(finalInbox.Imports(),i=>i.Status=="Reviewed"&&i.ReviewNote=="Checked in test");
        Assert.Equal(2,finalInbox.Imports()[0].Report.Tables.Count);
    }
    [Fact]public void TamperedAndUnexpectedPackagesAreRejectedWithoutImport()
    {
        using var f=new Fixture();var path=f.Workflow.Export(f.Party,new(2026,10,27),true);var owner=new ReportWorkflow(f.Store,f.Owner);
        var preview=owner.PreviewPackage(path);
        using(var zip=ZipFile.Open(path,ZipArchiveMode.Update)){zip.GetEntry("report.json")!.Delete();using var writer=new StreamWriter(zip.CreateEntry("report.json").Open());writer.Write("{}");}
        Assert.Throws<BusinessRuleException>(()=>owner.Import(path,preview.Hash));Assert.Empty(owner.Imports());
        var malicious=Path.Combine(f.Root,"bad.tvreport");using(var zip=ZipFile.Open(malicious,ZipArchiveMode.Create)){using var writer=new StreamWriter(zip.CreateEntry("../escape").Open());writer.Write("bad");}
        Assert.Throws<BusinessRuleException>(()=>owner.Preview(malicious));Assert.False(File.Exists(Path.Combine(f.Root,"escape")));
    }
    [Fact]public void ExportedExcelIsOpenXmlWithMetadataNumbersAndNoFormulaInjection()
    {
        using var f=new Fixture();var package=f.Workflow.Export(f.Party,new(2026,10,27),true);var folder=Path.GetDirectoryName(package)!;
        foreach(var name in new[]{"AccountDetails.xlsx","DebitCredit.xlsx"})
        {using var zip=ZipFile.OpenRead(Path.Combine(folder,name));Assert.NotNull(zip.GetEntry("[Content_Types].xml"));using var reader=new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());var xml=reader.ReadToEnd();Assert.Contains("SYNTHETIC TEST PARTY",xml);Assert.Contains("TOTAL / CLOSING",xml);Assert.Contains("<v>",xml);Assert.DoesNotContain("<f>",xml);}
        Assert.True(File.Exists(Path.Combine(folder,"Print.html")));
    }
    [Fact]public void DuplicateReferencesRejectedAndPasswordChangeInvalidatesSessions()
    {
        using var f=new Fixture();var saved=f.Store.Load(f.Owner,"form",f.Party);saved.Rows.Add(Fixture.Invoice("INV-001","three"));Assert.Throws<BusinessRuleException>(()=>f.Store.Save(f.Owner,"form",saved.Revision,saved.Rows,f.Party));
        f.Store.ChangePassword(f.Owner,"Owner@123","Changed-Test-Password-123");Assert.Throws<BusinessRuleException>(()=>f.Store.GetParties(f.Owner));
        Assert.Throws<BusinessRuleException>(()=>f.Store.Login("owner","Owner@123"));Assert.Equal("Owner",f.Store.Login("owner","Changed-Test-Password-123").Role);
    }
    [Fact]public void VerifiedBackupRestoreRequiresOwnerAndSignsOut()
    {
        using var f=new Fixture();var backup=f.Store.CreateBackup(f.Owner);f.Store.CreateParty(f.Owner,"AFTER BACKUP");
        Assert.Throws<BusinessRuleException>(()=>f.Store.RestoreBackup(f.Operator,backup));
        f.Store.RestoreBackup(f.Owner,backup);Assert.Throws<BusinessRuleException>(()=>f.Store.GetParties(f.Owner));
        var owner=f.Store.Login("owner","Owner@123");Assert.DoesNotContain(f.Store.GetParties(owner),p=>p.Name=="AFTER BACKUP");Assert.Equal(2,f.Store.GetParties(owner).Count);
    }
    [Fact]public void AdvanceCanBeAllocatedLaterThroughAtomicCorrection()
    {
        using var f=new Fixture();var advance=f.Workflow.RecordReceipt(f.Party,new(2026,5,1),5000,"Advance",[]);
        Assert.Equal(22400,f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[0].Totals["F"]+f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[0].Totals["K"]);
        f.Workflow.CorrectReceipt(f.Party,advance.Id,new(2026,7,1),5000,"Allocate advance",[new("one","Bank",5000)]);
        Assert.Equal(15000,f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[0].Totals["F"]);
        Assert.Equal(17400,f.Workflow.Build(f.Party,new(2026,7,1),true).Tables[1].Totals["E"]);
        Assert.Equal(3,f.Workflow.Receipts(f.Party).Count);
    }
    [Fact]public void FailedCorrectionDoesNotReverseOriginalOrAppendAnything()
    {
        using var f=new Fixture();var posted=f.Workflow.RecordReceipt(f.Party,new(2026,7,1),100,"Receipt",[new("one","Bank",100)]);
        Assert.Throws<BusinessRuleException>(()=>f.Workflow.CorrectReceipt(f.Party,posted.Id,new(2026,7,2),20000,"Invalid correction",[new("one","Bank",20000)]));
        Assert.Single(f.Workflow.Receipts(f.Party));Assert.Equal(22300,f.Workflow.Build(f.Party,new(2026,7,2),true).Tables[1].Totals["E"]);
    }
    [Fact]public void ValidPackageChangedAfterPreviewIsRejected()
    {
        using var f=new Fixture();var path=f.Workflow.Export(f.Party,new(2026,10,27),true);var inbox=new ReportWorkflow(f.Store,f.Owner);var preview=inbox.PreviewPackage(path);
        var next=f.Workflow.Export(f.Party,new(2026,10,27),true);File.Copy(next,path,true);
        Assert.Throws<BusinessRuleException>(()=>inbox.Import(path,preview.Hash));Assert.Empty(inbox.Imports());
    }
    [Fact]public void PreviousSchemaBackupCanBeRestoredAndUpgraded()
    {
        using var f=new Fixture();var backup=f.Store.CreateBackup(f.Owner);
        using(var c=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=backup,Pooling=false}.ToString()))
        {c.Open();using var cmd=c.CreateCommand();cmd.CommandText="DROP TABLE workflow_documents;DROP TABLE workflow_audit;DELETE FROM schema_migrations WHERE version=4";cmd.ExecuteNonQuery();}
        f.Store.RestoreBackup(f.Owner,backup);var owner=f.Store.Login("owner","Owner@123");Assert.NotNull(f.Store.Load(owner,"form",f.Party));
        new ReportWorkflow(f.Store,owner).Configure(f.Party,new("F6","F22",true,new(2026,1,1)));
        Assert.Equal(22400,new ReportWorkflow(f.Store,owner).Build(f.Party,new(2026,10,27),true).Tables[1].Totals["E"]);
    }
    [Fact]public void IncompleteBackupIsRejectedWithoutReplacingWorkingData()
    {
        using var f=new Fixture();var backup=f.Store.CreateBackup(f.Owner);
        using(var c=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder{DataSource=backup,Pooling=false}.ToString())){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="DROP TABLE workflow_documents";cmd.ExecuteNonQuery();}
        Assert.Throws<BusinessRuleException>(()=>f.Store.RestoreBackup(f.Owner,backup));Assert.Equal(22400,f.Workflow.Build(f.Party,new(2026,10,27),true).Tables[1].Totals["E"]);
    }
}
