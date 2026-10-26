using System.Windows;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        try
        {
            if(e.Args.Contains("--self-test")){LiveChecks.Run();Console.WriteLine("PASS: live persistence/authentication/worksheet checks");Shutdown(0);return;}
            using var database=new PortableDatabase(PortablePaths.FromApplicationDirectory());
            var store=new WorkbookStore(database);
            if(e.Args.Contains("--initialize-db")){Console.WriteLine($"Initialized empty company worksheets: {database.Paths.Database}");Shutdown(0);return;}
            while(true)
            {
                var login=new LoginWindow(store);if(login.ShowDialog()!=true)break;
                var session=login.Session!;new MainWindow(store,session,database.Paths.Database).ShowDialog();store.Logout(session);
            }
            Shutdown(0);
        }
        catch(Exception ex){Console.Error.WriteLine(ex);if(!e.Args.Contains("--self-test"))MessageBox.Show(ex.Message,"TradeVault startup failed");Shutdown(1);}
    }
}
