using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using TradeVault.Application;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class ReportWorkflow
{
    private const int MaximumPackage=32*1024*1024;
    public string Export(Guid party,DateOnly asOf,bool generated)=>ExportExcel(party,asOf,generated);
    public string ExportExcel(Guid party,DateOnly asOf,bool generated,bool includeAll=false)
    {
        var snapshot=includeAll?BuildCurrent(party,generated):Build(party,asOf,generated);
        var report=store.Mutate<ReportBundle>("export",snapshot.SubmissionId.ToString(),old=>snapshot with{Revision=(old?.Revision??0)+1});
        var safe=new string(report.PartyName.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray()).Trim().TrimEnd('.');
        if(string.IsNullOrWhiteSpace(safe))safe="Party";
        safe=safe[..Math.Min(60,safe.Length)];
        if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","LPT1","LPT2","LPT3"}.Contains(safe.ToUpperInvariant()))safe="Party-"+safe;
        var folder=Path.Combine(Root,"Reports",safe);Directory.CreateDirectory(folder);
        var filename=$"{safe} - Account Reports - {asOf:yyyy-MM-dd} - Revision {report.Revision}.xlsx";
        var path=Path.Combine(folder,filename);int copy=2;
        while(File.Exists(path))path=Path.Combine(folder,Path.GetFileNameWithoutExtension(filename)+$" - Copy {copy++}.xlsx");
        WriteNew(path,ExportWorkbook(report));return path;
    }

    private static void WriteNew(string path,byte[] bytes)
    {var pending=path+".partial";using(var stream=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}File.Move(pending,path);}
    public static byte[] ExportPageWorkbook(ReportBundle report)
    {
        if(report.Tables.Count!=1)throw new BusinessRuleException("A page export must contain exactly one worksheet.");
        return StylePageWorkbook(Workbook(report,report.Tables[0],false),report);
    }
    private static byte[] Workbook(ReportBundle report,ReportTable table,bool includeTotals=true)
    {
        XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";XNamespace rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using var memory=new MemoryStream();using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))
        {
            void Put(string name,string xml){using var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false));writer.Write(xml);}
            Put("[Content_Types].xml","""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Put("_rels/.rels","""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Put("xl/workbook.xml",new XElement(ns+"workbook",new XAttribute(XNamespace.Xmlns+"r",rel),new XElement(ns+"sheets",new XElement(ns+"sheet",new XAttribute("name",table.Name),new XAttribute("sheetId",1),new XAttribute(rel+"id","rId1"))),new XElement(ns+"definedNames",new XElement(ns+"definedName",new XAttribute("name","_xlnm.Print_Titles"),new XAttribute("localSheetId",0),$"'{table.Name}'!$1:$5"))).ToString());
            Put("xl/_rels/workbook.xml.rels","""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Put("xl/styles.xml","""<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="10"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FF111111"/><name val="Calibri"/></font></fonts><fills count="4"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFFFFF"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFFFFF"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left style="hair"><color rgb="FFDADADA"/></left><right style="hair"><color rgb="FFDADADA"/></right><top style="hair"><color rgb="FFDADADA"/></top><bottom style="hair"><color rgb="FFDADADA"/></bottom></border></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="7"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"><alignment horizontal="center" wrapText="1" vertical="center"/></xf><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"><alignment horizontal="center" vertical="center"/></xf><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0"><alignment horizontal="center" wrapText="1" vertical="center"/></xf><xf numFmtId="0" fontId="0" fillId="3" borderId="0" xfId="0" applyFill="1" applyNumberFormat="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf><xf numFmtId="4" fontId="0" fillId="3" borderId="0" xfId="0" applyFill="1" applyNumberFormat="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf><xf numFmtId="14" fontId="0" fillId="3" borderId="0" xfId="0" applyFill="1" applyNumberFormat="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");
            var data=new XElement(ns+"sheetData");int number=0;var headerRow=includeTotals?5:1;
            void Row(IEnumerable<(string Text,string Type)> cells,bool heading=false)
            {var r=new XElement(ns+"row",new XAttribute("r",++number));if(heading)r.Add(new XAttribute("ht",60),new XAttribute("customHeight",1));if(!heading)r.Add(new XAttribute("ht",34),new XAttribute("customHeight",1));int column=0;foreach(var (text,type) in cells){column++;string letters="";for(int n=column;n>0;n=(n-1)/26)letters=(char)('A'+(n-1)%26)+letters;var cell=new XElement(ns+"c",new XAttribute("r",letters+number));
                if(type is "number" or "integer"&&decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var amount)){cell.Add(new XAttribute("s",type=="integer"?0:1),new XElement(ns+"v",amount.ToString(CultureInfo.InvariantCulture)));}
                else if(type=="date"&&DateOnly.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)&&date.Year>=1900){cell.Add(new XAttribute("s",2),new XElement(ns+"v",date.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture)));}
                else{cell.Add(new XAttribute("t","inlineStr"),new XAttribute("s",heading?3:0),new XElement(ns+"is",new XElement(ns+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text)));}if(!heading&&(number-headerRow)%2==0)cell.SetAttributeValue("s",4+int.Parse(cell.Attribute("s")?.Value??"0"));r.Add(cell);}data.Add(r);}
            if(includeTotals){Row([(report.PartyName+" · "+table.Name,"text")]);Row([($"USD · {(report.Mode=="All saved records"?"Prepared":"As of")} {report.AsOf:dd-MM-yyyy} · {report.Mode}","text")]);Row([($"Submission {report.SubmissionId} · revision {report.Revision} · {report.CalculationVersion}","text")]);Row([("Generated "+report.CreatedAt.ToString("O"),"text")]);}
            Row(table.Columns.Select(c=>(c.Label,"text")),true);
            foreach(var row in table.Rows)Row(table.Columns.Select(c=>(row.GetValueOrDefault(c.Key,""),c.Type)));
            if(includeTotals)Row(table.Columns.Select((c,i)=>(i==0?"TOTAL / CLOSING":table.Totals.TryGetValue(c.Key,out var total)?total.ToString("0.00",CultureInfo.InvariantCulture):"",i==0?"text":"number")));

            var sheet=new XElement(ns+"worksheet",new XElement(ns+"sheetViews",new XElement(ns+"sheetView",new XAttribute("workbookViewId",0),new XAttribute("showGridLines",0),new XElement(ns+"pane",new XAttribute("ySplit",headerRow),new XAttribute("topLeftCell","A"+(headerRow+1)),new XAttribute("state","frozen")))),new XElement(ns+"cols",new XElement(ns+"col",new XAttribute("min",1),new XAttribute("max",table.Columns.Count),new XAttribute("width",27),new XAttribute("customWidth",1))),data,new XElement(ns+"autoFilter",new XAttribute("ref",$"A{headerRow}:{(char)('A'+table.Columns.Count-1)}{table.Rows.Count+headerRow}")),new XElement(ns+"pageMargins",new XAttribute("left",0.25),new XAttribute("right",0.25),new XAttribute("top",0.4),new XAttribute("bottom",0.4),new XAttribute("header",0.2),new XAttribute("footer",0.2)),new XElement(ns+"pageSetup",new XAttribute("paperSize",8),new XAttribute("orientation","landscape"),new XAttribute("fitToWidth",1),new XAttribute("fitToHeight",0)));
            var last=(char)('A'+table.Columns.Count-1);var merged=new XElement(ns+"mergeCells");foreach(var r in Enumerable.Range(1,includeTotals?4:0))merged.Add(new XElement(ns+"mergeCell",new XAttribute("ref",$"A{r}:{last}{r}")));if(includeTotals)sheet.Element(ns+"autoFilter")!.AddAfterSelf(merged);Put("xl/worksheets/sheet1.xml",sheet.ToString());
        }return memory.ToArray();
    }
    private static string PrintHtml(ReportBundle report)
    {
        static string E(string text)=>System.Net.WebUtility.HtmlEncode(text);var html=new StringBuilder("<!doctype html><meta charset='utf-8'><title>TradeVault reports</title><style>body{font:11px Arial;color:#18243b}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:5px;text-align:right}th{background:#eee}td:first-child,td:nth-child(2){text-align:left}section{break-after:page}thead{display:table-header-group}@page{size:A3 landscape;margin:12mm}</style>");
        foreach(var table in report.Tables){html.Append($"<section><h1>{E(report.PartyName)} — {E(table.Name)}</h1><p>USD · {report.AsOf:dd-MM-yyyy} · revision {report.Revision} · {E(report.Mode)}</p><table><thead><tr>");foreach(var c in table.Columns)html.Append("<th>"+E(c.Label)+"</th>");html.Append("</tr></thead><tbody>");foreach(var row in table.Rows){html.Append("<tr>");foreach(var c in table.Columns)html.Append("<td>"+E(row.GetValueOrDefault(c.Key,""))+"</td>");html.Append("</tr>");}html.Append("<tr>");for(int i=0;i<table.Columns.Count;i++){var c=table.Columns[i];html.Append("<td>"+(i==0?"TOTAL / CLOSING":table.Totals.TryGetValue(c.Key,out var value)?value.ToString("N2",CultureInfo.InvariantCulture):"")+"</td>");}html.Append("</tr></tbody></table>");foreach(var note in report.Notes)html.Append("<p>"+E(note)+"</p>");html.Append("</section>");}return html.ToString();
    }
}
