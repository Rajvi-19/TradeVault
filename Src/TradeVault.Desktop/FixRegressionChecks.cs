using System.Collections;
using System.Windows;
using System.Windows.Controls;
using TradeVault.Domain;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
internal static class FixRegressionChecks
{
    private static void Check(bool result,string name){if(!result)throw new InvalidOperationException(name);}
    internal static void Run(WorkbookStore store)
    {
        var fields=new[]{new Field("A","Input"),new Field("B","Calculated",true),new Field("C","Input")};
        var pasted=PastePlan.Create("one\t999\tthree",0,0,1,fields);
        Check(pasted.Count==2&&pasted[1].Key=="C"&&pasted[1].Value=="three","Full row paste skips formulas and preserves input positions.");
        var controller=new WorksheetController(store);controller.Load("form");controller.Rows.Clear();
        var first=controller.Add();first["F1"]="REGRESSION ONLY";controller.Calculate();
        Check(!first.HasErrors,"Draft row must not publish missing required fields.");
        try{controller.Save();throw new Exception("Missing invoice saved");}catch(BusinessRuleException){}
        Check(first.Errors.ContainsKey("F9")&&!first.Errors.ContainsKey("F1"),"Only missing invoice field receives an error.");
        Check(!first.GetErrors("[F1]").Cast<string>().Any(),"Errors must be scoped to field.");
        first["F9"]="REG-1";first["F17"]="2026-06-17";
        var second=controller.Add();second["F1"]="REGRESSION ONLY";second["F9"]="REG-2";second["F17"]="2026-06-17";
        first["F5"]="bad";controller.Calculate();Check(second["F19"]=="2026-09-15","Invalid row must not erase other due dates.");
        first["F5"]="100";first["F17"]="9999-12-31";try{controller.Save();throw new Exception("Date overflow saved");}catch(BusinessRuleException){}
        var small=new GridRow();small["C"]="2";var large=new GridRow();large["C"]="10";
        var sorter=new ExcelGrid.BlankLastComparer([],new Field("C","DEBIT"),true);Check(sorter.Compare(small,large)<0,"Amounts sort numerically.");
        var picker=new DateCellEditor();picker.Text="05-11-2026";Check(picker.Value=="2026-11-05","Typed date writes ISO value.");
        Check(picker.ToolTip==null,"Date input has no focus tooltip.");
        picker.Value="2026-06-17";Check(picker.Text=="17-06-2026","Reloaded date uses day-first display.");
        picker.Text="";Check(picker.Value=="","Clearing date clears value.");
        Check(DateCellEditor.FormatTyping("277")=="27-07-","Single digit month auto padded.");
        Check(DateCellEditor.FormatTyping("2772026")=="27-07-2026","Compact day/month/year formatting.");
        Check(DateCellEditor.FormatTyping("27102026")=="27-10-2026","Two digit month formatting.");
        Check(DateCellEditor.FormatTyping("2707")=="27-07-","Leading zero month preserved.");
        picker.Text="277";Check(picker.Text=="27-07-","Text changes apply date mask.");
        picker.AppendText("2026");Check(picker.Value=="2026-07-27","Completing masked year persists ISO.");
        picker.Text="";foreach(var digit in "2772026")picker.AppendText(digit.ToString());
        Check(picker.Text=="27-07-2026"&&picker.Value=="2026-07-27","Character-by-character year is not expanded prematurely.");
        var grid=new ExcelGrid(ExcelFields.For("form"),[]);Check(grid.Table.Columns[3] is DataGridTemplateColumn&&grid.Table.Columns[13] is DataGridTemplateColumn,"Editable dates use calendar columns.");
        Check(grid.Table.Columns[18].IsReadOnly,"Calculated dates remain locked.");
        var printTable=new TradeVault.Application.ReportTable("Print regression",[new("A","INVOICE"),new("B","AMOUNT / USD","number")],Enumerable.Range(1,35).Select(i=>new Dictionary<string,string>{{"A","SYNTHETIC-"+i},{"B","125.50"}}).ToList(),new(){{"B",4392.5m}});
        var report=new TradeVault.Application.ReportBundle(1,"test",Guid.NewGuid(),Guid.NewGuid(),"SYNTHETIC PDF TEST",new(2026,10,28),DateTimeOffset.UtcNow,Guid.NewGuid(),1,"Generated",[printTable],[]);
        var paginator=new ReportPaginator(report,new(1122,793));Check(paginator.PageCount>=3&&paginator.Source.DocumentPaginator==paginator,"Print pagination has a valid source and multiple pages.");
        var folder=System.IO.Path.Combine(AppContext.BaseDirectory,"TestEvidence");System.IO.Directory.CreateDirectory(folder);var pdf=System.IO.Path.Combine(folder,"report-print-regression.pdf");ReportPdf.Save(report,pdf);
        Check(System.IO.File.ReadAllText(pdf).StartsWith("%PDF-1.4"),"Direct PDF export succeeds without a printer.");
    }
}
