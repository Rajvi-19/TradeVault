using System.Windows;
using System.Windows.Controls;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;
internal static class AuthUiChecks
{
    internal static void Run(WorkbookStore store)
    {
        var reveal=new RevealPassword();var grid=(Grid)reveal.Content;
        ((PasswordBox)grid.Children[0]).Password="Synthetic-Secret";reveal.Toggle();
        if(reveal.Secret!="Synthetic-Secret"||grid.Children[1].Visibility!=Visibility.Visible)throw new InvalidOperationException("Show password did not preserve value.");
        ((TextBox)grid.Children[1]).Text="Edited-Synthetic-Secret";reveal.Toggle();
        if(reveal.Secret!="Edited-Synthetic-Secret"||grid.Children[0].Visibility!=Visibility.Visible)throw new InvalidOperationException("Hide password lost visible edits.");
        reveal.Clear();if(reveal.Secret.Length!=0)throw new InvalidOperationException("Password not cleared.");
        var login=new LoginWindow(store);if(login.Icon==null)throw new InvalidOperationException("Window icon missing.");login.Close();
        var session=store.Login("owner","Owner@123");var owner=new MainWindow(store,session,store.GetType().Name);
        var inbox=new ImportedReportsWindow(owner,new ReportWorkflow(store,session));
        var surface=(FrameworkElement)inbox.Content;surface.Measure(new(900,600));surface.Arrange(new(0,0,900,600));surface.UpdateLayout();inbox.Close();owner.Close();store.Logout(session);
    }
}
