using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal sealed class TransferReceiptDialog:Window
{
    private sealed record Choice(string Id,string Name){public override string ToString()=>Name;}
    private readonly ComboBox master=new(){MinHeight=36,IsEditable=false,IsTextSearchEnabled=true,SelectedValuePath="Id",MaxDropDownHeight=260};
    private readonly ComboBox receiving=new(){MinHeight=36,IsEditable=false,IsTextSearchEnabled=true,SelectedValuePath="Id",MaxDropDownHeight=260};
    private readonly DateCellEditor date=new(){MinHeight=36,Padding=new(10,6,10,6),BorderThickness=new(1),BorderBrush=Ui.Color("#DCE3ED"),Background=Ui.Color("#FFFFFF")};
    private readonly TextBox amount=new(){MinHeight=36,TextAlignment=TextAlignment.Left};
    private readonly TextBox usdValue=new(){MinHeight=36,MaxLength=100,TextAlignment=TextAlignment.Left,ToolTip="Value of one US dollar at the payment date. Recorded as text; receipt amount stays in USD."};
    private readonly TextBox description=new(){MinHeight=64,MaxLength=500,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private readonly TextBlock error=Ui.Text("",12,"#B42318");
    private readonly Button save;
    internal TransferReceipt? Result{get;private set;}
    internal TransferReceiptDialog(WorkbookStore store,TransferReceiptKind kind)
    {
        var party=kind==TransferReceiptKind.Party;
        Width=Math.Min(470,SystemParameters.WorkArea.Width);SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        master.ItemsSource=party?store.GetParties().Where(p=>!p.IsUnassigned).Select(p=>new Choice(p.Id.ToString(),p.Name)).ToList():store.GetAgents().Select(a=>new Choice(a.Id,a.Name)).ToList();
        master.SetResourceReference(StyleProperty,"FilterSelectorStyle");System.Windows.Controls.TextSearch.SetTextPath(master,"Name");
        var nameText=new FrameworkElementFactory(typeof(TextBlock));nameText.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Name"));nameText.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);master.ItemTemplate=new DataTemplate{VisualTree=nameText};
        receiving.ItemsSource=store.GetAgents().Select(a=>new Choice(a.Id,a.Name)).ToList();receiving.SetResourceReference(StyleProperty,"FilterSelectorStyle");receiving.ItemTemplate=master.ItemTemplate;TextSearch.SetTextPath(receiving,"Name");
        date.Value=DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        var panel=new StackPanel{Margin=new(20)};panel.Children.Add(Ui.Text("Add New Receipt",21,"#18243B",true));
        var hint=Ui.Text(party?"Party payment to an agent":"Agent payment to your company. Allocated to oldest invoices first; excess stays unallocated.",12,"#64748B");hint.Margin=new(0,5,0,8);panel.Children.Add(hint);
        foreach(var (label,control) in new (string,FrameworkElement)[]{(party?"Party name":"Agent name",master),("Payment date",date),("Receipt amount (USD)",amount),("Reference / description (optional)",description)})
        {
            var text=Ui.Text(label,12,"#52627A",true);text.Margin=new(0,10,0,5);panel.Children.Add(text);panel.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,label);
            if(party&&ReferenceEquals(control,master)){var agentLabel=Ui.Text("Receiving agent",12,"#52627A",true);agentLabel.Margin=new(0,10,0,5);panel.Children.Add(agentLabel);panel.Children.Add(receiving);System.Windows.Automation.AutomationProperties.SetName(receiving,"Receiving agent");}
            if(ReferenceEquals(control,amount)){var valueLabel=Ui.Text("USD value",12,"#52627A",true);valueLabel.Margin=new(0,10,0,5);panel.Children.Add(valueLabel);panel.Children.Add(usdValue);System.Windows.Automation.AutomationProperties.SetName(usdValue,"USD value at payment date");}
        }
        if(master.Items.Count==0){var empty=Ui.Text(party?"Add a party in Party Master first.":"Add an agent in Agent Master first.",12,"#64748B");empty.Margin=new(0,8,0,0);panel.Children.Add(empty);}
        if(party&&receiving.Items.Count==0){var empty=Ui.Text("Add a receiving agent in Agent Master first.",12,"#64748B");empty.Margin=new(0,8,0,0);panel.Children.Add(empty);}
        error.Margin=new(0,10,0,0);error.Visibility=Visibility.Collapsed;panel.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,16,0,0)};
        var cancel=Ui.Button("Cancel",()=>DialogResult=false);cancel.IsCancel=true;cancel.Margin=new(0,0,8,0);actions.Children.Add(cancel);
        save=Ui.Button("Save receipt",()=>
        {
            try
            {
                if(Result!=null)return;
                var choice=master.SelectedItem as Choice??throw new ArgumentException(party?"Select a party from Party Master.":"Select an agent from Agent Master.");
                if(!date.TryCommitDate()||!DateOnly.TryParseExact(date.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var day))throw new ArgumentException("Enter a valid payment date.");
                if(!decimal.TryParse(amount.Text,NumberStyles.AllowDecimalPoint|NumberStyles.AllowThousands|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var usd))throw new ArgumentException("Enter a valid USD amount, for example 1250.50.");
                var agentId=party?(receiving.SelectedItem as Choice)?.Id:null;
                Result=store.AddTransferReceipt(kind,choice.Id,day,usd,description.Text,agentId,usdValue.Text);DialogResult=true;
            }
            catch(Exception ex){error.Text=ex.Message;error.Visibility=Visibility.Visible;}
        },"PrimaryButton");save.IsDefault=true;save.IsEnabled=master.Items.Count>0&&(!party||receiving.Items.Count>0);actions.Children.Add(save);panel.Children.Add(actions);
        Ui.WindowFrame(this,new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},party?"New Party Receipt":"New Agent Receipt");Loaded+=(_,_)=>master.Focus();
    }
    internal void FillForCheck(string id,string day,string usd,string note,string? agentId=null,string value=""){master.SelectedValue=id;receiving.SelectedValue=agentId??receiving.Items.Cast<Choice>().FirstOrDefault()?.Id;date.Value=day;amount.Text=usd;description.Text=note;usdValue.Text=value;}
    internal void SubmitForCheck()=>save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal bool HasError=>error.Visibility==Visibility.Visible;
    internal string DefaultDate=>date.Value;
    internal bool AmountIsLeftAligned=>amount.TextAlignment==TextAlignment.Left;
    internal string SelectedNameForCheck()
    {
        master.ApplyTemplate();master.UpdateLayout();
        static IEnumerable<TextBlock> Texts(DependencyObject node){if(node is TextBlock text)yield return text;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++)foreach(var item in Texts(System.Windows.Media.VisualTreeHelper.GetChild(node,i)))yield return item;}
        return string.Join("",Texts(master).Select(text=>text.Text));
    }
}

