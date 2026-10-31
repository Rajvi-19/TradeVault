using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TradeVault.Application;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

public sealed partial class MainWindow
{
    private readonly TextBlock pageTitle=Ui.Text("Invoice Register",14,"#D9E4F4",true);
    private readonly Button exportButton=new();
    private readonly ContextMenu exportMenu=new();
    private DockPanel? pageHeading;
    private static string PageName(string name)=>name switch{"parties"=>"Party Master","agents"=>"Agent Master","form"=>"Invoice Register","partyReceipt"=>"Party Receipt","agentReceipt"=>"Agent Receipt","details"=>"Account Details","ledger"=>"Debit Credit",_=>"Reports"};
    private void SetupExport()
    {
        exportButton.SetResourceReference(StyleProperty,"SecondaryActionButton");exportButton.Content="Export  ▾";exportButton.Padding=new(12,4,12,4);exportButton.Margin=new(0,4,8,4);exportButton.MinHeight=30;
        System.Windows.Automation.AutomationProperties.SetName(exportButton,"Export current page");
        foreach(var pdf in new[]{false,true})
        {
            var item=new MenuItem{Header=pdf?"Export to PDF":"Export to Excel",Padding=new(12,8,12,8)};item.Click+=(_,_)=>ExportCurrentPage(pdf);exportMenu.Items.Add(item);
        }
        exportMenu.Background=Ui.Color("#FFFFFF");exportMenu.Foreground=Ui.Color("#18243B");exportButton.Click+=(_,_)=>{exportMenu.PlacementTarget=exportButton;exportMenu.Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom;exportMenu.IsOpen=true;};
    }
    private void RefreshPageHeading()
    {
        if(sheetArea.Parent is Border frame){frame.BorderThickness=new Thickness(activePage=="reports"?0:1);frame.Background=Ui.Color(activePage=="reports"?"#F4F6FA":"#FFFFFF");}
        pageTitle.Text=activePage=="reports"?"Reports / "+CurrentReportName:PageName(activePage);
        var party=(partySelector.SelectedItem as WorksheetParty)?.Name??"All parties";
        var agent=(agentSelector.SelectedItem as AgentMasterEntry)?.Name??"All agents";
        title.FontSize=12;title.Text=activePage=="form"?$"Party: {party}  ·  Agent: {agent}":activePage=="parties"?"All parties":activePage=="agents"?"All agents":"";
        title.ToolTip=null;if(pageHeading!=null)pageHeading.Visibility=title.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
        exportButton.IsEnabled=grid!=null;exportButton.ToolTip=grid==null?"This page has no entries to export yet.":"Export the visible entries from this page.";
    }
    private ReportBundle BuildPageExport()
    {
        if(grid==null)throw new ArgumentException("This page has no entries to export yet.");
        var table=grid.PageSnapshot(activePage=="reports"?CurrentReportName:PageName(activePage));
        return new(1,"Current page",Guid.Empty,Guid.Empty,activePage=="reports"?currentCompanyReport?.Subject??"Reports":title.Text,DateOnly.FromDateTime(DateTime.Today),DateTimeOffset.Now,Guid.NewGuid(),0,"Page export",[table],[]);
    }
    internal static ReportBundle PdfPageBands(ReportBundle report)
    {
        return report;
    }
    private void ExportCurrentPage(bool pdf)
    {
        try
        {
            RequireSaved();var report=BuildPageExport();var page=activePage=="reports"?CurrentReportName:PageName(activePage);var extension=pdf?"pdf":"xlsx";var folder=Path.Combine(AppContext.BaseDirectory,"Exports",page);Directory.CreateDirectory(folder);
            var raw=page+" - "+(activePage=="form"?((partySelector.SelectedItem as WorksheetParty)?.Name??"All parties")+" - "+((agentSelector.SelectedItem as AgentMasterEntry)?.Name??"All agents"):activePage=="reports"?currentCompanyReport?.Subject??"All records":"All records");
            var safe=new string(raw.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());safe=safe[..Math.Min(145,safe.Length)];
            var picker=new SaveFileDialog{Filter=pdf?"PDF file (*.pdf)|*.pdf":"Excel workbook (*.xlsx)|*.xlsx",FileName=$"{safe} - {report.AsOf:yyyy-MM-dd}.{extension}",InitialDirectory=folder,AddExtension=true,DefaultExt=extension};
            if(picker.ShowDialog(this)!=true)return;
            busy=true;
            if(pdf)ReportPdf.Save(PdfPageBands(report),picker.FileName);else SavePageExcel(report,picker.FileName);
            status.Text=page+" exported";
            try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(picker.FileName){UseShellExecute=true});}catch(Exception){MessageBox.Show(this,"File saved successfully:\n"+picker.FileName,"Export saved");}
        }
        catch(Exception ex){Error(ex.Message);}
        finally{busy=false;}
    }
    private static void SavePageExcel(ReportBundle report,string path)
    {
        var pending=path+".partial";
        try{using(var file=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(ReportWorkflow.ExportPageWorkbook(report));file.Flush(true);}File.Move(pending,path,true);}
        finally{if(File.Exists(pending))File.Delete(pending);}
    }
    internal void CheckPageExport()
    {
        void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        Check(!partySelector.Items.Cast<WorksheetParty>().Any(party=>party.IsUnassigned),"Unassigned must not appear in the party dropdown.");
        Check(pageTitle.Text==PageName(activePage)&&!ReferenceEquals(pageTitle.Parent,title.Parent),"Page title must be separate from the applied-filter summary.");
        Check(exportMenu.Items.Cast<MenuItem>().Select(item=>item.Header?.ToString()).SequenceEqual(new[]{"Export to Excel","Export to PDF"}),"Export menu must contain exactly Excel and PDF actions.");
        var report=BuildPageExport();Check(report.Tables.Count==1&&report.Tables[0].Name==PageName(activePage)&&report.Tables[0].Rows.Count==grid!.Table.Items.Cast<GridRow>().Count(row=>row["_blank"]!="1"),"Page export must contain only currently visible rows.");
        if(activePage=="form")
        {
            Check(report.Tables[0].Columns.Count==21&&report.Tables[0].Columns[0].Key=="F1"&&grid!.Table.FrozenColumnCount==3,"Party must be the first frozen export/register column.");
            var bands=PdfPageBands(report);Check(bands.Tables.Count==1&&bands.Tables[0].Columns.Count==21,"PDF must retain every register column together on one wide page.");
            var evidence=Path.Combine(AppContext.BaseDirectory,"TestEvidence");Directory.CreateDirectory(evidence);var excel=Path.Combine(evidence,"Invoice Register - Current Page.xlsx");var pdf=Path.Combine(evidence,"Invoice Register - Current Page.pdf");SavePageExcel(report,excel);ReportPdf.Save(bands,pdf);Check(File.Exists(excel)&&File.ReadAllText(pdf).StartsWith("%PDF-1.4"),"Both page export formats must produce valid files.");
        }
    }
}
