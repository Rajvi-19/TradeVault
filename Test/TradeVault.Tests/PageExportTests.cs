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
        using var sheet=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var document=XDocument.Load(sheet);Assert.Equal(6,document.Descendants(ns+"row").Count());
        var xml=document.ToString();Assert.Contains("VISIBLE-1",xml);Assert.DoesNotContain("MUST NOT EXPORT",xml);Assert.DoesNotContain("TOTAL / CLOSING",xml);Assert.Contains("125.50",xml);
        Assert.Throws<BusinessRuleException>(()=>ReportWorkflow.ExportPageWorkbook(report with{Tables=[table,table]}));
    }
}
