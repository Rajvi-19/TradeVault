using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using TradeVault.Application;

namespace TradeVault.Desktop;

// Fixed page layout repeats the identity and column headings on every printed page.
internal sealed class ReportPaginator : DocumentPaginator
{
    private readonly ReportBundle report;
    private readonly List<(ReportTable Table,int Start,int Count,bool Total)> pages=[];
    public ReportPaginator(ReportBundle report,Size size)
    {
        this.report=report;PageSize=size;
        int capacity=Math.Max(1,(int)((size.Height-220)/38));
        foreach(var table in report.Tables)
        {
            for(int start=0;start<table.Rows.Count;start+=capacity)
                pages.Add((table,start,Math.Min(capacity,table.Rows.Count-start),start+capacity>=table.Rows.Count));
            if(table.Rows.Count==0)pages.Add((table,0,0,true));
        }
    }
    public override bool IsPageCountValid=>true;
    public override int PageCount=>pages.Count;
    public override Size PageSize{get;set;}
    private sealed class SourceDocument(ReportPaginator paginator):IDocumentPaginatorSource
    {public DocumentPaginator DocumentPaginator=>paginator;}
    public override IDocumentPaginatorSource Source=>new SourceDocument(this);
    public override DocumentPage GetPage(int pageNumber)
    {
        if(pageNumber<0||pageNumber>=pages.Count)return DocumentPage.Missing;
        var page=pages[pageNumber];var visual=new DrawingVisual();using var dc=visual.RenderOpen();
        dc.DrawRectangle(Brushes.White,null,new Rect(PageSize));
        double left=24,width=PageSize.Width-48,cw=width/page.Table.Columns.Count;
        void Text(string value,double x,double y,double w,double h,double size,bool bold=false,bool right=false)
        {
            var text=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,Brushes.Black,1){MaxTextWidth=Math.Max(1,w-10),MaxTextHeight=h-6,Trimming=TextTrimming.CharacterEllipsis,TextAlignment=right?TextAlignment.Right:TextAlignment.Left};
            dc.DrawText(text,new Point(x+5,y+3));
        }
        Text("TRADEVAULT",left,18,width,25,12,true);
        Text(report.PartyName+"  /  "+page.Table.Name,left,44,width,30,18,true);
        Text($"All amounts USD  •  As of {report.AsOf:dd-MM-yyyy}",left,76,width,24,10);
        void Row(IEnumerable<string> values,double y,double height,bool header=false,bool total=false)
        {
            int index=0;foreach(var value in values){double x=left+index*cw;
                dc.DrawRectangle(header?new SolidColorBrush(Color.FromRgb(229,236,246)):total?Brushes.AliceBlue:Brushes.White,new Pen(Brushes.LightGray,.5),new Rect(x,y,cw,height));
                Text(value,x,y,cw,height,header?8:9,header||total,!header&&page.Table.Columns[index].Type=="number");index++;}
        }
        Row(page.Table.Columns.Select(c=>c.Label),108,48,true);
        double y=156;foreach(var row in page.Table.Rows.Skip(page.Start).Take(page.Count))
        {
            Row(page.Table.Columns.Select(c=>{var value=row.GetValueOrDefault(c.Key,"");return c.Type=="number"&&decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var amount)?amount.ToString("N2",CultureInfo.InvariantCulture):value;}),y,38);y+=38;
        }
        if(page.Total)Row(page.Table.Columns.Select((c,i)=>i==0?"TOTAL / CLOSING":page.Table.Totals.TryGetValue(c.Key,out var total)?total.ToString("N2",CultureInfo.InvariantCulture):""),y,28,total:true);
        Text($"TradeVault  •  {page.Table.Name}  •  Page {pageNumber+1} of {PageCount}",left,PageSize.Height-24,width,22,8);
        return new DocumentPage(visual,PageSize,new Rect(PageSize),new Rect(PageSize));
    }
}
