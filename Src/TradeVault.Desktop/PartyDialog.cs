using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;

internal sealed class PartyDialog:Window
{
    public WorksheetParty? Result{get;private set;}
    internal PartyDialog(WorkbookStore store,LoginSession session,bool assignment=false)
    {
        Title=assignment?"Assign worksheet":"New party";Width=Math.Min(440,SystemParameters.WorkArea.Width);SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new(18)};
        panel.Children.Add(Ui.Text(Title,20,"#18243B",true));
        var hint=Ui.Text(assignment?"Choose the party that owns every row. The destination worksheet must be empty.":"Keep invoices and payments together for this party.",12,"#64748B");hint.Margin=new(0,6,0,14);panel.Children.Add(hint);
        var label=Ui.Text(assignment?"Destination party":"Party name",12,"#52627A",true);label.Margin=new(0,0,0,6);panel.Children.Add(label);
        var input=new TextBox{MaxLength=200,MinHeight=36,Padding=new(10,6,10,6)};var list=new ComboBox{ItemsSource=store.GetParties(session).Where(p=>!p.IsUnassigned).ToList(),MinHeight=36};
        if(assignment){panel.Children.Add(list);System.Windows.Automation.AutomationProperties.SetName(list,"Destination party");}
        else{panel.Children.Add(input);System.Windows.Automation.AutomationProperties.SetName(input,"Party name");}
        var error=Ui.Text("",12,"#B42318");error.Margin=new(0,8,0,0);error.Visibility=Visibility.Collapsed;panel.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,14,0,0)};
        var cancel=Ui.Button("Cancel",()=>DialogResult=false);cancel.IsCancel=true;cancel.Margin=new(0,0,8,0);cancel.Padding=new(14,6,14,6);actions.Children.Add(cancel);
        var action=Ui.Button(assignment?"Select destination":"Create party",()=>
        {
            try{Result=assignment?list.SelectedItem as WorksheetParty??throw new ArgumentException("Select a destination party."):store.CreateParty(session,input.Text);DialogResult=true;}
            catch(Exception ex){error.Text=ex.Message;error.Visibility=Visibility.Visible;}
        },"PrimaryButton");action.IsDefault=true;action.Padding=new(14,6,14,6);actions.Children.Add(action);panel.Children.Add(actions);Ui.WindowFrame(this,new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Title);
        Loaded+=(_,_)=>{if(!assignment)input.Focus();};
    }
}
