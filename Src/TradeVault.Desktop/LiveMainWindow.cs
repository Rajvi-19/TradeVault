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
    private readonly ContentControl content=new();
    private readonly TextBlock status=Ui.Text("All changes saved",12,"#64748B");
    private readonly TextBlock title=Ui.Text("Invoice register",28,"#18243B",true);
    private readonly TextBlock subtitle=Ui.Text("",13,"#64748B");
    private readonly TextBlock count=Ui.Text("0 records",12,"#64748B");
    private readonly TextBlock revision=Ui.Text("",11,"#64748B");
    private readonly TextBox opening=new(){Text="0.00",Width=112};
    private readonly TextBox terms=new(){Text="90",Width=64};
    private readonly DatePicker asOf=new(){SelectedDate=DateTime.Today,Width=145};
    private readonly StackPanel openingGroup=new(){Orientation=Orientation.Horizontal};
    private readonly StackPanel termsGroup=new(){Orientation=Orientation.Horizontal};
    private readonly Dictionary<string,Button> nav=new();
    private readonly Button saveButton;
    private readonly Button deleteButton;
    private readonly Border notification;
    private readonly TextBlock message=Ui.Text("",12,"#B42318");
    private ExcelGrid? grid;
    private bool dirty,refreshing,compact;
    private readonly Grid sheetArea=new();
    private readonly StackPanel emptyState=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,MaxWidth=400,Margin=new(0,80,0,0)};

    public MainWindow(WorkbookStore store,LoginSession session,string path)
    {
        controller=new(store,session);Title="TradeVault | "+session.Role;Width=1480;Height=920;MinWidth=1100;MinHeight=720;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new Grid{Background=Ui.Color("#F4F6FA")};root.ColumnDefinitions.Add(new(){Width=new GridLength(224)});root.ColumnDefinitions.Add(new());Content=root;
        var sidebar=new DockPanel{Background=Ui.Color("#111E35")};root.Children.Add(sidebar);
        var brand=new StackPanel{Margin=new(24,30,20,30)};
        var brandLine=new StackPanel{Orientation=Orientation.Horizontal};var mark=Ui.Badge("TV","#2F60D7","#FFFFFF");mark.Margin=new(0,0,10,0);brandLine.Children.Add(mark);brandLine.Children.Add(Ui.Text("TradeVault",21,"#FFFFFF",true));brand.Children.Add(brandLine);
        var tagline=Ui.Text("EXPORT OPERATIONS",10,"#91A4C0",true);tagline.Margin=new(0,12,0,0);brand.Children.Add(tagline);DockPanel.SetDock(brand,Dock.Top);sidebar.Children.Add(brand);
        var account=new StackPanel{Margin=new(16,16,16,22)};
        account.Children.Add(new Border{Height=1,Background=Ui.Color("#2A3850"),Margin=new(4,0,4,18)});
        account.Children.Add(Ui.Text(session.Username,14,"#FFFFFF",true));account.Children.Add(Ui.Text(session.Role+" workspace",12,"#A5B6CE"));
        var logout=Ui.Button("Sign out",()=>Run(()=>{if(Discard()){dirty=false;Close();}}),"QuietButton","\uE8AC");logout.Foreground=Ui.Color("#CCD8EA");logout.HorizontalAlignment=HorizontalAlignment.Left;logout.Margin=new(-10,12,0,0);account.Children.Add(logout);DockPanel.SetDock(account,Dock.Bottom);sidebar.Children.Add(account);
        var navigation=new StackPanel{Margin=new(12,0,12,0)};
        navigation.Children.Add(new TextBlock{Text="WORKSPACE",FontSize=10,FontWeight=FontWeights.SemiBold,Foreground=Ui.Color("#91A4C0"),Margin=new(12,4,0,12)});
        foreach(var (name,label,icon) in new[]{("form","Invoice register","\uE8A5"),("details","Account details","\uE8D4"),("ledger","Debit & credit","\uE8AB")})
        {
            var b=Ui.Button(label,()=>Run(()=>{if(Discard())Open(name);}),"QuietButton",icon);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Foreground=Ui.Color("#CCD8EA");b.Padding=new(14,13,14,13);b.Margin=new(0,0,0,6);nav.Add(name,b);navigation.Children.Add(b);
        }
        if(session.Role=="Owner")
        {
            navigation.Children.Add(new Border{Height=1,Background=Ui.Color("#2A3850"),Margin=new(12,18,12,20)});
            var imports=Ui.Button("Operator reports",()=>{},"QuietButton","\uE8B5");imports.Foreground=Ui.Color("#A5B6CE");imports.IsEnabled=false;imports.ToolTip="Report import is not available yet.";imports.HorizontalContentAlignment=HorizontalAlignment.Left;navigation.Children.Add(imports);
        }
        sidebar.Children.Add(navigation);
        var body=new DockPanel();Grid.SetColumn(body,1);root.Children.Add(body);
        var top=new DockPanel{Background=Brushes.White,Height=62,LastChildFill=false};
        var breadcrumb=Ui.Text("Workspace   /   Financial records",12,"#64748B");breadcrumb.Margin=new(30,0,0,0);top.Children.Add(breadcrumb);
        var offline=Ui.Badge("●  Offline workspace","#EDF7F2","#247653");offline.Margin=new(0,0,26,0);DockPanel.SetDock(offline,Dock.Right);top.Children.Add(offline);DockPanel.SetDock(top,Dock.Top);body.Children.Add(top);
        var footer=new DockPanel{Margin=new(30,10,30,12)};footer.Children.Add(status);revision.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(revision,Dock.Right);footer.Children.Add(revision);DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);
        var page=new DockPanel{Margin=new(30,26,30,0)};body.Children.Add(page);
        var pageHeader=new DockPanel{Margin=new(0,0,0,22)};var headings=new StackPanel();headings.Children.Add(title);subtitle.Margin=new(0,7,0,0);headings.Children.Add(subtitle);
        saveButton=Ui.Button("Save changes",()=>Run(Save),"PrimaryButton","\uE74E");saveButton.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(saveButton,Dock.Right);pageHeader.Children.Add(saveButton);pageHeader.Children.Add(headings);DockPanel.SetDock(pageHeader,Dock.Top);page.Children.Add(pageHeader);
        var options=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};
        options.Children.Add(Ui.Badge("USD","#EEF2F8","#52627A"));
        termsGroup.Margin=new(22,0,0,0);var termLabel=Ui.Text("Payment terms",12,"#64748B");termLabel.Margin=new(0,0,10,0);termsGroup.Children.Add(termLabel);termsGroup.Children.Add(terms);termsGroup.Children.Add(Ui.Text("  days",12,"#64748B"));options.Children.Add(termsGroup);
        openingGroup.Margin=new(22,0,0,0);var openLabel=Ui.Text("Opening balance",12,"#64748B");openLabel.Margin=new(0,0,10,0);openingGroup.Children.Add(openLabel);openingGroup.Children.Add(opening);options.Children.Add(openingGroup);
        var dateLabel=Ui.Text("As of",12,"#64748B");dateLabel.Margin=new(22,0,10,0);options.Children.Add(dateLabel);options.Children.Add(asOf);
        var calc=Ui.Button("Recalculate",Calculate,"QuietButton","\uE9D9");calc.Margin=new(16,0,0,0);options.Children.Add(calc);
        var optionCard=Ui.Card(options,new(16,12,16,12));optionCard.Margin=new(0,0,0,18);DockPanel.SetDock(optionCard,Dock.Top);page.Children.Add(optionCard);
        notification=new Border{Background=Ui.Color("#FFF2F0"),BorderBrush=Ui.Color("#F3C8C3"),BorderThickness=new(1),CornerRadius=new(6),Padding=new(14,10,14,10),Margin=new(0,0,0,12),Child=message,Visibility=Visibility.Collapsed};DockPanel.SetDock(notification,Dock.Top);page.Children.Add(notification);
        var sheet=new DockPanel();var commands=new DockPanel{Margin=new(18,14,18,14)};
        var actions=new StackPanel{Orientation=Orientation.Horizontal};actions.Children.Add(Ui.Button("Add record",()=>Run(Add),null,"\uE710"));
        var reload=Ui.Button("Reload",()=>Run(()=>{if(Discard())Open(controller.Name);}),"QuietButton","\uE72C");reload.Margin=new(8,0,0,0);actions.Children.Add(reload);
        deleteButton=Ui.Button("Delete",()=>Run(Delete),"QuietButton","\uE74D");deleteButton.Foreground=Ui.Color("#A63E3E");deleteButton.Margin=new(8,0,0,0);actions.Children.Add(deleteButton);
        var density=Ui.Button("Compact view",()=>{compact=!compact;if(grid!=null)grid.Table.RowHeight=compact?30:40;},"QuietButton");density.ToolTip="Toggle compact worksheet rows";density.Margin=new(8,0,0,0);actions.Children.Add(density);
        count.HorizontalAlignment=HorizontalAlignment.Right;DockPanel.SetDock(count,Dock.Right);commands.Children.Add(count);commands.Children.Add(actions);DockPanel.SetDock(commands,Dock.Top);sheet.Children.Add(commands);
        sheet.Children.Add(sheetArea);content.Content=sheet;page.Children.Add(Ui.Card(content));
        emptyState.Children.Add(new Border{Background=Ui.Color("#EDF3FF"),CornerRadius=new(12),Padding=new(18),HorizontalAlignment=HorizontalAlignment.Center,Child=Ui.Icon("\uE8A5","#2458D3",28),Margin=new(0,0,0,18)});
        emptyState.Children.Add(new TextBlock{Text="Your worksheet starts here",FontSize=20,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center});
        emptyState.Children.Add(new TextBlock{Text="Add your first record to start tracking invoices and payments. Your changes stay in this workspace.",TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Foreground=Ui.Color("#64748B"),Margin=new(0,10,0,20)});
        emptyState.Children.Add(Ui.Button("Add first record",()=>Run(Add),"PrimaryButton","\uE710"));
        opening.TextChanged+=(_,_)=>{if(!refreshing)MarkDirty();};terms.TextChanged+=(_,_)=>{if(!refreshing)MarkDirty();};asOf.SelectedDateChanged+=(_,_)=>Calculate();
        System.Windows.Automation.AutomationProperties.SetName(opening,"Opening balance in USD");System.Windows.Automation.AutomationProperties.SetName(terms,"Payment terms in calendar days");System.Windows.Automation.AutomationProperties.SetName(asOf,"Report as of date");
        ToolTipService.SetToolTip(revision,"Storage: "+path);Closing+=(_,e)=>{if(!Discard())e.Cancel=true;};
        InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Save)),Key.S,ModifierKeys.Control));InputBindings.Add(new KeyBinding(new ActionCommand(()=>Run(Add)),Key.N,ModifierKeys.Control));
        Open("form");
    }
    private void Run(Action action){try{action();}catch(Exception ex){Error(ex.Message);}}
    private void Error(string text){message.Text=text;notification.Visibility=Visibility.Visible;status.Text="Action needs attention";status.Foreground=Ui.Color("#B42318");}
    private void MarkDirty(){dirty=true;saveButton.IsEnabled=true;status.Text="●  Unsaved changes";status.Foreground=Ui.Color("#9A6700");}
    private void Watch(GridRow row)=>row.Changed+=_=>{if(!refreshing)MarkDirty();};
    private bool Discard(){grid?.Table.CommitEdit();grid?.Table.CommitEdit(DataGridEditingUnit.Row,true);return !dirty||MessageBox.Show(this,"You have unsaved changes. Discard them and continue?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
    private void Open(string name)
    {
        controller.Load(name);refreshing=true;opening.Text=controller.Opening.ToString("0.00",CultureInfo.InvariantCulture);terms.Text=controller.Terms.ToString();refreshing=false;
        foreach(var row in controller.Rows)Watch(row);
        title.Text=name switch{"form"=>"Invoice register","details"=>"Account details",_=>"Debit & credit"};
        subtitle.Text=name switch{"form"=>"Manage export invoices, shipment references and payment dates.","details"=>"Review invoice amounts, receipts and the outstanding difference.",_=>"Maintain your party ledger with a running USD balance."};
        foreach(var item in nav){item.Value.Background=Ui.Color(item.Key==name?"#243B60":"#111E35");item.Value.Foreground=Ui.Color(item.Key==name?"#FFFFFF":"#CCD8EA");}
        openingGroup.Visibility=name=="form"?Visibility.Collapsed:Visibility.Visible;termsGroup.Visibility=name=="form"?Visibility.Visible:Visibility.Collapsed;
        grid=new ExcelGrid(controller.Fields,controller.Rows);grid.Table.RowHeight=compact?30:40;grid.Edited+=Calculate;
        grid.Table.SelectedCellsChanged+=(_,_)=>deleteButton.IsEnabled=grid.Table.CurrentItem is GridRow;
        sheetArea.Children.Clear();sheetArea.Children.Add(grid);sheetArea.Children.Add(emptyState);UpdateRecords();notification.Visibility=Visibility.Collapsed;dirty=false;saveButton.IsEnabled=false;
        status.Text="All changes saved";status.Foreground=Ui.Color("#64748B");revision.Text=$"Revision {controller.Revision}  ·  Local storage";
    }
    private void UpdateRecords(){count.Text=$"{controller.Rows.Count} record{(controller.Rows.Count==1?"":"s")}";emptyState.Visibility=controller.Rows.Count==0?Visibility.Visible:Visibility.Collapsed;if(grid!=null)grid.Table.Visibility=controller.Rows.Count==0?Visibility.Hidden:Visibility.Visible;deleteButton.IsEnabled=grid?.Table.CurrentItem is GridRow;}
    private void Add(){if(grid==null)return;var row=controller.Add();Watch(row);MarkDirty();UpdateRecords();grid.Table.ScrollIntoView(row);grid.Table.CurrentCell=new DataGridCellInfo(row,grid.Table.Columns.First(c=>!c.IsReadOnly));grid.Table.Focus();grid.Table.BeginEdit();}
    private void Delete(){if(grid?.Table.CurrentItem is GridRow row&&MessageBox.Show(this,"Remove the selected record? Save changes to confirm the deletion.","Delete record",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes){controller.Rows.Remove(row);MarkDirty();UpdateRecords();Calculate();}}
    private void Options(){if(!decimal.TryParse(opening.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||decimal.Round(value,2)!=value)throw new ArgumentException("Opening balance: enter USD with at most two decimals.");if(!int.TryParse(terms.Text,out var days)||days<0||days>3650)throw new ArgumentException("Payment terms must be 0–3650 days.");controller.Opening=value;controller.Terms=days;controller.AsOf=DateOnly.FromDateTime(asOf.SelectedDate??DateTime.Today);}
    private void Calculate(){if(grid==null)return;try{Options();refreshing=true;controller.Calculate();var errors=controller.Validate();if(errors.Count>0)Error(string.Join("\n",errors.Take(3)));else{notification.Visibility=Visibility.Collapsed;status.Text=dirty?"●  Unsaved changes":"All changes saved";}}catch(Exception ex){Error(ex.Message);}finally{refreshing=false;}}
    private void Save(){if(grid==null)return;if(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Correct invalid cells before saving.");Options();refreshing=true;try{controller.Save();dirty=false;saveButton.IsEnabled=false;notification.Visibility=Visibility.Collapsed;status.Foreground=Ui.Color("#247653");status.Text="✓  Changes saved successfully";revision.Text=$"Revision {controller.Revision}  ·  Local storage";}finally{refreshing=false;}}
    internal void PreviewSheet(string name)=>Open(name);
    internal void CheckInteractions()
    {
        Open("form");var before=controller.Rows.Count;Add();
        var row=controller.Rows.Last();row["F1"]="UI TEST ONLY";row["F9"]="UI-TEST";
        if(!saveButton.IsEnabled)throw new InvalidOperationException("Editing must enable Save.");
        Save();Open("form");
        if(controller.Rows.Count!=before+1||saveButton.IsEnabled)throw new InvalidOperationException("UI save/reload failed.");
        foreach(var name in new[]{"details","ledger","form"}){Open(name);Measure(new Size(1100,720));Arrange(new Rect(0,0,1100,720));UpdateLayout();}
    }
    private sealed class ActionCommand(Action execute):ICommand{public event EventHandler? CanExecuteChanged{add{}remove{}}public bool CanExecute(object? parameter)=>true;public void Execute(object? parameter)=>execute();}
}
