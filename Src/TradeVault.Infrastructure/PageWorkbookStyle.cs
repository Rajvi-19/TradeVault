using System.IO.Compression;
using System.Xml.Linq;
using TradeVault.Application;

namespace TradeVault.Infrastructure;

public sealed partial class ReportWorkflow
{
    private static byte[] StylePageWorkbook(byte[] bytes,ReportBundle report)
    {
        XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var memory=new MemoryStream();memory.Write(bytes);
        using(var zip=new ZipArchive(memory,ZipArchiveMode.Update,true))
        {
            XDocument Read(string path){using var input=zip.GetEntry(path)!.Open();return XDocument.Load(input);}
            void Save(string path,XDocument xml){zip.GetEntry(path)!.Delete();using var output=zip.CreateEntry(path).Open();xml.Save(output);}
            var styles=Read("xl/styles.xml");
            var fonts=styles.Root!.Element(ns+"fonts")!;
            fonts.Elements().ElementAt(1).Element(ns+"color")!.SetAttributeValue("rgb","FF000000");
            fonts.Elements().ElementAt(1).Element(ns+"sz")!.SetAttributeValue("val",12);
            var borders=styles.Root.Element(ns+"borders")!;
            borders.RemoveNodes();
            borders.Add(new XElement(ns+"border"),new XElement(ns+"border",
                new[]{"left","right","top","bottom"}.Select(edge=>new XElement(ns+edge,new XAttribute("style","thin"),new XElement(ns+"color",new XAttribute("rgb","FFB8B8B8"))))));
            borders.SetAttributeValue("count",2);
            var formats=styles.Root.Element(ns+"cellXfs")!;
            foreach(var (index,format) in formats.Elements().Select((f,i)=>(i,f)))
            {
                format.SetAttributeValue("fillId",0);
                format.SetAttributeValue("borderId",1);
                format.SetAttributeValue("applyBorder",1);
                format.Element(ns+"alignment")?.SetAttributeValue("horizontal",index==3?"center":index is 1 or 5?"right":index is 2 or 6?"center":"left");
            }
            Save("xl/styles.xml",styles);
            var table=report.Tables.Single();var sheet=Read("xl/worksheets/sheet1.xml");var data=sheet.Root!.Element(ns+"sheetData")!;
            sheet.Descendants(ns+"sheetView").Single().SetAttributeValue("showGridLines",1);
            foreach(var row in data.Elements())row.SetAttributeValue("ht",row.Attribute("r")!.Value=="1"?48:22);
            var cols=sheet.Root.Element(ns+"cols")!;cols.RemoveNodes();
            for(int i=0;i<table.Columns.Count;i++)
            {
                var column=table.Columns[i];var length=table.Rows.Select(r=>r.GetValueOrDefault(column.Key,"").Length).DefaultIfEmpty(10).Max();var width=column.Type=="date"?15:column.Type is "number" or "integer"?19:Math.Clamp(length+2,18,42);
                cols.Add(new XElement(ns+"col",new XAttribute("min",i+1),new XAttribute("max",i+1),new XAttribute("width",width),new XAttribute("customWidth",1)));
            }
            Save("xl/worksheets/sheet1.xml",sheet);
            var book=Read("xl/workbook.xml");book.Descendants(ns+"definedName").Single().Value=$"'{table.Name.Replace("'","''")}'!$1:$1";Save("xl/workbook.xml",book);
        }
        return memory.ToArray();
    }
}
