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
        Width=Math.Min(480,SystemParameters.WorkArea.Width);Height=Math.Min(490,SystemParameters.WorkArea.Height);MinWidth=Math.Min(400,SystemParameters.WorkArea.Width);MinHeight=Math.Min(460,SystemParameters.WorkArea.Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var layout=new DockPanel{Background=System.Windows.Media.Brushes.White};
        var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(28,16,28,16)};
        var logo=Ui.BrandIcon(32);logo.Margin=new(0,0,10,0);brand.Children.Add(logo);brand.Children.Add(Ui.Text("TradeVault",21,"#18243B",true));
        var header=new Border{Background=System.Windows.Media.Brushes.White,BorderBrush=Ui.Color("#E2E8F0"),BorderThickness=new(0,0,0,1),Child=brand};DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
        var form=new StackPanel{Margin=new(28,24,28,24)};
        form.Children.Add(new TextBlock{Text="Sign in",FontSize=26,FontWeight=FontWeights.SemiBold,Foreground=Ui.Color("#18243B"),Margin=new(0,0,0,22)});
        form.Children.Add(new TextBlock{Text="Username",FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,6)});
        var username=new TextBox{Height=40,MaxLength=128,ToolTip="Enter your account username",Margin=new(0,0,0,16)};username.SetResourceReference(StyleProperty,"AuthTextBox");usernameInput=username;form.Children.Add(username);
        var passwordHeader=new DockPanel{Margin=new(0,0,0,6)};
        var forgot=Ui.Button("Forgot password?",()=>RecoveryUi.Forgot(this,store,username.Text),"QuietButton");forgot.Foreground=Ui.Color("#2458D3");forgot.FontSize=11;forgot.MinHeight=20;forgot.Padding=new(0);DockPanel.SetDock(forgot,Dock.Right);passwordHeader.Children.Add(forgot);passwordHeader.Children.Add(Ui.Text("Password",12,"#18243B",true));form.Children.Add(passwordHeader);
        var password=new RevealPassword();password.Margin=new(0,0,0,18);form.Children.Add(password);
        var errors=Ui.Text("",12,"#B42318");var errorBox=new Border{Background=Ui.Color("#FFF2F0"),CornerRadius=new(5),Padding=new(10),Child=errors,Visibility=Visibility.Collapsed,Margin=new(0,0,0,12)};form.Children.Add(errorBox);
        var button=Ui.Button("Sign in",()=>{},"PrimaryButton");button.Height=42;button.IsDefault=true;form.Children.Add(button);var signInContent=button.Content;
        form.Children.Add(new TextBlock{Text="Usernames and passwords are case-sensitive.",FontSize=11,Foreground=Ui.Color("#64748B"),TextWrapping=TextWrapping.Wrap,Margin=new(0,16,0,0),LineHeight=17});
        System.Windows.Automation.AutomationProperties.SetName(username,"Username");
        button.Click+=async(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(username.Text)||password.Secret.Length==0){errors.Text="Enter your username and password.";errorBox.Visibility=Visibility.Visible;return;}
            button.IsEnabled=false;username.IsEnabled=false;password.IsEnabled=false;forgot.IsEnabled=false;errorBox.Visibility=Visibility.Collapsed;button.Content="Signing in…";
            var name=username.Text;var secret=password.Secret;
            try{Session=await Task.Run(()=>store.Login(name,secret));password.Clear();if(!store.HasRecoveryCode(Session))RecoveryUi.Setup(this,store,Session);DialogResult=true;}
            catch(Exception ex){errors.Text=ex.Message;errorBox.Visibility=Visibility.Visible;password.Clear();}
            finally{button.IsEnabled=true;username.IsEnabled=true;password.IsEnabled=true;forgot.IsEnabled=true;button.Content=signInContent;}
        };
        Closing+=(_,e)=>{if(!button.IsEnabled&&Session==null)e.Cancel=true;};
        layout.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Stretch});
        Ui.WindowFrame(this,layout,"TradeVault · Sign in");Loaded+=(_,_)=>username.Focus();
    }
}
