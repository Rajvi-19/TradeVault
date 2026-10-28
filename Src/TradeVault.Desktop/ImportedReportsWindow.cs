using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using TradeVault.Application;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
internal sealed class ImportedReportsWindow:Window
{
    private readonly ReportWorkflow service;
    private readonly DataGrid table=new(){AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Single,SelectionUnit=DataGridSelectionUnit.FullRow,ColumnHeaderHeight=42,RowHeight=44};
    private readonly TextBox search=new(){Width=260,ToolTip="Search party, report date or source",Margin=new(0,0,12,0)};
    private readonly ComboBox filter=new(){ItemsSource=new[]{"All statuses","New","Reviewed","Needs correction"},SelectedIndex=0,Width=160};
    private readonly TextBlock summary=Ui.Text("",12,"#64748B");
    private readonly TextBlock selection=Ui.Text("Select a submission to view its details.",13,"#64748B");
    private readonly TextBlock notice=Ui.Text("",12,"#B42318");
    private readonly TextBlock empty=Ui.Text("No reports yet. Import a .xlsx file to start reviewing submissions.",14,"#64748B");
    private readonly Button open,review;
    private List<ImportedSubmission> items=[];
    public ImportedReportsWindow(Window owner,ReportWorkflow service)
    {
        this.service=service;if(owner.IsVisible)Owner=owner;Width=1180;Height=750;MinWidth=900;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new(22)};
        var heading=new DockPanel{Margin=new(0,0,0,16)};
        var import=Ui.Button("Import report",Import,"PrimaryButton","\uE8B5");DockPanel.SetDock(import,Dock.Right);heading.Children.Add(import);
        var brand=new StackPanel{Orientation=Orientation.Horizontal};var logo=Ui.BrandIcon(36);logo.Margin=new(0,0,12,0);brand.Children.Add(logo);var titles=new StackPanel();titles.Children.Add(Ui.Text("Operator reports",24,"#18243B",true));titles.Children.Add(Ui.Text("Review submitted reports without changing your working records.",12,"#64748B"));brand.Children.Add(titles);heading.Children.Add(brand);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        summary.Margin=new(0,0,0,16);DockPanel.SetDock(summary,Dock.Top);root.Children.Add(summary);
        var tools=new DockPanel{Margin=new(0,0,0,12)};
        var actions=new StackPanel{Orientation=Orientation.Horizontal};
        open=Ui.Button("Open report",()=>Run(()=>{if(table.SelectedItem is ImportedSubmission item)WorkflowWindows.ReportView(this,item.Report,"Submitted report · read-only").ShowDialog();}),null,"\uE8A5");open.Margin=new(0,0,8,0);actions.Children.Add(open);
        review=Ui.Button("Update review",()=>Run(Review),null,"\uE70F");actions.Children.Add(review);DockPanel.SetDock(actions,Dock.Right);tools.Children.Add(actions);
        var filters=new StackPanel{Orientation=Orientation.Horizontal};var searchLabel=Ui.Text("Search",12,"#64748B");searchLabel.Margin=new(0,0,8,0);filters.Children.Add(searchLabel);filters.Children.Add(search);filters.Children.Add(filter);tools.Children.Add(filters);DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        var foot=new StackPanel();selection.Margin=new(0,12,0,6);foot.Children.Add(selection);notice.Margin=new(0,0,0,6);foot.Children.Add(notice);
        foot.Children.Add(Ui.Text("Frozen submissions • All revisions retained • Integrity checked; sender identity verified through your handover process",11,"#64748B"));DockPanel.SetDock(foot,Dock.Bottom);root.Children.Add(foot);
        void Column(string title,string path,double width,string? format=null)=>table.Columns.Add(new DataGridTextColumn{Header=title,Width=width,Binding=new Binding(path){StringFormat=format}});
        Column("PARTY","Report.PartyName",250);Column("AS OF","Report.AsOf",110,"{0:dd-MM-yyyy}");Column("REVISION","Report.Revision",85);Column("MODE","Report.Mode",140);Column("STATUS","Status",150);Column("RECEIVED","ImportedAt",155,"{0:dd-MM-yyyy HH:mm}");
        var surface=new Grid();surface.Children.Add(table);empty.HorizontalAlignment=HorizontalAlignment.Center;empty.VerticalAlignment=VerticalAlignment.Center;empty.Margin=new(28);surface.Children.Add(empty);root.Children.Add(Ui.Card(surface));
        table.SelectionChanged+=(_,_)=>UpdateSelection();search.TextChanged+=(_,_)=>ApplyFilter();filter.SelectionChanged+=(_,_)=>ApplyFilter();
        System.Windows.Automation.AutomationProperties.SetName(search,"Search imported reports");System.Windows.Automation.AutomationProperties.SetName(filter,"Review status filter");
        Ui.WindowFrame(this,root,"Operator reports");Refresh();
    }
    private void Run(Action action){try{notice.Text="";notice.Foreground=Ui.Color("#B42318");action();}catch(Exception ex){notice.Text=ex.Message;}}
    private void Refresh()
    {
        items=service.Imports();summary.Text=$"{items.Count} submissions    •    {items.Count(i=>i.Status=="New")} new    •    {items.Count(i=>i.Status=="Reviewed")} reviewed    •    {items.Count(i=>i.Status=="Needs correction")} need correction";ApplyFilter();
    }
    private void ApplyFilter()
    {
        var text=search.Text.Trim();var status=filter.SelectedItem as string??"All statuses";
        var visible=items.Where(i=>(status=="All statuses"||i.Status==status)&&(text.Length==0||$"{i.Report.PartyName} {i.Report.AsOf:dd-MM-yyyy} {i.Report.SourceId} {i.Report.Mode}".Contains(text,StringComparison.OrdinalIgnoreCase))).ToList();
        table.ItemsSource=visible;empty.Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;
        empty.Text=items.Count==0?"No reports yet. Import a .xlsx file to start reviewing submissions.":"No reports match your search or status filter.";UpdateSelection();
    }
    private void UpdateSelection()
    {
        var item=table.SelectedItem as ImportedSubmission;open.IsEnabled=review.IsEnabled=item!=null;
        selection.Text=item==null?"Select a submission to view its details.":$"{item.Report.PartyName}  ·  Revision {item.Report.Revision}  ·  {item.Report.Tables.Sum(t=>t.Rows.Count)} report rows\nSource {item.Report.SourceId}  |  Submission {item.Report.SubmissionId}";
    }
    private void Import()=>Run(()=>
    {
        var picker=new OpenFileDialog{Filter="TradeVault Excel report (*.xlsx)|*.xlsx",InitialDirectory=service.Root};if(picker.ShowDialog(this)!=true)return;
        var preview=service.PreviewPackage(picker.FileName);WorkflowWindows.ReportView(this,preview.Report,"Import preview · verify source and totals").ShowDialog();
        if(MessageBox.Show(this,$"Import {preview.Report.PartyName}, revision {preview.Report.Revision}? Confirm the source through your normal handover process.","Import report",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        service.Import(picker.FileName,preview.Hash);Refresh();notice.Foreground=Ui.Color("#247653");notice.Text="Report imported. The original submission is preserved.";
    });
    private void Review()
    {
        if(table.SelectedItem is not ImportedSubmission item)return;
        var dialog=new Window{Owner=this,Width=540,Height=420,WindowStartupLocation=WindowStartupLocation.CenterOwner};var form=new StackPanel{Margin=new(24)};
        form.Children.Add(Ui.Text("Review submission",23,"#18243B",true));form.Children.Add(Ui.Text(item.Report.PartyName+" · Revision "+item.Report.Revision,12,"#64748B"));
        var state=new ComboBox{ItemsSource=new[]{"New","Reviewed","Needs correction"},SelectedItem=item.Status,Margin=new(0,16,0,12),MinHeight=34};form.Children.Add(state);
        var note=new TextBox{Text=item.ReviewNote,MaxLength=4000,AcceptsReturn=true,Height=140,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};form.Children.Add(note);
        form.Children.Add(Ui.Text("Your note is stored separately from the submitted report.",11,"#64748B"));var error=Ui.Text("",12,"#B42318");form.Children.Add(error);
        var save=Ui.Button("Save review",()=>{try{service.Review(item,(string)state.SelectedItem,note.Text);dialog.DialogResult=true;}catch(Exception ex){error.Text=ex.Message;}},"PrimaryButton");save.Margin=new(0,12,0,0);form.Children.Add(save);Ui.WindowFrame(dialog,form,"Review submission");if(dialog.ShowDialog()==true)Refresh();
    }
    internal void PreviewReview(){table.SelectedIndex=0;Review();}
}
