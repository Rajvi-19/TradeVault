using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;

internal sealed class PartyDialog:Window
{
    public WorksheetParty? Result{get;private set;}
    internal PartyDialog(WorkbookStore store,LoginSession session,bool assignment=false)
    {
        Title=assignment?"Assign unassigned worksheet":"Create party";Width=480;Height=300;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new(24)};
        panel.Children.Add(Ui.Text(Title,22,"#18243B",true));
        var hint=Ui.Text(assignment?"Select the party that owns ALL rows in this worksheet. Existing destination worksheets cannot be overwritten.":"Create a separate workspace for this party. Excel column labels remain unchanged.",12,"#64748B");hint.Margin=new(0,10,0,16);panel.Children.Add(hint);
        var input=new TextBox{MaxLength=200};var list=new ComboBox{ItemsSource=store.GetParties(session).Where(p=>!p.IsUnassigned).ToList(),MinHeight=34};
        if(assignment){panel.Children.Add(list);System.Windows.Automation.AutomationProperties.SetName(list,"Destination party");}
        else{panel.Children.Add(input);System.Windows.Automation.AutomationProperties.SetName(input,"Party name");}
        var error=Ui.Text("",12,"#B42318");error.Margin=new(0,8,0,8);panel.Children.Add(error);
        var action=Ui.Button(assignment?"Select destination":"Create party",()=>
        {
            try{Result=assignment?list.SelectedItem as WorksheetParty??throw new ArgumentException("Select a destination party."):store.CreateParty(session,input.Text);DialogResult=true;}
            catch(Exception ex){error.Text=ex.Message;}
        },"PrimaryButton");action.IsDefault=true;panel.Children.Add(action);Content=panel;
        Loaded+=(_,_)=>{if(!assignment)input.Focus();};
    }
}
