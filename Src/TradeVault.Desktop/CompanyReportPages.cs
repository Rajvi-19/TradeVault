using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

public sealed partial class MainWindow
{
    private CompanyReportKind reportKind=CompanyReportKind.AccountDetails;
    private CompanyReport? currentCompanyReport;
    private readonly Dictionary<CompanyReportKind,string> reportSelections=[];
    private sealed record ReportChoice(string Id,string Name){public override string ToString()=>Name;}
    private string CurrentReportName=>currentCompanyReport?.Table.Name??"Reports";
    internal void PreviewCompanyReport(CompanyReportKind kind,string id){reportKind=kind;reportSelections[kind]=id;OpenPage("reports");}
    internal void CheckCompanyReportPages()
    {
        void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        var party=store.CreateParty("SYNTHETIC statement buyer");var agent=store.CreateAgent("SYNTHETIC statement agent");
        var id=store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"STATEMENT-PI",new(2026,6,1),agent.Id));var snapshot=store.Load("form",party.Id);var invoice=snapshot.Rows.Single(r=>r.GetValueOrDefault("_id")==id);invoice["F5"]="10000";invoice["F6"]="7000";invoice["F7"]="6500";invoice["F9"]="STATEMENT-IN";invoice["F10"]="2026-06-02";invoice["F17"]="2026-06-20";store.Save("form",snapshot.Revision,snapshot.Rows,party.Id);
        store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,7,1),8000,"Statement incoming",agent.Id);store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,7,2),5000,"Statement remittance");
        foreach(var kind in new[]{CompanyReportKind.AccountDetails,CompanyReportKind.PartyLedger,CompanyReportKind.AgentLedger})
        {
            PreviewCompanyReport(kind,kind==CompanyReportKind.AgentLedger?agent.Id:party.Id.ToString());
            Check(grid!=null&&grid.Table.IsReadOnly&&grid.Table.Columns.Count==CompanyReports.Columns(kind).Count,"Report reference columns must be read-only.");
            var export=BuildPageExport();Check(export.Tables[0].Name==currentCompanyReport!.Table.Name&&export.PartyName==currentCompanyReport.Subject,"Report exports must retain report and account identity.");
            if(kind==CompanyReportKind.AccountDetails)
            {
                var row=(GridRow)grid!.Table.Items[0];Check(row["O"]=="5000.00"&&row["E"]=="7000.00"&&row["F"]=="0.00"&&row["J"]=="1000.00"&&row["H"]==""&&row["K"]=="","FIFO results and unavailable shipping inputs must remain distinct.");
                Check(export.Tables[0].Columns.Single(c=>c.Key=="B").Type=="text"&&export.Tables[0].Columns.Single(c=>c.Key=="N").Type=="date","Invoice labels must not be exported as dates.");
                Check(PdfPageBands(export).Tables.All(t=>t.Columns.Any(c=>c.Key=="B")),"PDF bands must repeat invoice identity.");
            }
            ReloadSaved();Check(currentCompanyReport?.Table.Name==export.Tables[0].Name,"Reload must preserve report selection.");
        }
        var content=(FrameworkElement)Content;content.Measure(new(1000,600));content.Arrange(new(0,0,1000,600));content.UpdateLayout();
        var point=grid!.TranslatePoint(new(0,0),content);Check(point.Y>=0&&point.Y+grid.ActualHeight<=600,"Report table must stay inside the compact viewport.");
        Navigate("form");
    }
    private void LoadCompanyReportPage()
    {
        sheetArea.Children.Clear();grid=null;currentCompanyReport=null;
        var root=new DockPanel();var header=new StackPanel{Margin=new(12,10,12,8)};
        var tabs=new WrapPanel();
        foreach(var (kind,label) in new[]{(CompanyReportKind.AccountDetails,"Account Details"),(CompanyReportKind.PartyLedger,"Party Ledger"),(CompanyReportKind.AgentLedger,"Agent Ledger")})
        {
            var button=Ui.Button(label,()=>Run(()=>{reportKind=kind;LoadCompanyReportPage();RefreshPageHeading();}),"QuietButton");button.Padding=new(14,7,14,7);button.Margin=new(0,0,8,8);button.FontWeight=FontWeights.SemiBold;button.Background=Ui.Color(reportKind==kind?"#E8EFFD":"#FFFFFF");button.Foreground=Ui.Color(reportKind==kind?"#2458D3":"#52627A");tabs.Children.Add(button);
        }
        header.Children.Add(tabs);
        var filter=new DockPanel();
        var picker=new ComboBox{Width=290,MinHeight=32,IsEditable=false,IsTextSearchEnabled=true,SelectedValuePath="Id"};picker.SetResourceReference(StyleProperty,"FilterSelectorStyle");TextSearch.SetTextPath(picker,"Name");
        var choices=reportKind==CompanyReportKind.AgentLedger?store.GetAgents().Select(a=>new ReportChoice(a.Id,a.Name)).ToList():store.GetParties().Where(p=>!p.IsUnassigned).Select(p=>new ReportChoice(p.Id.ToString(),p.Name)).ToList();picker.ItemsSource=choices;
        var nameText=new FrameworkElementFactory(typeof(TextBlock));nameText.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Name"));picker.ItemTemplate=new DataTemplate{VisualTree=nameText};
        picker.SelectedItem=choices.FirstOrDefault(c=>c.Id==reportSelections.GetValueOrDefault(reportKind))??choices.FirstOrDefault();
        var selector=new StackPanel{Orientation=Orientation.Horizontal};var labelText=Ui.Text(reportKind==CompanyReportKind.AgentLedger?"Agent":"Party",12,"#52627A",true);labelText.Margin=new(0,0,10,0);selector.Children.Add(labelText);selector.Children.Add(picker);filter.Children.Add(selector);header.Children.Add(filter);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        System.Windows.Automation.AutomationProperties.SetName(picker,reportKind==CompanyReportKind.AgentLedger?"Report agent":"Report party");
        picker.SelectionChanged+=(_,_)=>Run(()=>{if(picker.SelectedItem is ReportChoice choice){reportSelections[reportKind]=choice.Id;LoadCompanyReportPage();RefreshPageHeading();}});
        sheetArea.Children.Add(root);
        if(picker.SelectedItem is not ReportChoice selected)
        {
            root.Children.Add(Ui.Text(reportKind==CompanyReportKind.AgentLedger?"Add an agent in Agent Master to view this report.":"Add a party in Party Master to view this report.",14,"#64748B"));RefreshPageHeading();return;
        }
        reportSelections[reportKind]=selected.Id;
        try
        {
            currentCompanyReport=new CompanyReports(store).Build(reportKind,selected.Id);
            var report=currentCompanyReport;
            if(reportKind!=CompanyReportKind.AccountDetails)
            {
                var footer=new WrapPanel{Margin=new(12,8,12,10)};
                foreach(var item in report.Summary){var panel=new StackPanel{Margin=new(0,0,32,4)};panel.Children.Add(Ui.Text(item.Key,11,"#64748B"));panel.Children.Add(Ui.Text(item.Value,14,"#18243B",true));footer.Children.Add(panel);}
                DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            }
            if(report.Notes.Count>0){var notice=Ui.Text(report.Notes[0],11,"#9A6700");notice.Margin=new(12,0,12,8);DockPanel.SetDock(notice,Dock.Top);root.Children.Add(notice);}
            var rows=new ObservableCollection<GridRow>(report.Table.Rows.Select(data=>{var row=new GridRow();foreach(var cell in data)row[cell.Key]=cell.Value;return row;}));
            grid=new ExcelGrid(report.Table.Columns.Select(c=>new Field(c.Key,c.Label,Width:c.Key=="A"?70:c.Type=="integer"?90:c.Type=="date"?140:c.Type=="text"?380:170,DataType:c.Type)).ToArray(),rows,true,frozenColumns:reportKind==CompanyReportKind.AgentLedger?2:1);
            root.Children.Add(grid);
        }
        catch(Exception ex){currentCompanyReport=null;grid=null;root.Children.Add(Ui.Text(ex.Message,13,"#B42318"));}
        RefreshPageHeading();
    }
}


