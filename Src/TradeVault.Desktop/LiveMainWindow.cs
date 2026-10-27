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
    private readonly TextBlock status=Ui.Text("All changes saved",11,"#64748B");
    private readonly TextBlock title=Ui.Text("Invoice register",18,"#18243B",true);
    private readonly TextBlock count=Ui.Text("0 records",11,"#64748B");
    private readonly TextBlock revision=Ui.Text("",11,"#64748B");
    private readonly TextBox opening=new(){Text="0.00",Width=100,MinHeight=30,Padding=new(8,4,8,4)};
    private readonly TextBox terms=new(){Text="90",Width=54,MinHeight=30,Padding=new(8,4,8,4)};
    private readonly DatePicker asOf=new(){SelectedDate=DateTime.Today,Width=145,MinHeight=30,SelectedDateFormat=DatePickerFormat.Short,Language=System.Windows.Markup.XmlLanguage.GetLanguage("en-IN")};
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
        controller=new(store,session);Width=1440;Height=880;MinWidth=1000;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new DockPanel{Background=Ui.Color("#F4F6FA")};
        var header=new DockPanel{Background=Ui.Color("#111E35"),Height=42};
        var account=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,8,0)};
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
        var heading=new DockPanel{Margin=new(12,8,12,6)};DockPanel.SetDock(count,Dock.Right);heading.Children.Add(count);heading.Children.Add(title);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);

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
        opening.TextChanged+=(_,_)=>{if(!refreshing)MarkDirty();};terms.TextChanged+=(_,_)=>{if(!refreshing)MarkDirty();};asOf.SelectedDateChanged+=(_,_)=>Calculate();
        System.Windows.Automation.AutomationProperties.SetName(opening,"Opening balance in USD");System.Windows.Automation.AutomationProperties.SetName(terms,"Payment terms in calendar days");System.Windows.Automation.AutomationProperties.SetName(asOf,"Report as of date");
        Ui.WindowFrame(this,root,"TradeVault · "+session.Role+" workspace");
        asOf.DateValidationError+=(_,e)=>{e.ThrowException=false;Error("As of: enter a valid date such as 26/10/2026, or choose one from the calendar.");};
        ToolTipService.SetToolTip(revision,"Storage: "+path);Closing+=(_,e)=>{if(!Discard())e.Cancel=true;};
        InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Save)),Key.S,ModifierKeys.Control));InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Add)),Key.N,ModifierKeys.Control));
        Open("form");
    }
    private static Button CompactButton(string text,Action action,string glyph){var b=Ui.Button(text,action,"QuietButton",glyph);b.MinHeight=30;b.Padding=new(9,4,9,4);b.Margin=new(4,0,0,0);return b;}
    private void Run(Action action){try{action();}catch(Exception ex){Error(ex.Message);}}
    private void Error(string text){message.Text=text;notification.Visibility=Visibility.Visible;status.Text="Action needs attention";status.Foreground=Ui.Color("#B42318");}
    private void MarkDirty(){dirty=true;saveButton.IsEnabled=true;status.Text="● Unsaved changes";status.Foreground=Ui.Color("#9A6700");}
    private void Watch(GridRow row)=>row.Changed+=_=>{if(!refreshing)MarkDirty();};
    private bool Discard(){grid?.Table.CommitEdit();grid?.Table.CommitEdit(DataGridEditingUnit.Row,true);return !dirty||MessageBox.Show(this,"You have unsaved changes. Discard them and continue?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
    private void Open(string name)
    {
        controller.Load(name);refreshing=true;opening.Text=controller.Opening.ToString("0.00",CultureInfo.InvariantCulture);terms.Text=controller.Terms.ToString();refreshing=false;
        foreach(var row in controller.Rows)Watch(row);
        title.Text=name switch{"form"=>"Invoice register","details"=>"Account details",_=>"Debit & credit"};
        foreach(var item in nav){item.Value.Background=Ui.Color(item.Key==name?"#E8EFFD":"#FFFFFF");item.Value.Foreground=Ui.Color(item.Key==name?"#2458D3":"#52627A");}
        openingGroup.Visibility=name=="form"?Visibility.Collapsed:Visibility.Visible;termsGroup.Visibility=name=="form"?Visibility.Visible:Visibility.Collapsed;
        grid=new ExcelGrid(controller.Fields,controller.Rows,promote:row=>{controller.Add(row);Watch(row);MarkDirty();UpdateRecords();});grid.Table.RowHeight=compact?28:36;grid.Edited+=Calculate;
        grid.Table.SelectedCellsChanged+=(_,_)=>UpdateRecords();
        sheetArea.Children.Clear();sheetArea.Children.Add(grid);UpdateRecords();notification.Visibility=Visibility.Collapsed;dirty=false;saveButton.IsEnabled=false;
        status.Text="All changes saved";status.Foreground=Ui.Color("#64748B");revision.Text=$"Revision {controller.Revision} · Local storage";
    }
    private void UpdateRecords(){count.Text=$"{controller.Rows.Count} record{(controller.Rows.Count==1?"":"s")}";deleteButton.IsEnabled=grid?.Table.CurrentItem is GridRow row&&controller.Rows.Contains(row);}
    private void Add(){if(grid==null)return;var row=controller.Add();Watch(row);MarkDirty();UpdateRecords();grid.Table.ScrollIntoView(row);grid.Table.CurrentCell=new DataGridCellInfo(row,grid.Table.Columns.First(c=>!c.IsReadOnly));grid.Table.Focus();grid.Table.BeginEdit();}
    private void Delete(){if(grid?.Table.CurrentItem is GridRow row&&controller.Rows.Contains(row)&&MessageBox.Show(this,"Remove the selected record? Save changes to confirm the deletion.","Delete record",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes){controller.Rows.Remove(row);MarkDirty();UpdateRecords();Calculate();}}
    private void Options(){if(!decimal.TryParse(opening.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||decimal.Round(value,2)!=value)throw new ArgumentException("Opening balance: enter USD with at most two decimals.");if(!int.TryParse(terms.Text,out var days)||days<0||days>3650)throw new ArgumentException("Payment terms must be 0–3650 days.");controller.Opening=value;controller.Terms=days;if(!DateOnly.TryParseExact(DateCellConverter.Normalize(asOf.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var reportDate))throw new ArgumentException("As of: enter a valid date or choose one from the calendar.");controller.AsOf=reportDate;}
    private void Calculate(){if(grid==null)return;try{Options();refreshing=true;controller.Calculate();var errors=controller.Validate();if(errors.Count>0)Error(string.Join("\n",errors.Take(3)));else{notification.Visibility=Visibility.Collapsed;status.Text=dirty?"● Unsaved changes":"All changes saved";status.Foreground=Ui.Color(dirty?"#9A6700":"#64748B");}}catch(Exception ex){Error(ex.Message);}finally{refreshing=false;}}
    private void Save(){if(grid==null)return;if(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Correct invalid cells before saving.");Options();refreshing=true;try{controller.Save();dirty=false;saveButton.IsEnabled=false;notification.Visibility=Visibility.Collapsed;status.Foreground=Ui.Color("#247653");status.Text="Changes saved";revision.Text=$"Revision {controller.Revision} · Local storage";}finally{refreshing=false;}}
    internal void PreviewSheet(string name)=>Open(name);
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
    private sealed class ActionCommand(Action execute):ICommand{public event EventHandler? CanExecuteChanged{add{}remove{}}public bool CanExecute(object? parameter)=>true;public void Execute(object? parameter)=>execute();}
}
