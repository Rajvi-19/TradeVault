using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
public sealed class LoginWindow : Window
{
    private TextBox usernameInput=null!;
    internal void PreviewUsername(string text)=>usernameInput.Text=text;
    public LoginSession? Session{get;private set;}
    public LoginWindow(WorkbookStore store)
    {
        Title="TradeVault | Sign in";Width=980;Height=650;MinWidth=820;MinHeight=600;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new Grid();root.ColumnDefinitions.Add(new(){Width=new GridLength(0.43,GridUnitType.Star)});root.ColumnDefinitions.Add(new(){Width=new GridLength(0.57,GridUnitType.Star)});Content=root;
        var brand=new DockPanel{Background=Ui.Color("#111E35"),Margin=new(0)};root.Children.Add(brand);
        var logo=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(36,38,0,0)};var mark=Ui.Badge("TV","#2F60D7","#FFFFFF");mark.Margin=new(0,0,12,0);logo.Children.Add(mark);logo.Children.Add(Ui.Text("TradeVault",23,"#FFFFFF",true));DockPanel.SetDock(logo,Dock.Top);brand.Children.Add(logo);
        var foot=Ui.Text("EXPORT OPERATIONS  /  USD",10,"#91A4C0",true);foot.Margin=new(36,0,24,32);DockPanel.SetDock(foot,Dock.Bottom);brand.Children.Add(foot);
        var story=new StackPanel{Margin=new(36,0,30,0),VerticalAlignment=VerticalAlignment.Center};
        story.Children.Add(Ui.Text("A clear view of\nyour export business.",31,"#FFFFFF",true));
        var description=Ui.Text("Invoices, shipments and party accounts.\nOne focused workspace, wherever you work.",14,"#ACBCD3");description.Margin=new(0,22,0,34);description.LineHeight=24;story.Children.Add(description);
        foreach(var (icon,label) in new[]{("\uE8A5","Familiar spreadsheet workflows"),("\uE8D4","Clear account records"),("\uE8B7","Works without an internet connection")})
        {var line=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(0,0,0,18)};var glyph=Ui.Icon(icon,"#8DAAF3",18);glyph.Margin=new(0,0,12,0);line.Children.Add(glyph);line.Children.Add(Ui.Text(label,12,"#D5DFEE"));story.Children.Add(line);}
        brand.Children.Add(story);
        var right=new Grid{Background=Brushes.White};Grid.SetColumn(right,1);root.Children.Add(right);
        var form=new StackPanel{Width=340,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};right.Children.Add(form);
        var badge=Ui.Badge("OFFLINE WORKSPACE","#EEF3FC","#52627A");badge.HorizontalAlignment=HorizontalAlignment.Left;badge.Margin=new(0,0,0,24);form.Children.Add(badge);
        form.Children.Add(Ui.Text("Welcome back",29,"#18243B",true));var note=Ui.Text("Sign in to continue to your workspace.",13,"#64748B");note.Margin=new(0,10,0,28);form.Children.Add(note);
        var userLabel=new Label{Content="Username",Padding=new(0,0,0,7),FontWeight=FontWeights.SemiBold};form.Children.Add(userLabel);
        var username=new TextBox{Height=44,Margin=new(0,0,0,18),ToolTip="Enter your assigned username"};userLabel.Target=username;form.Children.Add(username);
        usernameInput=username;
        var passwordLabel=new Label{Content="Password",Padding=new(0,0,0,7),FontWeight=FontWeights.SemiBold};form.Children.Add(passwordLabel);
        var password=new PasswordBox{Height=44};passwordLabel.Target=password;form.Children.Add(password);
        System.Windows.Automation.AutomationProperties.SetName(username,"Username");System.Windows.Automation.AutomationProperties.SetName(password,"Password");
        var errors=Ui.Text("",12,"#B42318");var errorBox=new Border{Background=Ui.Color("#FFF2F0"),CornerRadius=new(5),Padding=new(12),Margin=new(0,14,0,0),Child=errors,Visibility=Visibility.Collapsed};form.Children.Add(errorBox);
        var button=Ui.Button("Sign in to workspace",()=>{},"PrimaryButton","\uE72A");button.IsDefault=true;button.Height=46;button.Margin=new(0,22,0,0);form.Children.Add(button);
        var progress=Ui.Text("Verifying your credentials…",12,"#64748B");progress.Margin=new(0,10,0,0);progress.Visibility=Visibility.Collapsed;form.Children.Add(progress);
        var help=Ui.Text("Use the account assigned to you by your administrator.",11,"#64748B");help.Margin=new(0,24,0,0);help.TextAlignment=TextAlignment.Center;form.Children.Add(help);
        button.Click+=async(_,_)=>
        {
            if(string.IsNullOrWhiteSpace(username.Text)||password.SecurePassword.Length==0){errors.Text="Enter your username and password.";errorBox.Visibility=Visibility.Visible;return;}
            button.IsEnabled=false;username.IsEnabled=false;password.IsEnabled=false;errorBox.Visibility=Visibility.Collapsed;progress.Visibility=Visibility.Visible;
            var name=username.Text;var secret=password.Password;
            try{Session=await Task.Run(()=>store.Login(name,secret));password.Clear();DialogResult=true;}
            catch(Exception ex){errors.Text=ex.Message;errorBox.Visibility=Visibility.Visible;password.Clear();}
            finally{button.IsEnabled=true;username.IsEnabled=true;password.IsEnabled=true;progress.Visibility=Visibility.Collapsed;}
        };
        Ui.WindowFrame(this,root,"TradeVault · Sign in");
        Loaded+=(_,_)=>username.Focus();
    }
}
