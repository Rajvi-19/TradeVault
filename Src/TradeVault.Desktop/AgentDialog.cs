using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal sealed class AgentDialog:Window
{
    internal AgentMasterEntry? Result{get;private set;}
    internal AgentDialog(WorkbookStore store)
    {
        Width=Math.Min(440,SystemParameters.WorkArea.Width);SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new(20)};panel.Children.Add(Ui.Text("New Agent",20,"#18243B",true));
        var hint=Ui.Text("Create an agent to select when adding an invoice.",12,"#64748B");hint.Margin=new(0,6,0,16);panel.Children.Add(hint);
        var label=Ui.Text("Agent name",12,"#52627A",true);label.Margin=new(0,0,0,6);panel.Children.Add(label);
        var input=new TextBox{MaxLength=200,MinHeight=36,Padding=new(10,6,10,6)};System.Windows.Automation.AutomationProperties.SetName(input,"Agent name");panel.Children.Add(input);
        var error=Ui.Text("",12,"#B42318");error.Margin=new(0,10,0,0);error.Visibility=Visibility.Collapsed;panel.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,16,0,0)};
        var cancel=Ui.Button("Cancel",()=>DialogResult=false);cancel.IsCancel=true;cancel.Margin=new(0,0,8,0);actions.Children.Add(cancel);
        var create=Ui.Button("Create agent",()=>{try{Result=store.CreateAgent(input.Text);DialogResult=true;}catch(Exception ex){error.Text=ex.Message;error.Visibility=Visibility.Visible;}},"PrimaryButton");create.IsDefault=true;actions.Children.Add(create);panel.Children.Add(actions);
        Ui.WindowFrame(this,new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},"New Agent");Loaded+=(_,_)=>input.Focus();
    }
}
