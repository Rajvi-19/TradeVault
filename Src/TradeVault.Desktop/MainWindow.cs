using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;

namespace TradeVault.Desktop;

public sealed class MainWindow : Window
{
    private readonly IPrototypeService service;
    private readonly ContentControl workspace = new();
    private readonly StackPanel navigation = new() { Orientation=Orientation.Horizontal };
    private readonly StackPanel ribbon = new() { Orientation=Orientation.Horizontal };
    private readonly TextBlock status = Text("Ready",12,"#616161");
    private readonly TextBlock section = Text("Invoices & shipments",13,"#FFFFFF");
    private ExcelGrid? activeGrid;
    private string role="Operator";
    private string current="Invoices & shipments";
    private bool hasUnsavedChanges;
    private ComboBox roleSelector = null!;
    private readonly TextBlock issues = new() { Foreground=Brush("#A4262C"), TextWrapping=TextWrapping.Wrap };
    public MainWindow() : this(new MockPrototypeService()) { }
    public MainWindow(IPrototypeService service)
    {
        this.service=service;
        Title="TradeVault"; Width=1440; Height=880; MinWidth=1060; MinHeight=640;
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanResize; Background=Brushes.White;
        FontFamily=new("Segoe UI"); FontSize=13;
        WindowChrome.SetWindowChrome(this,new WindowChrome { CaptionHeight=44, ResizeBorderThickness=new(6), CornerRadius=new(0), GlassFrameThickness=new(0) });
        var root=new DockPanel {Background=Brushes.White}; Content=root;
        var title=new DockPanel { Height=44, Background=Brush("#107C41") };
        var controls=new StackPanel { Orientation=Orientation.Horizontal };
        foreach (var (label,action) in new (string,Action)[] {("Minimize",()=>SystemCommands.MinimizeWindow(this)),("Maximize / restore",()=> {if(WindowState==WindowState.Maximized) SystemCommands.RestoreWindow(this); else SystemCommands.MaximizeWindow(this);}), ("Close",()=>Close()) }) {
            var b=Button(label=="Minimize"?"\uE921":label=="Close"?"\uE8BB":"\uE922",action); b.FontFamily=new("Segoe MDL2 Assets"); b.Foreground=Brushes.White; b.Width=46; b.ToolTip=label;
            System.Windows.Automation.AutomationProperties.SetName(b,label); WindowChrome.SetIsHitTestVisibleInChrome(b,true); controls.Children.Add(b);
        }
        DockPanel.SetDock(controls,Dock.Right); title.Children.Add(controls);
        var identity=new StackPanel { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center, Margin=new(18,0,0,0) };
        identity.Children.Add(Text("TradeVault",15,"#FFFFFF",true)); identity.Children.Add(Text("    /    ",14,"#A9D8BA")); identity.Children.Add(section); title.Children.Add(identity);
        DockPanel.SetDock(title,Dock.Top); root.Children.Add(title);

        var menubar=new DockPanel { Background=Brush("#FAFAFA"), Height=42 };
        var account=new StackPanel { Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center, Margin=new(0,0,16,0) };
        account.Children.Add(Text("Sample workspace  ·  ",12,"#767676"));
        var roles=new ComboBox { Width=104, ItemsSource=new[]{"Operator","Owner"}, SelectedIndex=0, ToolTip="Preview workspace role. Sign-in is not connected." };
        roleSelector=roles;
        roles.SelectionChanged+=(_,_)=> {role=(string)roles.SelectedItem; BuildNavigation(); ShowInvoices();}; account.Children.Add(roles);
        DockPanel.SetDock(account,Dock.Right); menubar.Children.Add(account);
        var menus=new StackPanel { Orientation=Orientation.Horizontal, Margin=new(10,0,0,0) };
        menus.Children.Add(Button("Home",()=>ShowCurrent()));
        menus.Children.Add(Button("Invoices",ShowInvoices)); menus.Children.Add(Button("Receipts",ShowReceipts));
        menus.Children.Add(Button("Reports",()=>ShowReport(false)));
        menubar.Children.Add(menus); DockPanel.SetDock(menubar,Dock.Top); root.Children.Add(menubar);

        var ribbonBorder=new Border { Background=Brush("#FAFAFA"), BorderBrush=Brush("#DCDCDC"), BorderThickness=new(0,0,0,1), Padding=new(14,8,14,8), Child=ribbon };
        DockPanel.SetDock(ribbonBorder,Dock.Top); root.Children.Add(ribbonBorder);
        var footer=new DockPanel { Height=28, Background=Brush("#F5F5F5"), LastChildFill=true };
        var right=Text("USD   |   Offline workspace",11,"#616161"); right.Margin=new(12,5,18,0); DockPanel.SetDock(right,Dock.Right); footer.Children.Add(right);
        status.Margin=new(16,5,0,0); footer.Children.Add(status); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        var sheets=new Border { Background=Brush("#FAFAFA"), BorderBrush=Brush("#DDDDDD"), BorderThickness=new(0,1,0,0), Padding=new(36,0,0,0), Child=navigation };
        DockPanel.SetDock(sheets,Dock.Bottom); root.Children.Add(sheets);
        root.Children.Add(workspace);
        service.Invoices.ToList().ForEach(Watch); BuildNavigation(); ShowInvoices();
        Closing+=(_,e)=> {
            if(hasUnsavedChanges && IsVisible && MessageBox.Show(this,"Changes in this sample workspace are not saved. Close anyway?","TradeVault",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) e.Cancel=true;
        };
    }
    private static SolidColorBrush Brush(string color)=>new((Color)ColorConverter.ConvertFromString(color));
    private static TextBlock Text(string text,double size=13,string color="#242424",bool bold=false)=>new() {Text=text,FontSize=size,Foreground=Brush(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,VerticalAlignment=VerticalAlignment.Center};
    private static Button Button(string text,Action action) {var b=new Button {Content=text}; b.Click+=(_,_)=>action();return b;}
    private void Watch(GridRow row)=>row.Changed+=r=> {hasUnsavedChanges=true;service.Validate(r);status.Text="Unsaved changes · sample data · not saved to USB";};
    private void ShowCurrent() {if(current=="Invoices & shipments") ShowInvoices();else if(current=="Receipts") ShowReceipts();else if(current=="Operator Reports") ShowSubmissions();else ShowReport(current=="Debit / Credit");}
    private void BuildNavigation()
    {
        navigation.Children.Clear();
        foreach(var (label,action) in new (string,Action)[]{("Invoices & shipments",ShowInvoices),("Receipts",ShowReceipts),("Account Details",()=>ShowReport(false)),("Debit / Credit",()=>ShowReport(true))}) AddTab(label,action);
        if(role=="Owner") AddTab("Operator Reports",ShowSubmissions);
    }
    private void AddTab(string label,Action action)
    {
        var b=Button(label,action); b.Padding=new(18,10,18,10); b.Foreground=Brush(label==current?"#107C41":"#616161");
        b.FontWeight=label==current?FontWeights.SemiBold:FontWeights.Normal;
        var border=new Border {BorderThickness=new(0,0,0,label==current?3:0),BorderBrush=Brush("#107C41"),Child=b}; navigation.Children.Add(border);
    }
    private void AddCommand(string icon,string label,Action action,bool enabled=true,string? tooltip=null)
    {
        var contents=new StackPanel {Orientation=Orientation.Horizontal};
        contents.Children.Add(new TextBlock {Text=icon,FontFamily=new("Segoe MDL2 Assets"),FontSize=17,Foreground=Brush("#107C41"),Margin=new(0,0,8,0),VerticalAlignment=VerticalAlignment.Center});
        contents.Children.Add(Text(label,12)); var b=Button("",action);b.Content=contents;b.IsEnabled=enabled;b.ToolTip=tooltip??label;
        System.Windows.Automation.AutomationProperties.SetName(b,label);ribbon.Children.Add(b);
    }
    private void SetupRibbon(bool inputs)
    {
        ribbon.Children.Clear();
        if(inputs) {
            AddCommand("\uE710","New invoice",AddInvoice);
            AddCommand("\uE8FB","Validate",Validate);
            AddCommand("\uE74E","Save",()=>{},false,"Saving to USB is not connected yet.");
            ribbon.Children.Add(new Border {Width=1,Background=Brush("#D6D6D6"),Margin=new(12,3,12,3)});
        }
        AddCommand("\uE8C8","Copy",()=>activeGrid?.CopySelection());
        if(inputs)AddCommand("\uE77F","Paste",()=>activeGrid?.PasteClipboard());
        ribbon.Children.Add(new Border {Width=1,Background=Brush("#D6D6D6"),Margin=new(12,3,12,3)});
        AddCommand("\uE896","Export",()=>{},false,"Report export is not connected yet.");
        if(current=="Operator Reports") AddCommand("\uE8B5","Import report",()=>{},false,"Report file import is not connected yet.");
    }
    private DockPanel Page(string title,string description)
    {
        current=title;section.Text=title;BuildNavigation();
        var page=new DockPanel();
        var heading=new DockPanel {Margin=new(22,18,22,16)};
        var note=Text("Sample data · not saved",11,"#777777");DockPanel.SetDock(note,Dock.Right);heading.Children.Add(note);
        var words=new StackPanel();words.Children.Add(Text(title,20,"#242424",true));var subtitle=Text(description,12,"#707070");subtitle.Margin=new(0,5,0,0);words.Children.Add(subtitle);heading.Children.Add(words);
        DockPanel.SetDock(heading,Dock.Top);page.Children.Add(heading);return page;
    }
    private void AddInvoice()
    {
        if(activeGrid==null || current!="Invoices & shipments")return;
        var row=service.NewInvoice();Watch(row);service.Invoices.Add(row);activeGrid.Table.ScrollIntoView(row);
        activeGrid.Table.CurrentCell=new DataGridCellInfo(row,activeGrid.Table.Columns[0]);activeGrid.Table.Focus();activeGrid.Table.BeginEdit();
        status.Text="Unsaved new invoice · sample data · not saved to USB";
        hasUnsavedChanges=true;
    }
    private void Validate()
    {
        if(activeGrid==null)return;activeGrid.Table.CommitEdit();activeGrid.Table.CommitEdit(DataGridEditingUnit.Row,true);
        RefreshIssues();status.Text=issues.Text.Length==0?"Validation passed · sample data is not saved":"Validation needs attention · inputs retained";
    }
    private void RefreshIssues()
    {
        issues.Text=string.Join("\n",service.Invoices.SelectMany((r,i)=>service.Validate(r).Issues.Select(e=>$"Row {i+1} · {e.Message}")).Distinct());
        issues.Visibility=issues.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
    }
    private void ShowInvoices()
    {
        if(issues.Parent is Panel previous)previous.Children.Remove(issues);
        var page=Page("Invoices & shipments","Maintain invoice amounts and shipment references in one worksheet.");SetupRibbon(true);
        issues.Margin=new(22,8,22,8);issues.MaxHeight=100;issues.Visibility=Visibility.Collapsed;
        DockPanel.SetDock(issues,Dock.Bottom);page.Children.Add(issues);
        activeGrid=new ExcelGrid(service.InvoiceFields,service.Invoices);activeGrid.Edited+=RefreshIssues;page.Children.Add(activeGrid);workspace.Content=page;
    }
    private void ShowReceipts()
    {
        var page=Page("Receipts","Allocate receipts across invoices and payment components.");SetupRibbon(false);
        var totals=new UniformGrid {Columns=3,Margin=new(22,0,22,18)};var s=service.ReceiptSummary;
        foreach(var (label,value) in new[]{("Receipt total",s.Total),("Allocated",s.Allocated),("Unallocated",s.Unallocated)}) {
            var card=new StackPanel {Margin=new(14,10,14,10)};card.Children.Add(Text(label,12,"#707070"));var amount=Text($"USD {value}",22,"#107C41",true);amount.Margin=new(0,6,0,0);card.Children.Add(amount);
            totals.Children.Add(new Border {BorderBrush=Brush("#E4E4E4"),BorderThickness=new(1),Margin=new(0,0,12,0),CornerRadius=new(5),Child=card});
        }
        DockPanel.SetDock(totals,Dock.Top);page.Children.Add(totals);
        activeGrid=new ExcelGrid(new Field[]{new("Invoice","Invoice number",Width:220),new("Component","Payment component",Width:240),new("Amount","Allocated · USD",true,200)},service.Allocations,true);page.Children.Add(activeGrid);workspace.Content=page;
    }
    private void ShowReport(bool ledger)
    {
        var data=service.Report(ledger);var page=Page(ledger?"Debit / Credit":"Account Details","Read-only report · sample reporting date: 26 October 2026");SetupRibbon(false);
        activeGrid=new ExcelGrid(data.Fields,data.Rows,true);page.Children.Add(activeGrid);workspace.Content=page;
    }
    private void ShowSubmissions()
    {
        if(role!="Owner")return;
        var page=Page("Operator Reports","Review submitted operator reports independently of your working records.");SetupRibbon(false);
        var notice=Text("No report file selected. Import will become available when the file service is connected.",12,"#707070");notice.Margin=new(22,0,22,18);DockPanel.SetDock(notice,Dock.Top);page.Children.Add(notice);
        var data=service.Submissions();activeGrid=new ExcelGrid(data.Fields,data.Rows,true);page.Children.Add(activeGrid);workspace.Content=page;
    }
    internal void CheckScreens()
    {
        foreach(var selectedRole in new[]{"Operator","Owner"}) {
            role=selectedRole;BuildNavigation();
            bool visible=navigation.Children.OfType<Border>().Select(b=>b.Child).OfType<Button>().Any(b=>Equals(b.Content,"Operator Reports"));
            if(visible!=(role=="Owner"))throw new InvalidOperationException("Owner navigation gate");
            foreach(var show in new Action[]{ShowInvoices,ShowReceipts,()=>ShowReport(false),()=>ShowReport(true),ShowSubmissions}) {show();RenderSurface();}
        }
    }
    private System.Windows.Media.Imaging.RenderTargetBitmap RenderSurface()
    {
        var surface=(FrameworkElement)Content;surface.Measure(new(1440,880));surface.Arrange(new(0,0,1440,880));surface.UpdateLayout();
        Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);surface.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1440,880,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);return bitmap;
    }
    internal void ExportDesignPreview(string path,string screen="invoices")
    {
        switch(screen) {case "receipts":ShowReceipts();break;case "details":ShowReport(false);break;case "ledger":ShowReport(true);break;case "owner":roleSelector.SelectedItem="Owner";ShowSubmissions();break;default:ShowInvoices();break;}
        RenderSurface();var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(RenderSurface()));
        using var stream=System.IO.File.Create(path);encoder.Save(stream);
    }
}
