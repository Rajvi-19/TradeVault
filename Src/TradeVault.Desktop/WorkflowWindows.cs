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
        var window=new Window{Owner=owner,Width=width,Height=height,MinWidth=480,MinHeight=300,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var form=new StackPanel{Margin=new(18)};form.Children.Add(Ui.Text(title,20,"#18243B",true));
        Ui.WindowFrame(window,new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},title);return(window,form);
    }
    private static T Field<T>(StackPanel form,string label,T control)where T:FrameworkElement
    {var text=Ui.Text(label,12,"#52627A");text.Margin=new(0,12,0,5);form.Children.Add(text);if(control is DateCellEditor date){date.Background=System.Windows.Media.Brushes.White;date.BorderBrush=Ui.Color("#DCE3ED");date.BorderThickness=new Thickness(1);date.MinHeight=32;}form.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,label);return control;}
    private static decimal Money(TextBox box)=>decimal.Parse(box.Text,NumberStyles.Number,CultureInfo.InvariantCulture);
    private static DateOnly Date(DateCellEditor box)=>DateOnly.ParseExact(DateCellConverter.Normalize(box.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture);
    internal static bool Configure(Window owner,ReportWorkflow service,Guid party)
    {
        var existing=service.Mapping(party);var (window,form)=Form(owner,"Report setup",700,650);
        form.Children.Add(Ui.Text("Choose the independent amount sources explicitly. Existing manual worksheets are preserved. Changing mappings after receipts are posted is blocked.",12,"#64748B"));
        ComboBox Choice(params KeyValuePair<string,string>[] values)=>new(){ItemsSource=values,DisplayMemberPath="Value",SelectedValuePath="Key",MinHeight=32};
        var contract=Field(form,"Bank contract amount source",Choice(new("F6","Our customs invoice amount (F6)"),new("F7","Pakistan customs invoice amount (F7)")));
        var received=Field(form,"Existing bank receipts source (opening cumulative amounts)",Choice(new("F22","Received MY → PK customs invoice amount (F22), dated F24"),new("F25","Received Nemichem account amount (F25), dated F26")));
        if(existing!=null){contract.SelectedValue=existing.ContractField;received.SelectedValue=existing.BankReceivedField;}
        var shipping=Field(form,"Shipping accounting",new CheckBox{Content="Explicitly deduct each register row's shipping amount once, on its invoice date",IsChecked=existing?.DeductRegisteredShipping??false});
        form.Children.Add(Ui.Text("Unchecked means shipping remains manual and is excluded from generated balances. This does not infer per-container or paid-date treatment.",11,"#64748B"));
        var openingDate=Field(form,"Opening balances effective date",new DateCellEditor{Value=(existing?.OpeningDate??DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd"),MinHeight=32});
        var bank=Field(form,"Opening bank balance / USD",new TextBox{Text=(existing?.OpeningBank??0).ToString("0.00",CultureInfo.InvariantCulture)});
        var diff=Field(form,"Opening difference balance / USD",new TextBox{Text=(existing?.OpeningDifference??0).ToString("0.00",CultureInfo.InvariantCulture)});
        var save=Ui.Button("Enable generated reports",()=>Safe(window,()=>{if(contract.SelectedValue is not string c||received.SelectedValue is not string r)throw new ArgumentException("Choose both amount sources.");service.Configure(party,new(c,r,shipping.IsChecked==true,Date(openingDate),Money(bank),Money(diff)));window.DialogResult=true;}),"PrimaryButton");save.Margin=new(0,16,0,0);form.Children.Add(save);
        return window.ShowDialog()==true;
    }
    internal static ExcelGrid ReportGrid(ReportTable table)
    {
        var fields=table.Columns.Select(c=>new Field(c.Key,c.Label,true,c.Type=="text"?240:170)).ToArray();
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
    internal static void Inbox(Window owner,ReportWorkflow service) { new ImportedReportsWindow(owner,service).ShowDialog(); }
    internal static void Receipts(Window owner,ReportWorkflow service,Guid party)
    {
        var (window,form)=Form(owner,"Receipts and allocations",900,740);
        if(service.Mapping(party)==null){MessageBox.Show(owner,"Complete Report setup for this party first.");return;}
        var history=new ListBox{Height=135,DisplayMemberPath="Reference"};void Reload()=>history.ItemsSource=service.Receipts(party).Select(r=>new ReceiptHistory(r)).ToArray();Reload();
        Field(form,"Receipt history / reversals",history);
        form.Children.Add(Ui.Button("Reverse selected receipt",()=>Safe(window,()=>{
            if(history.SelectedItem is not ReceiptHistory chosen)throw new ArgumentException("Select a receipt.");
            var (dialog,body)=Form(window,"Reverse receipt",540,360);var date=Field(body,"Effective correction date",new DateCellEditor{Value=DateTime.Today.ToString("yyyy-MM-dd"),MinHeight=32});var reason=Field(body,"Reason",new TextBox());
            body.Children.Add(Ui.Button("Post reversal",()=>Safe(dialog,()=>{service.ReverseReceipt(party,chosen.Receipt.Id,Date(date),reason.Text);dialog.DialogResult=true;}),"PrimaryButton"));dialog.ShowDialog();Reload();
        })));
        var receiptDate=Field(form,"New receipt date",new DateCellEditor{Value=DateTime.Today.ToString("yyyy-MM-dd"),MinHeight=32});
        var amount=Field(form,"Receipt total / USD",new TextBox{Text="0.00"});var reference=Field(form,"Reference / account",new TextBox());
        var allocations=new ObservableCollection<AllocationInput>();foreach(var invoice in service.Invoices(party)){var id=ReportWorkflow.InvoiceKey(party,invoice);allocations.Add(new(id,invoice.GetValueOrDefault("F9",""),"Bank"));allocations.Add(new(id,invoice.GetValueOrDefault("F9",""),"Difference"));}
        Guid? correcting=null;
        var table=new DataGrid{ItemsSource=allocations,AutoGenerateColumns=false,CanUserAddRows=false,Height=175};
        foreach(var (label,path,locked) in new[]{("Invoice","Invoice",true),("Component","Component",true),("Allocate / USD","Amount",false)})table.Columns.Add(new DataGridTextColumn{Header=label,IsReadOnly=locked,Width=label=="Invoice"?300:180,Binding=new System.Windows.Data.Binding(path){UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.LostFocus}});
        Field(form,"Allocate across invoices (leave zero for no allocation)",table);
        form.Children.Add(Ui.Button("Correct selected / allocate an advance",()=>Safe(window,()=>{
            if(history.SelectedItem is not ReceiptHistory chosen||chosen.Receipt.Reverses!=null)throw new ArgumentException("Select an original receipt.");correcting=chosen.Receipt.Id;
            amount.Text=chosen.Receipt.Amount.ToString("0.00",CultureInfo.InvariantCulture);reference.Text=chosen.Receipt.Reference;receiptDate.Value=DateTime.Today.ToString("yyyy-MM-dd");
            foreach(var item in allocations)item.Amount=(chosen.Receipt.Allocations.SingleOrDefault(a=>a.InvoiceId==item.Id&&a.Component==item.Component)?.Amount??0).ToString("0.00",CultureInfo.InvariantCulture);table.Items.Refresh();
        })));
        var totals=Ui.Text("Allocate amounts above, then choose Calculate summary.",12,"#2458D3");form.Children.Add(totals);
        form.Children.Add(Ui.Button("Calculate summary",()=>Safe(window,()=>{table.CommitEdit();table.CommitEdit(DataGridEditingUnit.Row,true);var sum=allocations.Sum(a=>decimal.Parse(a.Amount,CultureInfo.InvariantCulture));totals.Text=$"Receipt: USD {Money(amount):N2}   Allocated: USD {sum:N2}   Unallocated: USD {Money(amount)-sum:N2}";})));
        form.Children.Add(Ui.Button("Post receipt",()=>Safe(window,()=>{
            table.CommitEdit();table.CommitEdit(DataGridEditingUnit.Row,true);var splits=allocations.Where(a=>decimal.Parse(a.Amount,CultureInfo.InvariantCulture)!=0).Select(a=>new ReceiptSplit(a.Id,a.Component,decimal.Parse(a.Amount,CultureInfo.InvariantCulture))).ToList();
            if(correcting is {} id)service.CorrectReceipt(party,id,Date(receiptDate),Money(amount),reference.Text,splits);else service.RecordReceipt(party,Date(receiptDate),Money(amount),reference.Text,splits);correcting=null;amount.Text="0.00";reference.Clear();foreach(var a in allocations)a.Amount="0.00";table.Items.Refresh();Reload();totals.Text="Receipt posted. Outstanding amounts refresh when you open generated reports.";
        }),"PrimaryButton"));window.ShowDialog();
    }
    private sealed record ReceiptHistory(PartyReceipt Receipt){public string Reference=>$"{Receipt.Date:dd-MM-yyyy} · {Receipt.Reference} · USD {Receipt.Amount:N2}"+(Receipt.Reverses!=null?" · REVERSAL":"");}
    private sealed class AllocationInput(string id,string invoice,string component)
    {public string Id{get;}=id;public string Invoice{get;}=invoice;public string Component{get;}=component;public string Amount{get;set;}="0.00";}
    internal static void Backups(Window owner,WorkbookStore store,LoginSession session)
    {
        var (window,form)=Form(owner,"Backup and recovery",760,500);
        var list=Field(form,"Backups stored beside the application",new ListBox{ItemsSource=store.Backups(session),Height=200});
        form.Children.Add(Ui.Button("Create verified backup",()=>Safe(window,()=>{store.CreateBackup(session);list.ItemsSource=store.Backups(session);}),"PrimaryButton"));
        var restore=Ui.Button("Restore selected backup",()=>Safe(window,()=>{
            if(list.SelectedItem is not string path)throw new ArgumentException("Choose a backup.");
            if(MessageBox.Show(window,"Replace ALL working data and imported submissions with this backup? A safety backup is made first. You will be signed out.","Restore backup",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
            store.RestoreBackup(session,path);window.Close();owner.Close();
        }));restore.IsEnabled=session.Role=="Owner";form.Children.Add(restore);
        form.Children.Add(Ui.Text("For lost-drive recovery, keep a separate copy of a verified backup on another USB. The SQLite database is not encrypted by the login password.",12,"#64748B"));window.ShowDialog();
    }
    internal static void Reminders(Window owner,ReportWorkflow service,Guid party,DateOnly asOf)
    {
        var (window,form)=Form(owner,"Receivables and reminders",900,550);
        var name=service.Build(party,asOf,true).PartyName;
        var items=service.Reminders(party,asOf).Select(r=>new {Party=name,r.Invoice,r.Due,r.Outstanding,r.OverdueDays,r.Status}).ToList();var table=new DataGrid{ItemsSource=items,AutoGenerateColumns=false,IsReadOnly=true,CanUserDeleteRows=false,CanUserAddRows=false,Height=350};
        foreach(var (label,path,width,format) in new[]{("Party","Party",220,""),("Invoice","Invoice",190,""),("Due date","Due",110,"{0:dd-MM-yyyy}"),("Outstanding / USD","Outstanding",145,"{0:N2}"),("Overdue days","OverdueDays",110,""),("Status","Status",150,"")})
            table.Columns.Add(new DataGridTextColumn{Header=label,Width=width,Binding=new System.Windows.Data.Binding(path){StringFormat=format,ConverterCulture=CultureInfo.InvariantCulture}});
        form.Children.Add(Ui.Text($"Reporting cutoff: {asOf:dd-MM-yyyy} · All amounts USD",12,"#52627A"));form.Children.Add(table);
        form.Children.Add(Ui.Text("Includes settled, BL-pending, due-soon and overdue invoices. Unallocated advances do not settle a specific invoice.",12,"#64748B"));window.ShowDialog();
    }
}
