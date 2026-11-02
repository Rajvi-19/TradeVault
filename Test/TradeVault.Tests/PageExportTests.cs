using System.IO.Compression;
using System.Xml.Linq;
using TradeVault.Application;
using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class PageExportTests
{
    [Fact]public void PageWorkbookHasOneSheetOnlyRequestedColumnsRowsAndNoReportFooter()
    {
        var table=new ReportTable("Invoice Register",[new("party","Party name"),new("invoice","India invoice number"),new("amount","Amount ($)","number")],[new(){{"party","Synthetic buyer"},{"invoice","VISIBLE-1"},{"amount","125.50"},{"hidden","MUST NOT EXPORT"}}],new());
        var report=new ReportBundle(1,"Current page",Guid.NewGuid(),Guid.Empty,"Party: Synthetic buyer",new(2026,10,31),DateTimeOffset.UtcNow,Guid.NewGuid(),0,"Page export",[table],[]);
        using var zip=new ZipArchive(new MemoryStream(ReportWorkflow.ExportPageWorkbook(report)));XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var book=zip.GetEntry("xl/workbook.xml")!.Open();var workbook=XDocument.Load(book);Assert.Single(workbook.Descendants(ns+"sheet"));Assert.Equal("Invoice Register",workbook.Descendants(ns+"sheet").Single().Attribute("name")!.Value);
        Assert.Null(zip.GetEntry("xl/worksheets/sheet2.xml"));Assert.DoesNotContain(zip.Entries,entry=>entry.FullName.StartsWith("customXml/"));
        using var sheet=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var document=XDocument.Load(sheet);Assert.Equal(2,document.Descendants(ns+"row").Count());
        var xml=document.ToString();Assert.Contains("VISIBLE-1",xml);Assert.DoesNotContain("Submission",xml);Assert.DoesNotContain("Generated",xml);Assert.Equal("A1:C2",document.Descendants(ns+"autoFilter").Single().Attribute("ref")!.Value);Assert.DoesNotContain("MUST NOT EXPORT",xml);Assert.DoesNotContain("TOTAL / CLOSING",xml);Assert.Contains("125.50",xml);
        Assert.Empty(document.Descendants(ns+"mergeCell"));
        Assert.Equal("A2",document.Descendants(ns+"pane").Single().Attribute("topLeftCell")!.Value);
        using var styleStream=zip.GetEntry("xl/styles.xml")!.Open();var styles=XDocument.Load(styleStream);
        Assert.All(styles.Descendants(ns+"cellXfs").Elements(),f=>Assert.Equal("0",f.Attribute("fillId")!.Value));
        Assert.Equal("1",document.Descendants(ns+"sheetView").Single().Attribute("showGridLines")!.Value);
        Assert.All(styles.Descendants(ns+"cellXfs").Elements(),f=>{Assert.Equal("1",f.Attribute("borderId")!.Value);Assert.Equal("1",f.Attribute("applyBorder")!.Value);});
        Assert.Equal(4,styles.Descendants(ns+"border").Last().Elements().Count(e=>e.Attribute("style")?.Value=="thin"));
        var fonts=styles.Descendants(ns+"font").ToList();Assert.NotNull(fonts[1].Element(ns+"b"));Assert.True(double.Parse(fonts[1].Element(ns+"sz")!.Attribute("val")!.Value)>double.Parse(fonts[0].Element(ns+"sz")!.Attribute("val")!.Value));
        Assert.Throws<BusinessRuleException>(()=>ReportWorkflow.ExportPageWorkbook(report with{Tables=[table,table]}));
    }
    [Fact]public void ReportSummaryExportsBelowTableWithNumericAmountsAndFilterExcludesIt()
    {
        var table=new ReportTable("Party Ledger",[new("B","Particulars"),new("C","Debit","number"),new("D","Credit","number")],[new(){{"B","Invoice"},{"C","1000"},{"D","0"}}],new());
        var report=new ReportBundle(1,"Current page",Guid.NewGuid(),Guid.Empty,"Buyer",new(2026,11,2),DateTimeOffset.UtcNow,Guid.NewGuid(),0,"Page export",[table],[]);
        var summary=new Dictionary<string,string>{{"Total invoice amount","USD 1000.00"},{"Amount still owed by party","USD -25.50"},{"Number of invoices","1"}};
        using var zip=new ZipArchive(new MemoryStream(ReportWorkflow.ExportPageWorkbook(report,summary)));using var stream=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var sheet=XDocument.Load(stream);XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal("A1:C2",sheet.Descendants(ns+"autoFilter").Single().Attribute("ref")!.Value);Assert.Contains("Summary",sheet.ToString());Assert.Contains("Amount still owed by party",sheet.ToString());
        Assert.Equal("1000.00",sheet.Descendants(ns+"c").Single(c=>c.Attribute("r")?.Value=="C6").Element(ns+"v")!.Value);Assert.Equal("-25.50",sheet.Descendants(ns+"c").Single(c=>c.Attribute("r")?.Value=="C7").Element(ns+"v")!.Value);Assert.Equal("1",sheet.Descendants(ns+"c").Single(c=>c.Attribute("r")?.Value=="C8").Element(ns+"v")!.Value);
        Assert.All(sheet.Descendants(ns+"c").Where(c=>new[]{"C6","C7","C8"}.Contains(c.Attribute("r")?.Value)),c=>Assert.Null(c.Attribute("t")));Assert.Equal(4,sheet.Descendants(ns+"mergeCell").Count());
    }}

