using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TradeVault.Infrastructure;
using TradeVault.Application;
namespace TradeVault.Desktop;

public sealed class MainWindow : Window
{
    private WorksheetController controller;
    private readonly WorkbookStore store;
    private readonly LoginSession session;
    private readonly ReportWorkflow workflow;
    private bool generatedView;
    private bool busy;
    private readonly ComboBox partySelector=new(){Width=230,MinHeight=30,DisplayMemberPath="Name"};
    private readonly Button assignButton;
    private bool changingParty;
    private readonly TextBlock status=Ui.Text("All changes saved",11,"#64748B");
    private readonly TextBlock title=Ui.Text("Invoice register",18,"#18243B",true);
    private readonly TextBlock count=Ui.Text("0 records",11,"#64748B");
    private readonly TextBlock revision=Ui.Text("",11,"#64748B");
    private readonly TextBox opening=new(){Text="0.00",Width=100,MinHeight=30,Padding=new(8,4,8,4)};
    private readonly TextBox terms=new(){Text="90",Width=54,MinHeight=30,Padding=new(8,4,8,4)};
    private readonly DateCellEditor asOf=new(){Value=DateTime.Today.ToString("yyyy-MM-dd"),Width=145,MinHeight=30};
    private System.Windows.Controls.Calendar? cutoffCalendar;
    private System.Windows.Controls.Primitives.Popup? cutoffPopup;
    private readonly StackPanel openingGroup=new(){Orientation=Orientation.Horizontal};
    private readonly StackPanel termsGroup=new(){Orientation=Orientation.Horizontal};
    private readonly Dictionary<string,Button> nav=new();
    private readonly Button saveButton;
    private readonly Button deleteButton;
    private readonly Border notification;
    private readonly TextBlock message=Ui.Text("",12,"#B42318");
    private ExcelGrid? grid;
    private bool dirty,refreshing,compact=true;
    private readonly Grid sheetArea=new();

