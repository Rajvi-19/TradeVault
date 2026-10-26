using System.Windows;

namespace TradeVault.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test"))
        {
            try { PrototypeChecks.Run(); Console.WriteLine("PASS: desktop prototype checks"); Shutdown(0); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Shutdown(1); }
            return;
        }
        new MainWindow().Show();
    }
}
