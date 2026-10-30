using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TradeVault.Application;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class ReportWorkflow
{
    private static byte[] ExportWorkbook(ReportBundle report)
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
}
