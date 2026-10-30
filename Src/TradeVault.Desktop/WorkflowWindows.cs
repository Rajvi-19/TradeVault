using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TradeVault.Application;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal static class WorkflowWindows
{
    private static void Safe(Window owner,Action action){try{action();}catch(Exception ex){MessageBox.Show(owner,ex.Message,"TradeVault",MessageBoxButton.OK,MessageBoxImage.Information);}}
    private static (Window Window,StackPanel Form) Form(Window owner,string title,int width=660,int height=560)
    {
        var window=new Window{Owner=owner,Width=Math.Min(width,SystemParameters.WorkArea.Width),Height=Math.Min(height,SystemParameters.WorkArea.Height),MinWidth=Math.Min(480,SystemParameters.WorkArea.Width),MinHeight=300,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var form=new StackPanel{Margin=new(18)};form.Children.Add(Ui.Text(title,20,"#18243B",true));
        Ui.WindowFrame(window,new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},title);return(window,form);
    }
    private static T Field<T>(StackPanel form,string label,T control)where T:FrameworkElement
    {var text=Ui.Text(label,12,"#52627A");text.Margin=new(0,12,0,5);form.Children.Add(text);if(control is DateCellEditor date){date.Background=Ui.Color("#FFFFFF");date.BorderBrush=Ui.Color("#DCE3ED");date.BorderThickness=new Thickness(1);date.MinHeight=32;}form.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,label);return control;}
    internal static ExcelGrid ReportGrid(ReportTable table)
    {
        var fields=table.Columns.Select(c=>new Field(c.Key,ExcelFields.For(table.Name=="Account Details"?"details":"ledger").FirstOrDefault(f=>f.Key==c.Key)?.Label??c.Label,true,c.Type=="text"?240:170)).ToArray();
        var rows=new ObservableCollection<GridRow>(table.Rows.Select(data=>{var row=new GridRow();foreach(var cell in data)row[cell.Key]=cell.Value;return row;}));
        return new(fields,rows,true);
    }
    internal static Window ReportView(Window owner,ReportBundle report,string title="Report preview")
    {
        var window=new Window{Owner=owner,Width=1200,Height=760,MinWidth=760,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var root=new DockPanel();var meta=new StackPanel{Margin=new(12)};
        meta.Children.Add(Ui.Text(report.PartyName+" · "+report.AsOf.ToString("dd-MM-yyyy"),20,"#18243B",true));
        meta.Children.Add(Ui.Text(report.Mode+" · revision "+report.Revision+" · source "+report.SourceId,11,"#64748B"));
        meta.Children.Add(Ui.Button("Save PDF",()=>Safe(window,()=>SavePdf(window,report))));
        meta.Children.Add(Ui.Button("Print",()=>Safe(window,()=>
        {
            try{Print(report);}
            catch(Exception){MessageBox.Show(window,"Windows printing is unavailable. Save the same formatted report directly as PDF instead.","Save report as PDF",MessageBoxButton.OK,MessageBoxImage.Information);SavePdf(window,report);}
        })));
        foreach(var note in report.Notes)meta.Children.Add(Ui.Text(note,11,"#64748B"));
        DockPanel.SetDock(meta,Dock.Top);root.Children.Add(meta);
        var tabs=new TabControl();foreach(var table in report.Tables){var panel=new DockPanel();var totals=Ui.Text(string.Join("   |   ",table.Totals.Select(t=>(table.Columns.FirstOrDefault(c=>c.Key==t.Key)?.Label??t.Key)+": USD "+t.Value.ToString("N2",CultureInfo.InvariantCulture))),12,"#2458D3",true);totals.Margin=new(10);DockPanel.SetDock(totals,Dock.Bottom);panel.Children.Add(totals);panel.Children.Add(ReportGrid(table));tabs.Items.Add(new TabItem{Header=table.Name,Content=panel});}root.Children.Add(tabs);
        Ui.WindowFrame(window,root,title);return window;
    }
    private static void Print(ReportBundle report)
    {
        var printer=new PrintDialog();
        printer.PrintTicket.PageOrientation=System.Printing.PageOrientation.Landscape;
        if(printer.ShowDialog()!=true)return;
        var width=printer.PrintableAreaWidth;var height=printer.PrintableAreaHeight;
        if(width<height)(width,height)=(height,width);
        var paginator=new ReportPaginator(report,new Size(width,height));
        var document=new System.Windows.Documents.FixedDocument();document.DocumentPaginator.PageSize=new Size(width,height);
        for(int i=0;i<paginator.PageCount;i++)
        {
            var page=new System.Windows.Documents.FixedPage{Width=width,Height=height};
            page.Children.Add(new System.Windows.Shapes.Rectangle{Width=width,Height=height,Fill=new System.Windows.Media.VisualBrush(paginator.GetPage(i).Visual){Stretch=System.Windows.Media.Stretch.None,AlignmentX=System.Windows.Media.AlignmentX.Left,AlignmentY=System.Windows.Media.AlignmentY.Top}});
            var content=new System.Windows.Documents.PageContent{Child=page};document.Pages.Add(content);
        }
        try{printer.PrintDocument(document.DocumentPaginator,"TradeVault reports");}
        catch(Exception ex){throw new InvalidOperationException("Windows could not print this report. Use Save PDF to create the report directly without a printer driver. Printer detail: "+ex.Message,ex);}
    }
    private static void SavePdf(Window owner,ReportBundle report)
    {
        var safe=new string(report.PartyName.Select(c=>System.IO.Path.GetInvalidFileNameChars().Contains(c)?'_':c).ToArray());
        var picker=new SaveFileDialog{Filter="PDF report (*.pdf)|*.pdf",FileName=$"{safe} - Account Reports - {report.AsOf:yyyy-MM-dd}.pdf",InitialDirectory=AppContext.BaseDirectory};
        if(picker.ShowDialog(owner)!=true)return;ReportPdf.Save(report,picker.FileName);
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(picker.FileName){UseShellExecute=true});}catch(Exception){MessageBox.Show(owner,"PDF saved:\n"+picker.FileName);}
    }
    internal static void Receipts(Window owner,ReportWorkflow service,Guid party) { new PaymentsWindow(owner,service,party).ShowDialog(); }
    internal static void Backups(Window owner,WorkbookStore store)
    {
        var (window,form)=Form(owner,"Backup and recovery",760,500);
        var list=Field(form,"Backups stored beside the application",new ListBox{ItemsSource=store.Backups(),Height=200});
        form.Children.Add(Ui.Button("Create verified backup",()=>Safe(window,()=>{store.CreateBackup();list.ItemsSource=store.Backups();}),"PrimaryButton"));
        var restore=Ui.Button("Restore selected backup",()=>Safe(window,()=>{
            if(list.SelectedItem is not string path)throw new ArgumentException("Choose a backup.");
            if(MessageBox.Show(window,"Replace ALL working data with this backup? A safety backup is made first. The workspace will close. Reopen TradeVault after restoring.","Restore backup",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            store.RestoreBackup(path);window.Close();owner.Close();
        }));form.Children.Add(restore);
        form.Children.Add(Ui.Text("For lost-drive recovery, keep a separate copy of a verified backup on another USB. Keep backup drives in a safe place.",12,"#64748B"));window.ShowDialog();
    }
    internal static void Reminders(Window owner,ReportWorkflow service,Guid party,DateOnly asOf)
    {
        var (window,form)=Form(owner,"Unpaid invoices and payment reminders",900,550);
        var name=service.Build(party,asOf,true).PartyName;
        var items=service.Reminders(party,asOf).Select(r=>new {Party=name,r.Invoice,r.Due,r.Outstanding,r.OverdueDays,r.Status}).ToList();var table=new DataGrid{ItemsSource=items,AutoGenerateColumns=false,IsReadOnly=true,CanUserDeleteRows=false,CanUserAddRows=false,Height=350};
        foreach(var (label,path,width,format) in new[]{("Party","Party",220,""),("Invoice","Invoice",190,""),("Due date","Due",110,"{0:dd-MM-yyyy}"),("Outstanding / USD","Outstanding",145,"{0:N2}"),("Overdue days","OverdueDays",110,""),("Status","Status",150,"")})
            table.Columns.Add(new DataGridTextColumn{Header=label,Width=width,Binding=new System.Windows.Data.Binding(path){StringFormat=format,ConverterCulture=CultureInfo.InvariantCulture}});
        form.Children.Add(Ui.Text($"Reporting cutoff: {asOf:dd-MM-yyyy} · All amounts USD",12,"#52627A"));form.Children.Add(table);
        form.Children.Add(Ui.Text("Includes settled, BL-pending, due-soon and overdue invoices. Unallocated advances do not settle a specific invoice.",12,"#64748B"));window.ShowDialog();
    }
}
