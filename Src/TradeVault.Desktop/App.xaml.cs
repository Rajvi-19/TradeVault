using System.Windows;

namespace TradeVault.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length>=2 && e.Args[0]=="--render-ui")
        {
            new MainWindow().ExportDesignPreview(e.Args[1],e.Args.Length>2?e.Args[2]:"invoices"); Shutdown(0); return;
        }
        if (e.Args.Contains("--self-test"))
        {
            try { PrototypeChecks.Run(); Console.WriteLine("PASS: desktop interface checks"); Shutdown(0); }
            catch (Exception ex) { Console.Error.WriteLine(ex); Shutdown(1); }
            return;
        }
        new MainWindow().Show();
    }
}
