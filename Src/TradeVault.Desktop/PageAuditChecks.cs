using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal static class PageAuditChecks
{
    internal static void Run(string output)
    {
        output=Path.GetFullPath(output);Directory.CreateDirectory(output);
        var root=Path.Combine(output,"SyntheticWorkspace",Guid.NewGuid().ToString("N"));
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);
        var party=store.CreateParty("SYNTHETIC Client Audit");var wf=new ReportWorkflow(store);
        var sheet=new WorksheetController(store);sheet.Load("form",party.Id);
        for(int i=1;i<=35;i++){var row=sheet.Add();row["F9"]="AUDIT-2026-"+i.ToString("D3");row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F17"]="2026-06-17";}
        sheet.Save();wf.RecordReceipt(party.Id,new(2026,8,1),5000,"SYNTHETIC receipt",[new(sheet.Rows[0]["_id"],"Bank",5000)]);
        var owner=new MainWindow(store,db.Paths.Database);owner.Show();
        void Capture(Window window,string name)
        {
            var size=Ui.WorkspaceSize(new Rect(0,0,1366,728));
            LiveChecks.Capture(window,Path.Combine(output,name+".png"),(int)Math.Min(window.Width,size.Width),(int)Math.Min(window.Height,size.Height));
        }
        void Modal(string name,Action action)
        {
            Exception? failure=null;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};
            timer.Tick+=(_,_)=>
            {
                var dialog=System.Windows.Application.Current.Windows.OfType<Window>().LastOrDefault(w=>w.IsVisible&&w!=owner);if(dialog==null)return;
                timer.Stop();try{Capture(dialog,name);foreach(var scroll in Descendants(dialog).OfType<ScrollViewer>())scroll.ScrollToBottom();dialog.UpdateLayout();Capture(dialog,name+"-bottom");}catch(Exception ex){failure=ex;}finally{dialog.Close();}
            };
            timer.Start();try{action();}finally{timer.Stop();}if(failure!=null)throw failure;
        }
        try
        {
            foreach(var name in new[]{"form","details","ledger"}){owner.PreviewParty(name,party.Id);Capture(owner,"worksheet-"+name);}
            Modal("create-party",()=>new PartyDialog(store){Owner=owner}.ShowDialog());
            Modal("assign-party",()=>new PartyDialog(store,true){Owner=owner}.ShowDialog());
            Modal("reminders",()=>WorkflowWindows.Reminders(owner,wf,party.Id,new(2026,10,28)));
            Modal("report-preview",()=>WorkflowWindows.ReportView(owner,wf.Build(party.Id,new(2026,10,28),true)).ShowDialog());
            ReportPdf.Save(wf.Build(party.Id,new(2026,10,28),true),Path.Combine(output,"full-width-reports.pdf"));
            File.WriteAllText(Path.Combine(output,"page-audit.txt"),"PASS: page construction, modal open/close and screenshot rendering; synthetic data only. OS file dialogs, physical printing and real USB ejection require manual acceptance.");
        }
        finally{owner.Close();}
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {if(parent is Window {Content:DependencyObject content})parent=content;for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}}
}
