using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal sealed class NewInvoiceDialog:Window
{
    private readonly ComboBox partyInput=new(){MinHeight=36,IsEditable=false,IsTextSearchEnabled=true,SelectedValuePath="Id"};
    private readonly TextBox country=new(){MaxLength=200,MinHeight=36};
    private readonly ComboBox agent=new(){MinHeight=36,IsEditable=false,IsTextSearchEnabled=true,SelectedValuePath="Id",MaxDropDownHeight=260};
    private readonly TextBox proforma=new(){MaxLength=100,MinHeight=36};
    private readonly DateCellEditor date=new(){MinHeight=36,Padding=new(10,6,10,6),BorderThickness=new(1),BorderBrush=Ui.Color("#DCE3ED"),Background=Ui.Color("#FFFFFF")};
    private readonly TextBlock error=Ui.Text("",12,"#B42318");
    private readonly Button addRecord;
    internal string? CreatedId{get;private set;}
    internal Guid CreatedPartyId{get;private set;}
    internal NewInvoiceDialog(WorkbookStore store,Guid party,int revision,string? selectedAgentId=null)
    {
        Width=Math.Min(480,SystemParameters.WorkArea.Width);SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;ResizeMode=ResizeMode.NoResize;
        var parties=store.GetParties().Where(p=>!p.IsUnassigned).ToList();var revisions=parties.ToDictionary(p=>p.Id,p=>p.Id==party?revision:store.Load("form",p.Id).Revision);
        partyInput.ItemsSource=parties;partyInput.SetResourceReference(StyleProperty,"FilterSelectorStyle");partyInput.SelectedValue=party;System.Windows.Controls.TextSearch.SetTextPath(partyInput,"Name");
        agent.ItemsSource=store.GetAgents();agent.SetResourceReference(StyleProperty,"PartySelectorStyle");
        if(!string.IsNullOrEmpty(selectedAgentId))agent.SelectedValue=selectedAgentId;
        var agentText=new FrameworkElementFactory(typeof(TextBlock));agentText.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Name"));agentText.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);agent.ItemTemplate=new DataTemplate{VisualTree=agentText};System.Windows.Controls.TextSearch.SetTextPath(agent,"Name");
        var panel=new StackPanel{Margin=new(20)};
        panel.Children.Add(Ui.Text("New Invoice",21,"#18243B",true));
        var hint=Ui.Text("Select the party and enter invoice details. These five values are fixed after creation.",12,"#64748B");hint.Margin=new(0,6,0,8);panel.Children.Add(hint);
        foreach(var (label,editor) in new (string,FrameworkElement)[]{("Party name",partyInput),("Agent name",agent),("Invoice number",proforma),("Invoice date",date),("Country name",country)})
        {
            var text=Ui.Text(label,12,"#52627A",true);text.Margin=new(0,10,0,5);panel.Children.Add(text);panel.Children.Add(editor);
            System.Windows.Automation.AutomationProperties.SetName(editor,label);
        }
        if(agent.Items.Count==0){var empty=Ui.Text("No agents yet. Add an agent in Agent Master before creating an invoice.",12,"#64748B");empty.Margin=new(0,8,0,0);panel.Children.Add(empty);}
        date.Value=DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        error.Margin=new(0,10,0,0);error.Visibility=Visibility.Collapsed;panel.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,16,0,0)};
        var cancel=Ui.Button("Cancel",()=>DialogResult=false);cancel.IsCancel=true;cancel.Margin=new(0,0,8,0);actions.Children.Add(cancel);
        var add=Ui.Button("Add record",()=>
        {
            try
            {
                if(!date.TryCommitDate()||!DateOnly.TryParseExact(date.Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var entered))throw new ArgumentException("Enter a valid proforma invoice date.");
                var selected=agent.SelectedItem as AgentMasterEntry??throw new ArgumentException("Select an agent from Agent Master. Add agents on the Agent Master tab first.");
                var selectedParty=partyInput.SelectedItem as WorksheetParty??throw new ArgumentException("Select a party from Party Master.");
                CreatedId=store.CreateRegisterInvoice(selectedParty.Id,revisions[selectedParty.Id],new(country.Text,selected.Name,proforma.Text,entered,selected.Id));CreatedPartyId=selectedParty.Id;DialogResult=true;
            }
            catch(Exception ex){error.Text=ex.Message;error.Visibility=Visibility.Visible;}
        },"PrimaryButton");addRecord=add;add.IsDefault=true;actions.Children.Add(add);panel.Children.Add(actions);
        Ui.WindowFrame(this,new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},"New Invoice");Loaded+=(_,_)=>agent.Focus();
    }
    internal void FillForCheck(NewRegisterInvoice input){country.Text=input.CountryName;agent.SelectedItem=agent.Items.Cast<AgentMasterEntry>().FirstOrDefault(a=>string.Equals(a.Name,input.AgentName,StringComparison.OrdinalIgnoreCase));proforma.Text=input.ProformaInvoiceNumber;date.Value=input.ProformaInvoiceDate.ToString("yyyy-MM-dd");}
    internal void SubmitForCheck()=>addRecord.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal void SelectPartyForCheck(Guid id)=>partyInput.SelectedValue=id;
    internal bool HasValidationError=>error.Visibility==Visibility.Visible;
    internal string SelectedAgentTextForCheck()
    {
        agent.ApplyTemplate();agent.UpdateLayout();Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
        static IEnumerable<TextBlock> Texts(DependencyObject node){if(node is TextBlock text)yield return text;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Texts(System.Windows.Media.VisualTreeHelper.GetChild(node,i)))yield return child;}
        return string.Join("",Texts(agent).Select(text=>text.Text));
    }
}