    public MainWindow(WorkbookStore store,LoginSession session,string path)
    {
        this.store=store;this.session=session;workflow=new(store,session);controller=new(store,session);var startup=Ui.WorkspaceSize(SystemParameters.WorkArea);Width=startup.Width;Height=startup.Height;MinWidth=Math.Min(1000,startup.Width);MinHeight=Math.Min(600,startup.Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new DockPanel{Background=Ui.Color("#F4F6FA")};
        var header=new DockPanel{Background=Ui.Color("#111E35"),Height=42};
        var windowControls=Ui.WindowControls(this);DockPanel.SetDock(windowControls,Dock.Right);header.Children.Add(windowControls);
        var account=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,8,0)};
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(account,true);
        account.Children.Add(Ui.Text(session.Username+" · "+session.Role,12,"#E2E8F0"));
        var backup=CompactButton("Backups",()=>Run(()=>{RequireSaved();WorkflowWindows.Backups(this,store,session);}),"\uE74E");backup.Foreground=Brushes.White;account.Children.Add(backup);
        var logout=Ui.Button("Sign out",()=>Run(()=>{if(Discard()){dirty=false;Close();}}),"QuietButton","\uE8AC");logout.Foreground=Ui.Color("#FFFFFF");logout.Margin=new(12,0,0,0);logout.MinHeight=28;logout.Padding=new(10,4,10,4);account.Children.Add(logout);
        DockPanel.SetDock(account,Dock.Right);header.Children.Add(account);
        var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(14,0,0,0)};
        var logo=Ui.BrandIcon(26);logo.Margin=new(0,0,9,0);brand.Children.Add(logo);
        brand.Children.Add(Ui.Text("TradeVault",17,"#FFFFFF",true));var offline=Ui.Text("  /  Offline · USD",11,"#A5B6CE");brand.Children.Add(offline);header.Children.Add(brand);
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);

        var tabs=new DockPanel{Background=Brushes.White,Height=40};
        saveButton=Ui.Button("Save changes",()=>Run(Save),"PrimaryButton","\uE74E");saveButton.MinHeight=30;saveButton.Padding=new(12,4,12,4);saveButton.Margin=new(0,4,10,4);DockPanel.SetDock(saveButton,Dock.Right);tabs.Children.Add(saveButton);
        var navigation=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(6,0,0,0)};
        foreach(var (name,label,icon) in new[]{("form","Invoice register","\uE8A5"),("details","Account details","\uE8D4"),("ledger","Debit & credit","\uE8AB")})
        {
            var b=Ui.Button(label,()=>Run(()=>{if(Discard())Open(name);}),"QuietButton",icon);b.Padding=new(12,5,12,5);b.MinHeight=32;b.Margin=new(2,3,2,3);nav.Add(name,b);navigation.Children.Add(b);
        }
        if(session.Role=="Owner")
        {
            var imports=Ui.Button("Operator reports",()=>Run(()=>{RequireSaved();WorkflowWindows.Inbox(this,workflow);}),"QuietButton","\uE8B5");imports.MinHeight=32;imports.Padding=new(10,5,10,5);navigation.Children.Add(imports);
        }
        tabs.Children.Add(new ScrollViewer{Content=navigation,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});DockPanel.SetDock(tabs,Dock.Top);root.Children.Add(tabs);

        var footer=new DockPanel{Height=24,Margin=new(12,0,12,0)};var build=Ui.Text(Ui.BuildLabel,10,"#64748B");build.Margin=new(16,0,0,0);DockPanel.SetDock(build,Dock.Right);footer.Children.Add(build);DockPanel.SetDock(revision,Dock.Right);footer.Children.Add(revision);footer.Children.Add(status);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var heading=new DockPanel{Margin=new(12,8,12,6)};DockPanel.SetDock(count,Dock.Right);heading.Children.Add(count);
        var partyTools=new StackPanel{Orientation=Orientation.Horizontal};
        var partyLabel=Ui.Text("Party",12,"#52627A");partyLabel.Margin=new(0,0,8,0);partyTools.Children.Add(partyLabel);partyTools.Children.Add(partySelector);
        var create=CompactButton("Create party",()=>Run(CreateParty),"\uE710");partyTools.Children.Add(create);
        assignButton=CompactButton("Assign worksheet",()=>Run(AssignWorksheet),"\uE8B5");partyTools.Children.Add(assignButton);
        DockPanel.SetDock(partyTools,Dock.Right);heading.Children.Add(partyTools);heading.Children.Add(title);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);

        var options=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};
        termsGroup.Margin=new(0,0,14,0);var termLabel=Ui.Text("New terms",12,"#52627A");termLabel.Margin=new(0,0,6,0);termsGroup.Children.Add(termLabel);termsGroup.Children.Add(terms);termsGroup.Children.Add(Ui.Text(" days",11,"#64748B"));options.Children.Add(termsGroup);
        openingGroup.Margin=new(0,0,14,0);var openLabel=Ui.Text("Opening USD",12,"#52627A");openLabel.Margin=new(0,0,6,0);openingGroup.Children.Add(openLabel);openingGroup.Children.Add(opening);options.Children.Add(openingGroup);
        var dateLabel=Ui.Text("As of",12,"#52627A");dateLabel.Margin=new(0,0,6,0);options.Children.Add(dateLabel);options.Children.Add(asOf);
        var calendarButton=Ui.Button("",()=>{},"QuietButton","\uE787");calendarButton.Width=32;calendarButton.MinHeight=30;
        System.Windows.Automation.AutomationProperties.SetName(calendarButton,"Choose reporting cutoff date");
        var calendar=new System.Windows.Controls.Calendar();var calendarPopup=new System.Windows.Controls.Primitives.Popup{Child=new Border{Background=Brushes.White,BorderBrush=Ui.Color("#D8E0EB"),BorderThickness=new(1),Child=calendar},PlacementTarget=calendarButton,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom,StaysOpen=false};
        cutoffCalendar=calendar;cutoffPopup=calendarPopup;
        calendarButton.Click+=(_,_)=>{calendar.SelectedDate=DateOnly.TryParseExact(asOf.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var selected)?selected.ToDateTime(TimeOnly.MinValue):DateTime.Today;calendar.DisplayDate=calendar.SelectedDate.Value;calendarPopup.IsOpen=true;};
        calendar.SelectedDatesChanged+=(_,_)=>{if(calendarPopup.IsOpen&&calendar.SelectedDate is {} selected){asOf.Value=selected.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);calendarPopup.IsOpen=false;Calculate();}};
        options.Children.Add(calendarButton);
        var calc=CompactButton("Recalculate",Calculate,"\uE9D9");options.Children.Add(calc);
        options.Children.Add(new Border{Width=1,Height=22,Background=Ui.Color("#D8E0EB"),Margin=new(10,0,2,0)});
        options.Children.Add(CompactButton("Reload",()=>Run(()=>{if(Discard())Open(controller.Name);}),"\uE72C"));
        deleteButton=CompactButton("Delete",()=>Run(Delete),"\uE74D");deleteButton.Foreground=Ui.Color("#A63E3E");options.Children.Add(deleteButton);
        var density=CompactButton("Comfortable rows",()=>{compact=!compact;if(grid!=null)grid.Table.RowHeight=compact?28:36;},"\uE8A4");density.ToolTip="Switch between 28 and 36 pixel rows";options.Children.Add(density);
        var optionBar=new Border{Background=Brushes.White,BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(1),Padding=new(10,5,10,5),Margin=new(10,0,10,4),Child=options};DockPanel.SetDock(optionBar,Dock.Top);root.Children.Add(optionBar);
        var workflows=new WrapPanel{Margin=new(10,0,10,4)};
        workflows.Children.Add(CompactButton("Report setup",()=>Run(()=>{RequireSaved();RequireParty();if(WorkflowWindows.Configure(this,workflow,controller.PartyId))Open(controller.Name);}),"\uE713"));
        workflows.Children.Add(CompactButton("Export Excel",()=>ExportReports(),"\uE896"));
        workflows.Children.Add(CompactButton("Preview / Print",()=>Run(()=>{RequireSaved();RequireParty();Options();WorkflowWindows.ReportView(this,workflow.Build(controller.PartyId,controller.AsOf,workflow.Mapping(controller.PartyId)!=null)).ShowDialog();}),"\uE749"));
        workflows.Children.Add(CompactButton("Receipts",()=>Run(()=>{RequireSaved();RequireParty();WorkflowWindows.Receipts(this,workflow,controller.PartyId);Open(controller.Name);}),"\uE8D4"));
        workflows.Children.Add(CompactButton("Reminders",()=>Run(()=>{RequireSaved();RequireParty();Options();WorkflowWindows.Reminders(this,workflow,controller.PartyId,controller.AsOf);}),"\uE787"));
        DockPanel.SetDock(workflows,Dock.Top);root.Children.Add(workflows);
        notification=new Border{Background=Ui.Color("#FFF2F0"),BorderBrush=Ui.Color("#F3C8C3"),BorderThickness=new(1),Padding=new(10,6,10,6),Margin=new(10,0,10,4),Child=message,Visibility=Visibility.Collapsed};DockPanel.SetDock(notification,Dock.Top);root.Children.Add(notification);
        var sheet=new Border{Background=Brushes.White,BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(1),Margin=new(10,0,10,4),Child=sheetArea};root.Children.Add(sheet);
        opening.TextChanged+=(_,_)=>{if(!refreshing&&!generatedView)MarkDirty();};terms.TextChanged+=(_,_)=>{if(!refreshing&&!generatedView)MarkDirty();};asOf.TextChanged+=(_,_)=>{if(!refreshing&&grid!=null&&!generatedView)MarkDirty();};asOf.LostKeyboardFocus+=(_,_)=>Calculate();
        System.Windows.Automation.AutomationProperties.SetName(opening,"Opening balance in USD");System.Windows.Automation.AutomationProperties.SetName(terms,"Payment terms in calendar days");System.Windows.Automation.AutomationProperties.SetName(asOf,"Report as of date");
        Ui.WindowFrame(this,root,"TradeVault · "+session.Role+" workspace",embeddedControls:true);
        ToolTipService.SetToolTip(revision,"Storage: "+path);Closing+=(_,e)=>{if(busy||!Discard())e.Cancel=true;};
        InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Save)),Key.S,ModifierKeys.Control));InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Add)),Key.N,ModifierKeys.Control));
        System.Windows.Automation.AutomationProperties.SetName(partySelector,"Select party");
        RefreshParties(Guid.Empty);
        partySelector.SelectionChanged+=(_,_)=>SwitchParty();
        Open("form");
        Loaded+=(_,_)=>Run(ShowOpeningReminders);
    }
    internal void CheckNavigationLayout()
    {
        var content=(FrameworkElement)Content;content.Measure(new(1366,728));content.Arrange(new(0,0,1366,728));content.UpdateLayout();
        foreach(var name in new[]{"form","details","ledger"})
        {
            var button=nav[name];var point=button.TranslatePoint(new(0,0),content);
            if(button.ActualWidth<=0||point.Y<0||point.Y+button.ActualHeight>728||point.X<0||point.X+button.ActualWidth>1366)throw new InvalidOperationException("Worksheet navigation is outside the visible workspace: "+name);
        }
    }
    private static Button CompactButton(string text,Action action,string glyph){var b=Ui.Button(text,action,"QuietButton",glyph);b.MinHeight=30;b.Padding=new(9,4,9,4);b.Margin=new(4,0,0,0);return b;}
    private void RequireSaved(){if(dirty)throw new ArgumentException("Save or reload your current changes first.");}
    private void ShowOpeningReminders()
    {
        int overdue=0,soon=0,unavailable=0,configured=0;
        foreach(var party in store.GetParties(session).Where(p=>!p.IsUnassigned&&workflow.Mapping(p.Id)!=null))
        {
            configured++;try{var reminders=workflow.Reminders(party.Id,DateOnly.FromDateTime(DateTime.Today));overdue+=reminders.Count(r=>r.Status=="Overdue");soon+=reminders.Count(r=>r.Status=="Due within 30 days");}
            catch(TradeVault.Domain.BusinessRuleException){unavailable++;}
        }
        if(configured>0)status.Text=$"{overdue} overdue · {soon} due soon · {unavailable} party report(s) need validation · open Reminders for the selected party";
    }
    private async void ExportReports()
    {
        try{RequireSaved();RequireParty();Options();var party=controller.PartyId;var date=controller.AsOf;var generated=workflow.Mapping(party)!=null;busy=true;((UIElement)Content).IsEnabled=false;status.Text="Exporting reports…";
            var package=await Task.Run(()=>workflow.ExportExcel(party,date,generated));status.Text="Reports exported";try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(package){UseShellExecute=true});}catch(Exception){MessageBox.Show(this,"Excel report saved successfully. No spreadsheet application could open it automatically. Open this file manually:\n"+package,"Report saved");}
        }catch(Exception ex){Error(ex.Message);}finally{busy=false;((UIElement)Content).IsEnabled=true;}
    }
    private void RequireParty(){if(controller.PartyId==Guid.Empty)throw new ArgumentException("Select a named party first.");}
    private void ChangeSelectedTerms()
    {
        if(controller.Name!="form"||grid?.Table.CurrentItem is not GridRow row||!controller.Rows.Contains(row))throw new ArgumentException("Select an invoice row first.");
        var dialog=new Window{Owner=this,Width=420,Height=230,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(18)};
        panel.Children.Add(Ui.Text("Terms for "+row["F9"],18,"#18243B",true));var input=new TextBox{Text=row["_terms"],Margin=new(0,12,0,12)};panel.Children.Add(input);
        panel.Children.Add(Ui.Button("Apply to this invoice",()=>Run(()=>{if(!int.TryParse(input.Text,out var days)||days<0||days>3650)throw new ArgumentException("Terms must be 0–3650 days.");row["_terms"]=days.ToString();MarkDirty();Calculate();dialog.Close();}),"PrimaryButton"));Ui.WindowFrame(dialog,panel,"Invoice terms");dialog.ShowDialog();
    }
    private void Run(Action action){try{action();}catch(Exception ex){Error(ex.Message);}}
    private void Error(string text){message.Text=text;notification.Visibility=Visibility.Visible;status.Text="Action needs attention";status.Foreground=Ui.Color("#B42318");}
    private void MarkDirty(){dirty=true;saveButton.IsEnabled=true;status.Text="● Unsaved changes";status.Foreground=Ui.Color("#9A6700");}
    private void Watch(GridRow row)=>row.Changed+=_=>{if(!refreshing)MarkDirty();};
    private bool Discard(){grid?.Table.CommitEdit();grid?.Table.CommitEdit(DataGridEditingUnit.Row,true);return !dirty||MessageBox.Show(this,"You have unsaved changes. Discard them and continue?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
    private void Open(string name,Guid? partyId=null)
    {
        var selected=partyId??controller.PartyId;var next=new WorksheetController(store,session);next.Load(name,selected);
        var nextGenerated=name!="form"&&workflow.Mapping(selected)!=null;
        if(nextGenerated)
        {
            // Both generated tabs use the register's cutoff, or the current unsaved report cutoff.
            if(selected==controller.PartyId)next.AsOf=controller.AsOf;
            else {var register=new WorksheetController(store,session);register.Load("form",selected);next.AsOf=register.AsOf;}
        }
        var nextReport=nextGenerated?workflow.Build(selected,next.AsOf,true):null;
        controller=next;refreshing=true;opening.Text=controller.Opening.ToString("0.00",CultureInfo.InvariantCulture);terms.Text=controller.Terms.ToString();asOf.Value=controller.AsOf.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);refreshing=false;
        foreach(var row in controller.Rows)Watch(row);
        title.Text=name switch{"form"=>"Invoice register","details"=>"Account details",_=>"Debit & credit"};
        title.ToolTip=name=="form"?"Difference Amount = Original Amount minus the contract source selected in Report setup. Blank until setup is complete.":null;
        foreach(var item in nav){item.Value.Background=Ui.Color(item.Key==name?"#E8EFFD":"#FFFFFF");item.Value.Foreground=Ui.Color(item.Key==name?"#2458D3":"#52627A");}
        openingGroup.Visibility=name=="form"?Visibility.Collapsed:Visibility.Visible;termsGroup.Visibility=name=="form"?Visibility.Visible:Visibility.Collapsed;
        generatedView=nextGenerated;
        if(generatedView){grid=WorkflowWindows.ReportGrid(nextReport!.Tables[name=="details"?0:1]);title.Text+=" · generated";}
        else grid=new ExcelGrid(controller.Fields,controller.Rows,promote:row=>{controller.Add(row);Watch(row);MarkDirty();UpdateRecords();});grid.Table.RowHeight=compact?28:36;grid.Edited+=Calculate;
        grid.Table.SelectedCellsChanged+=(_,_)=>UpdateRecords();
        sheetArea.Children.Clear();sheetArea.Children.Add(grid);UpdateRecords();notification.Visibility=Visibility.Collapsed;dirty=false;saveButton.IsEnabled=false;openingGroup.IsEnabled=!generatedView;
        status.Text=name=="form"&&workflow.Mapping(selected)==null?"Select a named party and complete Report setup to calculate Difference Amount.":"All changes saved";status.Foreground=Ui.Color("#64748B");revision.Text=$"Revision {controller.Revision} · Local storage";
        assignButton.Visibility=controller.PartyId==Guid.Empty?Visibility.Visible:Visibility.Collapsed;
        assignButton.IsEnabled=controller.Rows.Count>0;
    }
    private void UpdateRecords(){count.Text=generatedView?"Generated · read-only":$"{controller.Rows.Count} record{(controller.Rows.Count==1?"":"s")}";deleteButton.IsEnabled=!generatedView&&grid?.Table.CurrentItem is GridRow row&&controller.Rows.Contains(row);assignButton.IsEnabled=controller.PartyId==Guid.Empty&&controller.Rows.Count>0&&!dirty;}
    private void Add(){if(grid==null||generatedView)return;var row=controller.Add();Watch(row);MarkDirty();UpdateRecords();grid.Table.ScrollIntoView(row);grid.Table.CurrentCell=new DataGridCellInfo(row,grid.Table.Columns.First(c=>!c.IsReadOnly));grid.Table.Focus();grid.Table.BeginEdit();}
    private void Delete(){if(grid?.Table.CurrentItem is GridRow row&&controller.Rows.Contains(row)&&MessageBox.Show(this,"Remove the selected record? Save changes to confirm the deletion.","Delete record",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes){controller.Rows.Remove(row);MarkDirty();UpdateRecords();Calculate();}}
    private void Options(){if(!decimal.TryParse(opening.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||decimal.Round(value,2)!=value)throw new ArgumentException("Opening balance: enter USD with at most two decimals.");if(!int.TryParse(terms.Text,out var days)||days<0||days>3650)throw new ArgumentException("Payment terms must be 0–3650 days.");controller.Opening=value;controller.Terms=days;if(!DateOnly.TryParseExact(DateCellConverter.Normalize(asOf.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var reportDate))throw new ArgumentException("As of: enter a valid date (dd-MM-yyyy).");controller.AsOf=reportDate;}
    private void Calculate(){if(grid==null)return;try{Options();if(generatedView){var report=workflow.Build(controller.PartyId,controller.AsOf,true);grid=WorkflowWindows.ReportGrid(report.Tables[controller.Name=="details"?0:1]);sheetArea.Children.Clear();sheetArea.Children.Add(grid);status.Text="Generated report refreshed · "+controller.AsOf.ToString("dd-MM-yyyy");return;}refreshing=true;controller.Calculate();controller.Validate(false);notification.Visibility=Visibility.Collapsed;status.Text=dirty?"● Unsaved changes":"All changes saved";status.Foreground=Ui.Color(dirty?"#9A6700":"#64748B");}catch(Exception ex){Error(ex.Message);}finally{refreshing=false;}}
    private void Save(){if(grid==null||generatedView)return;if(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Correct invalid cells before saving.");Options();refreshing=true;try{controller.Save();dirty=false;saveButton.IsEnabled=false;notification.Visibility=Visibility.Collapsed;status.Foreground=Ui.Color("#247653");status.Text="Changes saved";revision.Text=$"Revision {controller.Revision} · Local storage";UpdateRecords();}finally{refreshing=false;}}
    internal void PreviewSheet(string name)=>Open(name);
    internal void PreviewParty(string name,Guid party){RefreshParties(party);Open(name,party);}
    internal void CheckWorkflowInteractions()
    {
        var party=store.CreateParty(session,"Workflow UI test");var input=new WorksheetController(store,session);input.Load("form",party.Id);var row=input.Add();row["F9"]="WF-UI-1";row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F17"]="2026-06-17";input.Save();
        workflow.Configure(party.Id,new("F6","F22",true,new(2026,1,1)));
        PreviewParty("details",party.Id);
        if(!generatedView||grid?.Table.IsReadOnly!=true||((GridRow)grid.Table.Items[0])["F"]!="10000.00")throw new InvalidOperationException("Generated report UI did not use register source.");
        Save();if(store.Load(session,"details",party.Id).Rows.Count!=0)throw new InvalidOperationException("Generated view overwrote legacy worksheet.");
        asOf.Value="2026-05-01";Calculate();if(grid.Table.Items.Count!=0)throw new InvalidOperationException("Generated historical view did not refresh.");
        Open("ledger");if(controller.AsOf!=new DateOnly(2026,5,1)||grid!.Table.Items.Cast<GridRow>().Any(r=>r["B"].Contains("WF-UI-1")))throw new InvalidOperationException("Switching generated tabs lost the reporting cutoff.");
        Open("details");if(controller.AsOf!=new DateOnly(2026,5,1))throw new InvalidOperationException("Returning to details lost the cutoff.");
        asOf.Value="2026-10-28";Calculate();
        cutoffPopup!.IsOpen=true;cutoffCalendar!.SelectedDate=new DateTime(2026,8,15);
        if(controller.AsOf!=new DateOnly(2026,8,15)||cutoffPopup.IsOpen)throw new InvalidOperationException("Calendar selection did not apply the reporting cutoff and close the popup.");
        asOf.Value="2026-10-28";Calculate();
        workflow.RecordReceipt(party.Id,new(2026,7,1),5000,"UI workflow test",[new(ReportWorkflow.InvoiceKey(party.Id,row.Snapshot()),"Bank",5000)]);
        PreviewParty("details",party.Id);if(((GridRow)grid.Table.Items[0])["F"]!="5000.00")throw new InvalidOperationException("Generated UI did not reflect dated receipt.");
        PreviewParty("form",Guid.Empty);
    }
    private void RefreshParties(Guid selected)
    {
        changingParty=true;
        try{var parties=store.GetParties(session);partySelector.ItemsSource=parties;partySelector.SelectedItem=parties.Single(p=>p.Id==selected);}
        finally{changingParty=false;}
    }
    private void SwitchParty()
    {
        if(changingParty||partySelector.SelectedItem is not WorksheetParty party)return;
        if(party.Id==controller.PartyId)return;
        try{if(Discard()){Open(controller.Name,party.Id);return;}}
        catch(Exception ex){Error(ex.Message);}
        RefreshParties(controller.PartyId);
    }
    private void CreateParty()
    {
        if(!Discard())return;
        var dialog=new PartyDialog(store,session){Owner=this};
        if(dialog.ShowDialog()!=true)return;
        RefreshParties(dialog.Result!.Id);Open(controller.Name,dialog.Result.Id);
    }
    private void AssignWorksheet()
    {
        if(controller.PartyId!=Guid.Empty||controller.Rows.Count==0)return;
        if(dirty)throw new ArgumentException("Save or reload your unassigned changes before assigning this worksheet.");
        var dialog=new PartyDialog(store,session,true){Owner=this};
        if(dialog.ShowDialog()!=true)return;
        if(MessageBox.Show(this,$"Move this entire worksheet and its opening balance to {dialog.Result!.Name}? Only do this if every row belongs to that party.","Assign worksheet",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        store.AssignUnassigned(session,controller.Name,dialog.Result.Id,controller.Revision);
        RefreshParties(dialog.Result.Id);Open(controller.Name,dialog.Result.Id);
    }
    internal void CheckInteractions()
    {
        Open("form");var before=controller.Rows.Count;Add();
        var row=controller.Rows.Last();row["F1"]="UI TEST ONLY";row["F9"]="UI-TEST";
        if(!saveButton.IsEnabled)throw new InvalidOperationException("Editing must enable Save.");
        Save();Open("form");
        if(controller.Rows.Count!=before+1||saveButton.IsEnabled)throw new InvalidOperationException("UI save/reload failed.");
        foreach(var name in new[]{"details","ledger","form"}){Open(name);Measure(new Size(1000,600));Arrange(new Rect(0,0,1000,600));UpdateLayout();}
    }
    internal double SheetTop=>grid?.TranslatePoint(new Point(0,0),(UIElement)Content).Y??999;
    internal void CheckPartyInteractions()
    {
        var a=store.CreateParty(session,"UI Party A");var b=store.CreateParty(session,"UI Party B");
        RefreshParties(a.Id);Open("ledger",a.Id);opening.Text="100";
        var first=controller.Add();first["B"]="UI A invoice";first["C"]="50";Save();
        RefreshParties(b.Id);Open("ledger",b.Id);
        if(controller.Rows.Count!=0||opening.Text!="0.00")throw new InvalidOperationException("Party B inherited Party A records.");
        opening.Text="500";var second=controller.Add();second["B"]="UI B invoice";second["C"]="10";Save();
        RefreshParties(b.Id);partySelector.SelectedItem=partySelector.Items.Cast<WorksheetParty>().Single(p=>p.Id==a.Id);
        if(controller.PartyId!=a.Id||opening.Text!="100.00"||controller.Rows.Single()["B"]!="UI A invoice")throw new InvalidOperationException("Party switching failed to restore isolated records.");
        Open("form",a.Id);var row=controller.Add();
        if(row["F1"]!=a.Name)throw new InvalidOperationException("Selected party was not defaulted.");
        row["F9"]="UI-PARTY-A";terms.Text="60";asOf.Value="2026-10-01";Save();
        Open("form",b.Id);if(controller.Rows.Count!=0)throw new InvalidOperationException("Invoice register leaked across parties.");
        if(terms.Text!="90")throw new InvalidOperationException("Party terms leaked.");
        var reopenedController=new WorksheetController(store,session);reopenedController.Load("form",a.Id);
        if(reopenedController.Terms!=60||reopenedController.AsOf!=new DateOnly(2026,10,1))throw new InvalidOperationException("Party settings were not persisted.");
        RefreshParties(Guid.Empty);Open("form",Guid.Empty);
    }
    private sealed class ActionCommand(Action execute):ICommand{public event EventHandler? CanExecuteChanged{add{}remove{}}public bool CanExecute(object? parameter)=>true;public void Execute(object? parameter)=>execute();}
}
