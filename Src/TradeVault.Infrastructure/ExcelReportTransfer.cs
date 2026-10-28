using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TradeVault.Application;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class ReportWorkflow
{
    private static byte[] TransferWorkbook(ReportBundle report)
    {
        using var memory=new MemoryStream();
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
        {
            void Put(string name,byte[] data){using var output=zip.CreateEntry(name).Open();output.Write(data);}
            using var first=new ZipArchive(new MemoryStream(Workbook(report,report.Tables[0])),ZipArchiveMode.Read);
            foreach(var entry in first.Entries){using var input=entry.Open();using var data=new MemoryStream();input.CopyTo(data);Put(entry.FullName,data.ToArray());}
        }
        // Update the regular Excel package to contain both visible report sheets.
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Update,true))
        {
            void Put(string name,string value){zip.GetEntry(name)?.Delete();using var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false));writer.Write(value);}
            XDocument Read(string name){using var input=zip.GetEntry(name)!.Open();return XDocument.Load(input);}
            XNamespace main="http://schemas.openxmlformats.org/spreadsheetml/2006/main",rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships",pkg="http://schemas.openxmlformats.org/package/2006/relationships",ct="http://schemas.openxmlformats.org/package/2006/content-types";
            var workbook=Read("xl/workbook.xml");workbook.Root!.Element(main+"sheets")!.Add(new XElement(main+"sheet",new XAttribute("name","Debit Credit"),new XAttribute("sheetId",2),new XAttribute(rel+"id","rId3")));Put("xl/workbook.xml",workbook.ToString());
            var relationships=Read("xl/_rels/workbook.xml.rels");relationships.Root!.Add(new XElement(pkg+"Relationship",new XAttribute("Id","rId3"),new XAttribute("Type",rel.NamespaceName+"/worksheet"),new XAttribute("Target","worksheets/sheet2.xml")));Put("xl/_rels/workbook.xml.rels",relationships.ToString());
            var types=Read("[Content_Types].xml");types.Root!.Add(new XElement(ct+"Override",new XAttribute("PartName","/xl/worksheets/sheet2.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),new XElement(ct+"Override",new XAttribute("PartName","/customXml/tradevault.xml"),new XAttribute("ContentType","application/xml")));Put("[Content_Types].xml",types.ToString());
            var rootRels=Read("_rels/.rels");rootRels.Root!.Add(new XElement(pkg+"Relationship",new XAttribute("Id","tradevault"),new XAttribute("Type",rel.NamespaceName+"/customXml"),new XAttribute("Target","customXml/tradevault.xml")));Put("_rels/.rels",rootRels.ToString());
            using var second=new ZipArchive(new MemoryStream(Workbook(report,report.Tables[1])),ZipArchiveMode.Read);using var reader=new StreamReader(second.GetEntry("xl/worksheets/sheet1.xml")!.Open());Put("xl/worksheets/sheet2.xml",reader.ReadToEnd());
            Put("customXml/tradevault.xml",new XElement("TradeVaultReport",new XAttribute("version",1),Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(report))).ToString());
        }
        if(memory.Length>MaximumPackage)throw new BusinessRuleException("Excel report exceeds 32 MB.");return memory.ToArray();
    }
    private static ReportBundle ReadExcel(byte[] bytes)
    {
        try
        {
            using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
            if(zip.Entries.Count>40||zip.Entries.Select(e=>e.FullName).Distinct().Count()!=zip.Entries.Count||zip.Entries.Sum(e=>e.Length)>MaximumPackage||zip.Entries.Any(e=>e.Length>MaximumPackage/2))throw new BusinessRuleException("Oversized or invalid Excel report.");
            XDocument Read(string name){using var input=(zip.GetEntry(name)??throw new BusinessRuleException("Choose an original TradeVault Excel report containing both report sheets.")).Open();return XDocument.Load(input);}
            var metadata=Read("customXml/tradevault.xml");var report=JsonSerializer.Deserialize<ReportBundle>(Convert.FromBase64String(metadata.Root!.Value))??throw new BusinessRuleException("Missing report data.");
            if(report.FormatVersion!=1||report.Revision<1||report.SourceId==Guid.Empty||report.SubmissionId==Guid.Empty||report.PartyName.Length is <1 or >200||report.Tables.Count!=2||report.Tables[0].Name!="Account Details"||report.Tables[1].Name!="Debit Credit"||report.Tables.Any(t=>t.Rows.Count>100000||!t.Columns.SequenceEqual(Columns(t.Name))))throw new BusinessRuleException("Unsupported Excel report schema.");
            XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            for(int i=0;i<2;i++)
            {
                using var expected=new ZipArchive(new MemoryStream(Workbook(report,report.Tables[i])),ZipArchiveMode.Read);using var input=expected.GetEntry("xl/worksheets/sheet1.xml")!.Open();var original=XDocument.Load(input);
                if(!XNode.DeepEquals(Read($"xl/worksheets/sheet{i+1}.xml").Root!.Element(ns+"sheetData"),original.Root!.Element(ns+"sheetData")))throw new BusinessRuleException("Report cells were changed. Export a fresh report from TradeVault before importing.");
            }
            return report;
        }
        catch(Exception ex)when(ex is InvalidDataException or JsonException or System.Xml.XmlException or FormatException or NullReferenceException or ArgumentException){throw new BusinessRuleException("Invalid TradeVault Excel report: "+ex.Message);}
    }
}
