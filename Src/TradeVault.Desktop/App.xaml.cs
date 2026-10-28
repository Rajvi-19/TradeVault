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
            if(e.Args.Length==2&&e.Args[0]=="--audit-pages"){PageAuditChecks.Run(e.Args[1]);Shutdown(0);return;}
#if UI_ACCEPTANCE
            if(e.Args.Length==0){AcceptanceChecks.LaunchWorkspace();return;}
#endif
            if(e.Args.Length>=2&&e.Args[0]=="--qa-report"){Shutdown(AcceptanceChecks.Run(e.Args[1])?0:1);return;}
            if(e.Args.Length>=2 && e.Args[0]=="--render-ui"){LiveChecks.Render(e.Args[1]);Shutdown(0);return;}
            if(e.Args.Contains("--self-test")){LiveChecks.Run();Console.WriteLine("PASS: live persistence/authentication/worksheet checks");Shutdown(0);return;}
            var paths=PortablePaths.FromApplicationDirectory();
            if(e.Args.Contains("--seed-excel-demo")&&System.IO.File.Exists(paths.Database))throw new InvalidOperationException("Demo creation requires a new application folder without a database. Existing data is never overwritten.");
            using var database=new PortableDatabase(paths);
            var store=new WorkbookStore(database);
            if(e.Args.Length==2&&e.Args[0]=="--seed-excel-demo"){ExcelDemoSeeder.Run(store,e.Args[1]);Shutdown(0);return;}
#if DEBUG
            if(e.Args.Contains("--seed-workflow-data")){TestDataSeeder.RunWorkflow(store,database);Shutdown(0);return;}
            if(e.Args.Contains("--seed-test-data")){TestDataSeeder.Run(store,database);Shutdown(0);return;}
#else
            if(e.Args.Contains("--seed-test-data"))throw new InvalidOperationException("Sample seeding is disabled in release builds.");
#endif
            if(e.Args.Contains("--initialize-db")){Console.WriteLine($"Initialized empty company worksheets: {database.Paths.Database}");Shutdown(0);return;}
            while(true)
            {
                var login=new LoginWindow(store);if(login.ShowDialog()!=true)break;
                var session=login.Session!;new MainWindow(store,session,database.Paths.Database).ShowDialog();store.Logout(session);
            }
            Shutdown(0);
        }
        catch(Exception ex){Console.Error.WriteLine(ex);if(!e.Args.Any(a=>a is "--audit-pages" or "--self-test" or "--qa-report" or "--render-ui" or "--seed-test-data" or "--seed-workflow-data"))MessageBox.Show(ex.Message,"TradeVault startup failed");Shutdown(1);}
    }
}
