using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;

public sealed class MainWindow : Window
{
    private readonly WorksheetController controller;
    private readonly WorkbookStore store;
    private readonly LoginSession session;
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
        this.store=store;this.session=session;controller=new(store,session);Width=1440;Height=880;MinWidth=1000;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new DockPanel{Background=Ui.Color("#F4F6FA")};
        var header=new DockPanel{Background=Ui.Color("#111E35"),Height=42};
        var windowControls=Ui.WindowControls(this);DockPanel.SetDock(windowControls,Dock.Right);header.Children.Add(windowControls);
        var account=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,8,0)};
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(account,true);
        account.Children.Add(Ui.Text(session.Username+" · "+session.Role,12,"#E2E8F0"));
        var logout=Ui.Button("Sign out",()=>Run(()=>{if(Discard()){dirty=false;Close();}}),"QuietButton","\uE8AC");logout.Foreground=Ui.Color("#FFFFFF");logout.Margin=new(12,0,0,0);logout.MinHeight=28;logout.Padding=new(10,4,10,4);account.Children.Add(logout);
        DockPanel.SetDock(account,Dock.Right);header.Children.Add(account);
        var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(14,0,0,0)};
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
            var imports=Ui.Button("Operator reports",()=>{},"QuietButton","\uE8B5");imports.MinHeight=32;imports.Padding=new(10,5,10,5);imports.IsEnabled=false;imports.ToolTip="Operator report import is not implemented.";navigation.Children.Add(imports);
        }
        tabs.Children.Add(navigation);DockPanel.SetDock(tabs,Dock.Top);root.Children.Add(tabs);

        var footer=new DockPanel{Height=24,Margin=new(12,0,12,0)};var build=Ui.Text(Ui.BuildLabel,10,"#64748B");build.Margin=new(16,0,0,0);DockPanel.SetDock(build,Dock.Right);footer.Children.Add(build);DockPanel.SetDock(revision,Dock.Right);footer.Children.Add(revision);footer.Children.Add(status);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var heading=new DockPanel{Margin=new(12,8,12,6)};DockPanel.SetDock(count,Dock.Right);heading.Children.Add(count);
        var partyTools=new StackPanel{Orientation=Orientation.Horizontal};
        var partyLabel=Ui.Text("Party",12,"#52627A");partyLabel.Margin=new(0,0,8,0);partyTools.Children.Add(partyLabel);partyTools.Children.Add(partySelector);
        var create=CompactButton("Create party",()=>Run(CreateParty),"\uE710");partyTools.Children.Add(create);
        assignButton=CompactButton("Assign worksheet",()=>Run(AssignWorksheet),"\uE8B5");partyTools.Children.Add(assignButton);
        DockPanel.SetDock(partyTools,Dock.Right);heading.Children.Add(partyTools);heading.Children.Add(title);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);

        var options=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};
        termsGroup.Margin=new(0,0,14,0);var termLabel=Ui.Text("Terms",12,"#52627A");termLabel.Margin=new(0,0,6,0);termsGroup.Children.Add(termLabel);termsGroup.Children.Add(terms);termsGroup.Children.Add(Ui.Text(" days",11,"#64748B"));options.Children.Add(termsGroup);
        openingGroup.Margin=new(0,0,14,0);var openLabel=Ui.Text("Opening USD",12,"#52627A");openLabel.Margin=new(0,0,6,0);openingGroup.Children.Add(openLabel);openingGroup.Children.Add(opening);options.Children.Add(openingGroup);
        var dateLabel=Ui.Text("As of",12,"#52627A");dateLabel.Margin=new(0,0,6,0);options.Children.Add(dateLabel);options.Children.Add(asOf);
        var calc=CompactButton("Recalculate",Calculate,"\uE9D9");options.Children.Add(calc);
        options.Children.Add(new Border{Width=1,Height=22,Background=Ui.Color("#D8E0EB"),Margin=new(10,0,2,0)});
        options.Children.Add(CompactButton("Reload",()=>Run(()=>{if(Discard())Open(controller.Name);}),"\uE72C"));
        deleteButton=CompactButton("Delete",()=>Run(Delete),"\uE74D");deleteButton.Foreground=Ui.Color("#A63E3E");options.Children.Add(deleteButton);
        var density=CompactButton("Comfortable rows",()=>{compact=!compact;if(grid!=null)grid.Table.RowHeight=compact?28:36;},"\uE8A4");density.ToolTip="Switch between 28 and 36 pixel rows";options.Children.Add(density);
        var optionBar=new Border{Background=Brushes.White,BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(1),Padding=new(10,5,10,5),Margin=new(10,0,10,4),Child=options};DockPanel.SetDock(optionBar,Dock.Top);root.Children.Add(optionBar);
        notification=new Border{Background=Ui.Color("#FFF2F0"),BorderBrush=Ui.Color("#F3C8C3"),BorderThickness=new(1),Padding=new(10,6,10,6),Margin=new(10,0,10,4),Child=message,Visibility=Visibility.Collapsed};DockPanel.SetDock(notification,Dock.Top);root.Children.Add(notification);
        var sheet=new Border{Background=Brushes.White,BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(1),Margin=new(10,0,10,4),Child=sheetArea};root.Children.Add(sheet);
        opening.TextChanged+=(_,_)=>{if(!refreshing)MarkDirty();};terms.TextChanged+=(_,_)=>{if(!refreshing)MarkDirty();};asOf.TextChanged+=(_,_)=>{if(!refreshing&&grid!=null)MarkDirty();};asOf.LostKeyboardFocus+=(_,_)=>Calculate();
        System.Windows.Automation.AutomationProperties.SetName(opening,"Opening balance in USD");System.Windows.Automation.AutomationProperties.SetName(terms,"Payment terms in calendar days");System.Windows.Automation.AutomationProperties.SetName(asOf,"Report as of date");
        Ui.WindowFrame(this,root,"TradeVault · "+session.Role+" workspace",embeddedControls:true);
        ToolTipService.SetToolTip(revision,"Storage: "+path);Closing+=(_,e)=>{if(!Discard())e.Cancel=true;};
        InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Save)),Key.S,ModifierKeys.Control));InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Add)),Key.N,ModifierKeys.Control));
        System.Windows.Automation.AutomationProperties.SetName(partySelector,"Select party");
        RefreshParties(Guid.Empty);
        partySelector.SelectionChanged+=(_,_)=>SwitchParty();
        Open("form");
    }
    private static Button CompactButton(string text,Action action,string glyph){var b=Ui.Button(text,action,"QuietButton",glyph);b.MinHeight=30;b.Padding=new(9,4,9,4);b.Margin=new(4,0,0,0);return b;}
    private void Run(Action action){try{action();}catch(Exception ex){Error(ex.Message);}}
    private void Error(string text){message.Text=text;notification.Visibility=Visibility.Visible;status.Text="Action needs attention";status.Foreground=Ui.Color("#B42318");}
    private void MarkDirty(){dirty=true;saveButton.IsEnabled=true;status.Text="● Unsaved changes";status.Foreground=Ui.Color("#9A6700");}
    private void Watch(GridRow row)=>row.Changed+=_=>{if(!refreshing)MarkDirty();};
    private bool Discard(){grid?.Table.CommitEdit();grid?.Table.CommitEdit(DataGridEditingUnit.Row,true);return !dirty||MessageBox.Show(this,"You have unsaved changes. Discard them and continue?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
    private void Open(string name,Guid? partyId=null)
    {
        controller.Load(name,partyId);refreshing=true;opening.Text=controller.Opening.ToString("0.00",CultureInfo.InvariantCulture);terms.Text=controller.Terms.ToString();asOf.Value=controller.AsOf.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);refreshing=false;
        foreach(var row in controller.Rows)Watch(row);
        title.Text=name switch{"form"=>"Invoice register","details"=>"Account details",_=>"Debit & credit"};
        foreach(var item in nav){item.Value.Background=Ui.Color(item.Key==name?"#E8EFFD":"#FFFFFF");item.Value.Foreground=Ui.Color(item.Key==name?"#2458D3":"#52627A");}
        openingGroup.Visibility=name=="form"?Visibility.Collapsed:Visibility.Visible;termsGroup.Visibility=name=="form"?Visibility.Visible:Visibility.Collapsed;
        grid=new ExcelGrid(controller.Fields,controller.Rows,promote:row=>{controller.Add(row);Watch(row);MarkDirty();UpdateRecords();});grid.Table.RowHeight=compact?28:36;grid.Edited+=Calculate;
        grid.Table.SelectedCellsChanged+=(_,_)=>UpdateRecords();
        sheetArea.Children.Clear();sheetArea.Children.Add(grid);UpdateRecords();notification.Visibility=Visibility.Collapsed;dirty=false;saveButton.IsEnabled=false;
        status.Text="All changes saved";status.Foreground=Ui.Color("#64748B");revision.Text=$"Revision {controller.Revision} · Local storage";
        assignButton.Visibility=controller.PartyId==Guid.Empty?Visibility.Visible:Visibility.Collapsed;
        assignButton.IsEnabled=controller.Rows.Count>0;
    }
    private void UpdateRecords(){count.Text=$"{controller.Rows.Count} record{(controller.Rows.Count==1?"":"s")}";deleteButton.IsEnabled=grid?.Table.CurrentItem is GridRow row&&controller.Rows.Contains(row);assignButton.IsEnabled=controller.PartyId==Guid.Empty&&controller.Rows.Count>0&&!dirty;}
    private void Add(){if(grid==null)return;var row=controller.Add();Watch(row);MarkDirty();UpdateRecords();grid.Table.ScrollIntoView(row);grid.Table.CurrentCell=new DataGridCellInfo(row,grid.Table.Columns.First(c=>!c.IsReadOnly));grid.Table.Focus();grid.Table.BeginEdit();}
    private void Delete(){if(grid?.Table.CurrentItem is GridRow row&&controller.Rows.Contains(row)&&MessageBox.Show(this,"Remove the selected record? Save changes to confirm the deletion.","Delete record",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes){controller.Rows.Remove(row);MarkDirty();UpdateRecords();Calculate();}}
    private void Options(){if(!decimal.TryParse(opening.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||decimal.Round(value,2)!=value)throw new ArgumentException("Opening balance: enter USD with at most two decimals.");if(!int.TryParse(terms.Text,out var days)||days<0||days>3650)throw new ArgumentException("Payment terms must be 0–3650 days.");controller.Opening=value;controller.Terms=days;if(!DateOnly.TryParseExact(DateCellConverter.Normalize(asOf.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var reportDate))throw new ArgumentException("As of: enter a valid date or choose one from the calendar.");controller.AsOf=reportDate;}
    private void Calculate(){if(grid==null)return;try{Options();refreshing=true;controller.Calculate();controller.Validate(false);notification.Visibility=Visibility.Collapsed;status.Text=dirty?"● Unsaved changes":"All changes saved";status.Foreground=Ui.Color(dirty?"#9A6700":"#64748B");}catch(Exception ex){Error(ex.Message);}finally{refreshing=false;}}
    private void Save(){if(grid==null)return;if(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Correct invalid cells before saving.");Options();refreshing=true;try{controller.Save();dirty=false;saveButton.IsEnabled=false;notification.Visibility=Visibility.Collapsed;status.Foreground=Ui.Color("#247653");status.Text="Changes saved";revision.Text=$"Revision {controller.Revision} · Local storage";UpdateRecords();}finally{refreshing=false;}}
    internal void PreviewSheet(string name)=>Open(name);
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
