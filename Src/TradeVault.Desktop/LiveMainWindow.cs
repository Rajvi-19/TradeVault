using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
public sealed class MainWindow : Window
{
    private readonly WorksheetController controller;
    private readonly ContentControl content=new();
    private readonly TextBlock status=new(){Margin=new(12)};
    private readonly TextBox opening=new(){Text="0.00",Width=100};
    private readonly TextBox terms=new(){Text="90",Width=60};
    private readonly DatePicker asOf=new(){SelectedDate=DateTime.Today,Width=140};
    private ExcelGrid? grid; private bool dirty,refreshing;
    public MainWindow(WorkbookStore store,LoginSession session,string path)
    {
        controller=new(store,session);Title=$"TradeVault — {session.Role}";Width=1440;Height=880;MinWidth=1000;MinHeight=600;FontFamily=new("Segoe UI");Background=Brushes.White;
        var root=new DockPanel();Content=root;
        var heading=new TextBlock{Text=$"TRADEVAULT    {session.Username} / {session.Role}",FontSize=20,Foreground=Brushes.White,Background=Brushes.SeaGreen,Padding=new(16)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var tools=new WrapPanel{Margin=new(12)};
        tools.Children.Add(Button("New row",()=>{var row=controller.Add();Watch(row);dirty=true;grid!.Table.ScrollIntoView(row);}));
        tools.Children.Add(Button("Save",Save));tools.Children.Add(Button("Reload",()=>{if(Discard())Open(controller.Name);}));
        tools.Children.Add(Button("Delete row",()=>{if(grid?.Table.CurrentItem is GridRow row&&MessageBox.Show(this,"Delete selected row? Save to confirm.","Delete",MessageBoxButton.YesNo)==MessageBoxResult.Yes){controller.Rows.Remove(row);dirty=true;Calculate();}}));
        tools.Children.Add(new TextBlock{Text=" Opening USD: ",VerticalAlignment=VerticalAlignment.Center});tools.Children.Add(opening);
        tools.Children.Add(new TextBlock{Text=" Default terms: ",VerticalAlignment=VerticalAlignment.Center});tools.Children.Add(terms);
        tools.Children.Add(new TextBlock{Text=" As of: ",VerticalAlignment=VerticalAlignment.Center});tools.Children.Add(asOf);
        tools.Children.Add(Button("Recalculate",Calculate));tools.Children.Add(Button("Sign out",()=>{if(Discard()){dirty=false;Close();}}));
        DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        var tabs=new WrapPanel{Margin=new(12,0,12,8)};
        foreach(var (name,label) in new[]{("form","Invoice inputs — formet"),("details","PARTIES ACCOUNT DETAILS WISE"),("ledger","PARTIES ACCOUNT DEBIT CREDIT")})tabs.Children.Add(Button(label,()=>{if(Discard())Open(name);}));
        if(session.Role=="Owner")tabs.Children.Add(Button("Operator Reports",()=>MessageBox.Show(this,"Report-file import is not implemented yet. No sample submissions are loaded.")));
        DockPanel.SetDock(tabs,Dock.Top);root.Children.Add(tabs);
        var location=new TextBlock{Text="USD only • "+path,Margin=new(12,4,12,4),Foreground=Brushes.DimGray};DockPanel.SetDock(location,Dock.Bottom);root.Children.Add(location);
        DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);root.Children.Add(content);
        opening.TextChanged+=(_,_)=>{if(!refreshing)dirty=true;};terms.TextChanged+=(_,_)=>{if(!refreshing)dirty=true;};asOf.SelectedDateChanged+=(_,_)=>Calculate();
        Closing+=(_,e)=>{if(!Discard())e.Cancel=true;};Open("form");
    }
    private static Button Button(string label,Action action){var b=new Button{Content=label,Padding=new(10,6,10,6),Margin=new(0,0,8,0)};b.Click+=(_,_)=>{try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"TradeVault",MessageBoxButton.OK,MessageBoxImage.Error);}};return b;}
    private bool Discard(){grid?.Table.CommitEdit();grid?.Table.CommitEdit(DataGridEditingUnit.Row,true);return !dirty||MessageBox.Show(this,"Unsaved changes. Discard them?","TradeVault",MessageBoxButton.YesNo)==MessageBoxResult.Yes;}
    private void Watch(GridRow row)=>row.Changed+=_=>{if(!refreshing){dirty=true;status.Text="Unsaved changes — click Save.";}};
    private void Open(string name){controller.Load(name);refreshing=true;opening.Text=controller.Opening.ToString("0.00",CultureInfo.InvariantCulture);terms.Text=controller.Terms.ToString();refreshing=false;foreach(var r in controller.Rows)Watch(r);grid=new ExcelGrid(controller.Fields,controller.Rows);grid.Edited+=Calculate;content.Content=grid;opening.IsEnabled=name!="form";terms.IsEnabled=name=="form";dirty=false;status.Text=$"{controller.Rows.Count} saved rows • revision {controller.Revision}";}
    private void Options(){if(!decimal.TryParse(opening.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||decimal.Round(value,2)!=value)throw new ArgumentException("Opening balance: enter USD with at most two decimals.");if(!int.TryParse(terms.Text,out var days)||days<0||days>3650)throw new ArgumentException("Default terms must be 0–3650 days.");controller.Opening=value;controller.Terms=days;controller.AsOf=DateOnly.FromDateTime(asOf.SelectedDate??DateTime.Today);}
    private void Calculate(){if(grid is null)return;try{Options();refreshing=true;controller.Calculate();var errors=controller.Validate();status.Text=errors.Count>0?string.Join(" | ",errors.Take(3)):dirty?"Calculated — unsaved changes.":"Calculated from saved records.";}catch(Exception ex){status.Text=ex.Message;}finally{refreshing=false;}}
    private void Save(){if(grid is null)return;if(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Correct invalid cells before saving.");Options();refreshing=true;try{controller.Save();dirty=false;status.Text=$"Saved {controller.Rows.Count} rows to SQLite • revision {controller.Revision}";}finally{refreshing=false;}}
}
