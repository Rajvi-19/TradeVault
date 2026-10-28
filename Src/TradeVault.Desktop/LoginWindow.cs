using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
public sealed class LoginWindow:Window
{
    private TextBox usernameInput=null!;
    internal void PreviewUsername(string text)=>usernameInput.Text=text;
    public LoginSession? Session{get;private set;}
    public LoginWindow(WorkbookStore store)
    {
        Width=540;Height=520;MinWidth=500;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var form=new StackPanel{Margin=new(30,24,30,22)};
        var brand=new DockPanel{Margin=new(0,0,0,22)};var badge=Ui.Badge("OFFLINE","#EAF6F0","#247653");DockPanel.SetDock(badge,Dock.Right);brand.Children.Add(badge);
        var identity=new StackPanel{Orientation=Orientation.Horizontal};var logo=Ui.BrandIcon(40);logo.Margin=new(0,0,12,0);identity.Children.Add(logo);identity.Children.Add(Ui.Text("TradeVault",24,"#18243B",true));brand.Children.Add(identity);form.Children.Add(brand);
        form.Children.Add(Ui.Text("Sign in to your workspace",24,"#18243B",true));var hint=Ui.Text("Manage export records and party accounts, securely offline.",12,"#64748B");hint.Margin=new(0,7,0,20);form.Children.Add(hint);
        form.Children.Add(new TextBlock{Text="Username",FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,6)});
        var username=new TextBox{Height=40,ToolTip="Case-sensitive: owner or operator",Margin=new(0,0,0,6)};usernameInput=username;form.Children.Add(username);
        var caseNote=Ui.Text("Usernames and passwords are case-sensitive.",11,"#64748B");caseNote.Margin=new(0,0,0,16);form.Children.Add(caseNote);
        form.Children.Add(new TextBlock{Text="Password",FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,6)});var password=new RevealPassword();form.Children.Add(password);
        var forgot=Ui.Button("Forgot password?",()=>RecoveryUi.Forgot(this,store,username.Text),"QuietButton");forgot.Foreground=Ui.Color("#2458D3");forgot.HorizontalAlignment=HorizontalAlignment.Right;forgot.Margin=new(0,4,0,10);form.Children.Add(forgot);
        var errors=Ui.Text("",12,"#B42318");var errorBox=new Border{Background=Ui.Color("#FFF2F0"),CornerRadius=new(5),Padding=new(10),Child=errors,Visibility=Visibility.Collapsed,Margin=new(0,0,0,10)};form.Children.Add(errorBox);
        var button=Ui.Button("Sign in",()=>{},"PrimaryButton","\uE72A");button.Height=42;button.IsDefault=true;form.Children.Add(button);
        var progress=Ui.Text("Checking your credentials…",12,"#64748B");progress.Margin=new(0,8,0,0);progress.Visibility=Visibility.Collapsed;form.Children.Add(progress);
        var foot=Ui.Text("Local accounts • Your records stay in this workspace",11,"#64748B");foot.Margin=new(0,18,0,0);foot.TextAlignment=TextAlignment.Center;form.Children.Add(foot);
        System.Windows.Automation.AutomationProperties.SetName(username,"Username");
        button.Click+=async(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(username.Text)||password.Secret.Length==0){errors.Text="Enter your username and password.";errorBox.Visibility=Visibility.Visible;return;}
            button.IsEnabled=false;username.IsEnabled=false;password.IsEnabled=false;forgot.IsEnabled=false;errorBox.Visibility=Visibility.Collapsed;progress.Visibility=Visibility.Visible;
            var name=username.Text;var secret=password.Secret;
            try{Session=await Task.Run(()=>store.Login(name,secret));password.Clear();if(!store.HasRecoveryCode(Session))RecoveryUi.Setup(this,store,Session);DialogResult=true;}
            catch(Exception ex){errors.Text=ex.Message;errorBox.Visibility=Visibility.Visible;password.Clear();}
            finally{button.IsEnabled=true;username.IsEnabled=true;password.IsEnabled=true;forgot.IsEnabled=true;progress.Visibility=Visibility.Collapsed;}
        };
        Ui.WindowFrame(this,new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=System.Windows.Media.Brushes.White},"TradeVault · Sign in");Loaded+=(_,_)=>username.Focus();
    }
}
