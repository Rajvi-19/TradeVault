using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
internal static class RecoveryUi
{
    private static (Window,StackPanel) Shell(Window owner,string title,int height=600)
    {
        var dialog=new Window{Owner=owner,Width=560,Height=height,MinWidth=520,MinHeight=height,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new StackPanel{Margin=new(26)};panel.Children.Add(Ui.Text(title,24,"#18243B",true));
        Ui.WindowFrame(dialog,new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},title);return(dialog,panel);
    }
    private static T Field<T>(StackPanel panel,string title,T input)where T:FrameworkElement
    {panel.Children.Add(new TextBlock{Text=title,FontWeight=FontWeights.SemiBold,Margin=new(0,12,0,5)});panel.Children.Add(input);System.Windows.Automation.AutomationProperties.SetName(input,title);return input;}
    internal static void Setup(Window owner,WorkbookStore store,LoginSession session)
    {
        var (dialog,panel)=Shell(owner,"Save your recovery code",440);
        panel.Children.Add(Ui.Text("This single-use code lets you reset your password without internet access. Store it on paper or a separate safe USB, not beside this database. Creating a new code replaces the old one.",13,"#64748B"));
        var code=Field(panel,"Recovery code — shown once",new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MinHeight=60,FontFamily=new("Consolas")});
        var status=Ui.Text("No code has been generated yet.",12,"#64748B");status.Margin=new(0,10,0,10);panel.Children.Add(status);
        var generate=Ui.Button("Generate recovery code",()=>{},"PrimaryButton");panel.Children.Add(generate);
        generate.Click+=(_,_)=>{try{code.Text=store.GenerateRecoveryCode(session);generate.IsEnabled=false;status.Text="Save this code now. The application cannot display it again.";}catch(Exception ex){status.Text=ex.Message;}};
        var done=Ui.Button("I have saved my code",()=>{if(code.Text.Length>0)dialog.Close();});done.Margin=new(0,10,0,0);panel.Children.Add(done);
        var skip=Ui.Button("Skip for now",()=>dialog.Close(),"QuietButton");skip.Margin=new(0,4,0,0);panel.Children.Add(skip);
        dialog.ShowDialog();code.Clear();
    }
    internal static void Forgot(Window owner,WorkbookStore store,string initialName)
    {
        var (dialog,panel)=Shell(owner,"Reset your password",610);
        panel.Children.Add(Ui.Text("Use the single-use recovery code you saved earlier. No internet or OTP is required.",13,"#64748B"));
        var user=Field(panel,"Username (case-sensitive)",new TextBox{Text=initialName});
        var code=Field(panel,"Recovery code",new RevealPassword("Recovery code"));
        var password=Field(panel,"New password (6–128 characters)",new RevealPassword("New password"));
        var confirmation=Field(panel,"Confirm new password",new RevealPassword("Confirm new password"));
        var message=Ui.Text("",12,"#B42318");message.Margin=new(0,12,0,10);panel.Children.Add(message);
        var reset=Ui.Button("Reset password",()=>{},"PrimaryButton");panel.Children.Add(reset);
        reset.Click+=async(_,_)=>
        {
            if(password.Secret!=confirmation.Secret){message.Text="Passwords do not match.";return;}
            var name=user.Text;var token=code.Secret;var next=password.Secret;reset.IsEnabled=false;
            try{await Task.Run(()=>store.RecoverPassword(name,token,next));code.Clear();password.Clear();confirmation.Clear();MessageBox.Show(dialog,"Password reset. Sign in with your new password and create a fresh recovery code.","Password reset");dialog.Close();}
            catch(Exception ex){message.Text=ex.Message;}
            finally{reset.IsEnabled=true;}
        };
        var setup=Ui.Button("I know my password — set up recovery",()=>KnownPassword(dialog,store,user.Text),"QuietButton");setup.Margin=new(0,12,0,0);panel.Children.Add(setup);
        panel.Children.Add(Ui.Text("Without a password or saved code, use a verified backup with known credentials. There is no master-password bypass.",11,"#64748B"));dialog.ShowDialog();code.Clear();password.Clear();confirmation.Clear();
    }
    private static void KnownPassword(Window owner,WorkbookStore store,string initialName)
    {
        var (dialog,panel)=Shell(owner,"Verify your account",390);
        var user=Field(panel,"Username (case-sensitive)",new TextBox{Text=initialName});var secret=Field(panel,"Current password",new RevealPassword());
        var message=Ui.Text("",12,"#B42318");message.Margin=new(0,12,0,10);panel.Children.Add(message);
        var button=Ui.Button("Verify and create recovery code",()=>{},"PrimaryButton");panel.Children.Add(button);
        button.Click+=async(_,_)=>{button.IsEnabled=false;var name=user.Text;var password=secret.Secret;LoginSession? session=null;
            try{session=await Task.Run(()=>store.Login(name,password));secret.Clear();Setup(dialog,store,session);dialog.Close();}
            catch(Exception ex){message.Text=ex.Message;}
            finally{if(session!=null)store.Logout(session);button.IsEnabled=true;}};
        dialog.ShowDialog();secret.Clear();
    }
}
