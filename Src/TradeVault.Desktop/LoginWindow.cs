using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
public sealed class LoginWindow : Window
{
    public LoginSession? Session {get;private set;}
    public LoginWindow(WorkbookStore store)
    {
        Title="TradeVault — Sign in";Width=440;Height=370;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var panel=new StackPanel{Margin=new(32)};Content=panel;panel.Children.Add(new TextBlock{Text="TradeVault",FontSize=28,Foreground=Brushes.DarkGreen,Margin=new(0,0,0,16)});
        panel.Children.Add(new TextBlock{Text="Username"});var username=new TextBox{Margin=new(0,4,0,12)};panel.Children.Add(username);
        panel.Children.Add(new TextBlock{Text="Password"});var password=new PasswordBox{Margin=new(0,4,0,12)};panel.Children.Add(password);
        var errors=new TextBlock{Foreground=Brushes.DarkRed,TextWrapping=TextWrapping.Wrap};panel.Children.Add(errors);
        var button=new Button{Content="Sign in",IsDefault=true,Padding=new(12),Margin=new(0,12,0,0)};panel.Children.Add(button);
        button.Click+=async(_,_)=>{button.IsEnabled=false;var name=username.Text;var secret=password.Password;try{Session=await Task.Run(()=>store.Login(name,secret));password.Clear();DialogResult=true;}catch(Exception ex){errors.Text=ex.Message;password.Clear();}finally{button.IsEnabled=true;}};
    }
}
