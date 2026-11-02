using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;
internal sealed class ExpenseReceiptDialog:Window
{
    private sealed record Choice(string Id,string Name){public override string ToString()=>Name;}
    private readonly ComboBox party=new(){MinHeight=36,SelectedValuePath="Id",DisplayMemberPath="Name"};
    private readonly ComboBox invoice=new(){MinHeight=36,SelectedValuePath="Id",DisplayMemberPath="Name"};
    private readonly DateCellEditor date=new(){MinHeight=36,BorderThickness=new(1),Padding=new(10,6,10,6),BorderBrush=Ui.Color("#DCE3ED"),Background=Ui.Color("#FFFFFF")};
    private readonly TextBox amount=new(){MinHeight=36};
    private readonly TextBox description=new(){MinHeight=64,MaxLength=500,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true};
    private readonly Button save;
    internal ExpenseReceipt? Result{get;private set;}
    internal ExpenseReceiptDialog(WorkbookStore store)
    {
        Width=470;SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        party.SetResourceReference(StyleProperty,"FilterSelectorStyle");invoice.SetResourceReference(StyleProperty,"FilterSelectorStyle");
        party.ItemsSource=store.GetParties().Where(p=>!p.IsUnassigned).Select(p=>new Choice(p.Id.ToString(),p.Name)).ToList();
        party.SelectionChanged+=(_,_)=>{invoice.SelectedItem=null;invoice.ItemsSource=party.SelectedItem is Choice p?store.GetExpenseInvoices(Guid.Parse(p.Id)).Select(i=>new Choice(i.Id,$"{i.Number} · {i.Date:dd-MM-yyyy}")).ToList():null;};
        date.Value=DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var panel=new StackPanel{Margin=new(20)};panel.Children.Add(Ui.Text("New Expense Receipt",21,"#18243B",true));
        panel.Children.Add(Ui.Text("Record an invoice expense paid from funds held by its agent.",12,"#64748B"));
        foreach(var (label,control) in new (string,FrameworkElement)[]{("Party name",party),("Proforma invoice",invoice),("Expense date",date),("Expense amount (USD)",amount),("Description",description)})
        {
            var text=Ui.Text(label,12,"#52627A",true);text.Margin=new(0,10,0,5);panel.Children.Add(text);panel.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,label);
        }
        var error=Ui.Text("",12,"#B42318");error.Visibility=Visibility.Collapsed;panel.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,16,0,0)};
        var cancel=Ui.Button("Cancel",()=>DialogResult=false);cancel.IsCancel=true;cancel.Margin=new(0,0,8,0);actions.Children.Add(cancel);
        save=Ui.Button("Save expense",()=>
        {
            try
            {
                var p=party.SelectedItem as Choice??throw new ArgumentException("Select a party.");var i=invoice.SelectedItem as Choice??throw new ArgumentException("Select an invoice for this party.");
                if(!date.TryCommitDate()||!DateOnly.TryParseExact(date.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var day))throw new ArgumentException("Enter a valid expense date.");
                if(!decimal.TryParse(amount.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var usd))throw new ArgumentException("Enter a valid expense amount in USD.");
                Result=store.AddExpenseReceipt(Guid.Parse(p.Id),i.Id,day,usd,description.Text);DialogResult=true;
            }
            catch(Exception ex){error.Text=ex.Message;error.Visibility=Visibility.Visible;}
        },"PrimaryButton");save.IsDefault=true;actions.Children.Add(save);panel.Children.Add(actions);
        Ui.WindowFrame(this,new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},"New Expense Receipt");Loaded+=(_,_)=>party.Focus();
    }
    internal void FillForCheck(Guid partyId,string invoiceId){party.SelectedValue=partyId.ToString();invoice.SelectedValue=invoiceId;date.Value="2026-11-01";amount.Text="25.50";description.Text="SYNTHETIC invoice handling expense";}
    internal void SubmitForCheck()=>save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
}
