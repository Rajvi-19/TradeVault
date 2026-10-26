using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TradeVault.Desktop;

public sealed class MainWindow : Window
{
    private readonly IPrototypeService service;
    private readonly ContentControl workspace = new();
    private readonly StackPanel navigation = new();
    private readonly TextBlock status = new() { Text = "DEMO ONLY · Synthetic data · No files written", Margin = new(20,10,20,10) };
    private string role = "Operator";
    public MainWindow() : this(new MockPrototypeService()) { }
    public MainWindow(IPrototypeService service)
    {
        this.service = service;
        Title = "TradeVault · Desktop prototype"; Width = 1380; Height = 850; MinWidth = 980; MinHeight = 620;
        FontFamily = new("Segoe UI"); Background = new SolidColorBrush(Color.FromRgb(243,246,249));
        var root = new DockPanel();
        var header = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(22,45,64)), LastChildFill = true };
        var identity = new TextBlock { Text = "TRADEVAULT   /   USB WORKSPACE", Foreground = Brushes.White, FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new(22,18,22,18) };
        header.Children.Add(identity); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
        var warning = new TextBlock { Text = "INTERFACE PROTOTYPE — role selection bypasses login. All data, totals, validation and submission previews use local mock services.", Background = new SolidColorBrush(Color.FromRgb(255,239,201)), Padding = new(20,10,20,10), TextWrapping = TextWrapping.Wrap };
        DockPanel.SetDock(warning,Dock.Top); root.Children.Add(warning);
        DockPanel.SetDock(status,Dock.Bottom); root.Children.Add(status);
        navigation.Width = 215; navigation.Margin = new(14); DockPanel.SetDock(navigation,Dock.Left); root.Children.Add(navigation);
        workspace.Margin = new(8,20,22,12); root.Children.Add(workspace); Content = root;
        service.Invoices.ToList().ForEach(Watch);
        Login();
    }
    private void Watch(GridRow row) => row.Changed += r => { service.Validate(r); status.Text = "Unsaved mock inputs · Calculated fixtures are not refreshed · No disk persistence"; };
    private void Login()
    {
        navigation.Children.Clear(); var panel = Page("Open a demo workspace", "Authentication is not connected. Select a role to inspect its interface; no password is requested or verified.");
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0,24,0,0) };
        foreach (var name in new[] { "Operator", "Owner" }) choices.Children.Add(Button($"Continue as {name} (demo)", () => { role=name; BuildNavigation(); ShowInvoices(); }));
        panel.Children.Add(choices); workspace.Content = panel;
    }
    private void BuildNavigation()
    {
        navigation.Children.Clear(); navigation.Children.Add(new TextBlock { Text = $"{role.ToUpperInvariant()} · DEMO", FontWeight = FontWeights.Bold, Margin = new(8,8,0,24) });
        foreach (var (label,action) in new (string,Action)[] { ("Invoices & shipments",ShowInvoices),("Receipt allocations",ShowReceipts),("Account Details",()=>ShowReport(false)),("Debit / Credit",()=>ShowReport(true)) }) {
            var button = Button(label,action); button.Margin = new(0,0,0,10); navigation.Children.Add(button);
        }
        if (role == "Owner") navigation.Children.Add(Button("Operator Reports",ShowSubmissions));
        navigation.Children.Add(Button("Switch demo role",Login));
    }
    private static DockPanel Page(string title,string hint)
    {
        var page = new DockPanel();
        var heading = new StackPanel { Margin = new(0,0,0,18) };
        heading.Children.Add(new TextBlock { Text=title, FontSize=28, FontWeight=FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text=hint, TextWrapping=TextWrapping.Wrap, Margin=new(0,7,0,0) });
        DockPanel.SetDock(heading,Dock.Top); page.Children.Add(heading); return page;
    }
    private static Button Button(string text,Action action)
    { var b = new Button { Content=text }; b.Click += (_,_) => action(); return b; }
    private void ShowInvoices()
    {
        var page = Page("Invoices & shipments", "USD throughout · Shipping defaults to 3,800.00 · Terms default to 90 days, editable · Blue columns: calculated / locked");
        var actions = new StackPanel { Orientation=Orientation.Horizontal, Margin=new(0,0,0,12) };
        var messages = new TextBlock { TextWrapping=TextWrapping.Wrap, Foreground=Brushes.DarkRed, Margin=new(0,6,0,8), MaxHeight=100 };
        var grid = new ExcelGrid(service.InvoiceFields,service.Invoices);
        actions.Children.Add(Button("Add invoice row",()=> { var row=service.NewInvoice(); Watch(row); service.Invoices.Add(row); grid.Table.SelectedItem=row; grid.Table.ScrollIntoView(row); status.Text="New unsaved mock row"; }));
        actions.Children.Add(Button("Validate / mock save",()=> {
            grid.Table.CommitEdit(); grid.Table.CommitEdit(DataGridEditingUnit.Row,true);
            var result=service.SaveInvoices(); messages.Text=string.Join("\n",result.Issues.Select(x=>x.Message).Distinct()); status.Text=result.Message;
        }));
        actions.Children.Add(Button("Paste sample rows",()=> {
            grid.Table.CurrentCell = new DataGridCellInfo(service.Invoices[0],grid.Table.Columns[0]);
            grid.Paste("Demo Azure Ltd\tDEMO-101\t2026-10-20\nDemo Copper Ltd\tDEMO-102\t2026-10-21");
        }));
        grid.Edited += () => messages.Text=string.Join("\n",service.Invoices.SelectMany(r=>service.Validate(r).Issues).Select(x=>x.Message).Distinct());
        DockPanel.SetDock(actions,Dock.Top); page.Children.Add(actions); DockPanel.SetDock(messages,Dock.Bottom); page.Children.Add(messages); page.Children.Add(grid); workspace.Content=page;
    }
    private void ShowReceipts()
    {
        var page = Page("Receipt allocations", "Synthetic receipt DEMO-R001 · USD · Editing / allocation posting awaits Developer B's service.");
        var summary=service.ReceiptSummary;
        var totals=new TextBlock { Text=$"Receipt total: USD {summary.Total}     Allocated: USD {summary.Allocated}     Unallocated: USD {summary.Unallocated}", FontSize=18, Margin=new(0,0,0,20), TextWrapping=TextWrapping.Wrap };
        DockPanel.SetDock(totals,Dock.Top); page.Children.Add(totals);
        page.Children.Add(new ExcelGrid(new Field[] {new("Invoice","Invoice"),new("Component","Component",Width:220),new("Amount","Allocated / USD",true,200)},service.Allocations,true)); workspace.Content=page;
    }
    private void ShowReport(bool ledger)
    {
        var data=service.Report(ledger); var page=Page(ledger?"Debit / Credit":"Account Details", "Read-only synthetic service snapshot · As of 26 Oct 2026 · Report calculations and exports are not connected.");
        page.Children.Add(new ExcelGrid(data.Fields,data.Rows,true)); workspace.Content=page;
    }
    private void ShowSubmissions()
    {
        if (role!="Owner") return;
        var page=Page("Operator Reports", "Owner review area · Submitted snapshots remain separate from owner working invoices. No files are parsed or imported in this prototype.");
        var preview=new TextBlock { Text="Select Preview demo submission to inspect a synthetic incoming report.", TextWrapping=TextWrapping.Wrap, Margin=new(0,12,0,18) };
        var toolbar=new StackPanel(); toolbar.Children.Add(Button("Preview demo submission",()=>preview.Text="MOCK PREVIEW · DEMO-SUBMISSION-002 / revision 1\nOperator demo USB · Data through 26 Oct 2026 · Account Details + Debit/Credit\nStatus: not imported. File validation, duplicate checks and import await Developer B.")); toolbar.Children.Add(preview);
        DockPanel.SetDock(toolbar,Dock.Top); page.Children.Add(toolbar);
        var data=service.Submissions(); page.Children.Add(new ExcelGrid(data.Fields,data.Rows,true)); workspace.Content=page;
    }
    internal void CheckScreens()
    {
        foreach (var selectedRole in new[] { "Operator", "Owner" })
        {
            role=selectedRole; BuildNavigation();
            bool ownerVisible=navigation.Children.OfType<Button>().Any(x=>Equals(x.Content,"Operator Reports"));
            if (ownerVisible != (role=="Owner")) throw new InvalidOperationException("Owner navigation gate");
            foreach (var show in new Action[] { ShowInvoices,ShowReceipts,()=>ShowReport(false),()=>ShowReport(true),ShowSubmissions })
            {
                show(); Measure(new Size(1380,850)); Arrange(new Rect(0,0,1380,850)); UpdateLayout();
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1380,850,96,96,PixelFormats.Pbgra32);
                bitmap.Render(this); // In-memory layout/render smoke check; no host-disk image.
            }
        }
    }
}
