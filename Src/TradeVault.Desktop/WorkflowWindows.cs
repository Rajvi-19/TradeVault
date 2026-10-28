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
    {var text=Ui.Text(label,12,"#52627A");text.Margin=new(0,12,0,5);form.Children.Add(text);form.Children.Add(control);System.Windows.Automation.AutomationProperties.SetName(control,label);return control;}
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
        meta.Children.Add(Ui.Button("Print both reports",()=>Safe(window,()=>Print(report))));
        foreach(var note in report.Notes)meta.Children.Add(Ui.Text(note,11,"#64748B"));
        DockPanel.SetDock(meta,Dock.Top);root.Children.Add(meta);
        var tabs=new TabControl();foreach(var table in report.Tables){var panel=new DockPanel();var totals=Ui.Text(string.Join("   |   ",table.Totals.Select(t=>(table.Columns.FirstOrDefault(c=>c.Key==t.Key)?.Label??t.Key)+": USD "+t.Value.ToString("N2",CultureInfo.InvariantCulture))),12,"#2458D3",true);totals.Margin=new(10);DockPanel.SetDock(totals,Dock.Bottom);panel.Children.Add(totals);panel.Children.Add(ReportGrid(table));tabs.Items.Add(new TabItem{Header=table.Name,Content=panel});}root.Children.Add(tabs);
        Ui.WindowFrame(window,root,title);return window;
    }
    private static void Print(ReportBundle report)
    {
        var printer=new PrintDialog();if(printer.ShowDialog()!=true)return;
        var document=new System.Windows.Documents.FlowDocument{FontFamily=new System.Windows.Media.FontFamily("Segoe UI"),FontSize=8,PagePadding=new Thickness(20),ColumnWidth=double.PositiveInfinity};
        foreach(var data in report.Tables)
        {
            document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Bold(new System.Windows.Documents.Run(report.PartyName+" · "+data.Name))){FontSize=16,BreakPageBefore=document.Blocks.Count>0});
            document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"USD · As of {report.AsOf:dd-MM-yyyy} · revision {report.Revision} · {report.Mode}")));
            var table=new System.Windows.Documents.Table{CellSpacing=0};for(int i=0;i<data.Columns.Count;i++)table.Columns.Add(new System.Windows.Documents.TableColumn());
            var group=new System.Windows.Documents.TableRowGroup();table.RowGroups.Add(group);
            void Row(IEnumerable<string> values,bool bold=false){var row=new System.Windows.Documents.TableRow();foreach(var value in values){var paragraph=new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(value)){Margin=new Thickness(0)};row.Cells.Add(new System.Windows.Documents.TableCell(paragraph){Padding=new Thickness(3),BorderBrush=Ui.Color("#CCCCCC"),BorderThickness=new Thickness(0.5),FontWeight=bold?FontWeights.Bold:FontWeights.Normal});}group.Rows.Add(row);}
            Row(data.Columns.Select(c=>c.Label),true);foreach(var row in data.Rows)Row(data.Columns.Select(c=>row.GetValueOrDefault(c.Key,"")));Row(data.Columns.Select((c,i)=>i==0?"TOTAL / CLOSING":data.Totals.TryGetValue(c.Key,out var total)?total.ToString("N2",CultureInfo.InvariantCulture):""),true);
            document.Blocks.Add(table);foreach(var note in report.Notes)document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(note)));
        }
        var paginator=((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator;paginator.PageSize=new Size(printer.PrintableAreaWidth,printer.PrintableAreaHeight);printer.PrintDocument(paginator,"TradeVault reports");
    }
    internal static void Inbox(Window owner,ReportWorkflow service)
    {
        var window=new Window{Owner=owner,Width=1050,Height=680,MinWidth=760,MinHeight=480,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var root=new DockPanel();var actions=new WrapPanel{Margin=new(12)};var list=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Single,SelectionUnit=DataGridSelectionUnit.FullRow,CanUserAddRows=false};
        foreach(var (label,path) in new[]{("Party","Report.PartyName"),("As of","Report.AsOf"),("Revision","Report.Revision"),("Mode","Report.Mode"),("Status","Status"),("Received","ImportedAt")})list.Columns.Add(new DataGridTextColumn{Header=label,Binding=new System.Windows.Data.Binding(path),Width=label=="Party"?230:150});
        void Refresh()=>list.ItemsSource=service.Imports();
        actions.Children.Add(Ui.Button("Import report",()=>Safe(window,()=>{
            var picker=new OpenFileDialog{Filter="TradeVault submission (*.tvreport)|*.tvreport",InitialDirectory=service.Root};if(picker.ShowDialog(window)!=true)return;
            var preview=service.PreviewPackage(picker.FileName);var reportWindow=ReportView(window,preview.Report,"Import preview — checksums only; source identity is not authenticated");reportWindow.ShowDialog();
            if(MessageBox.Show(window,"Import this read-only revision? Checksums verify content integrity, not who sent it. Confirm the sender through your normal handover process.","Import report",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            service.Import(picker.FileName,preview.Hash);Refresh();
        }),"PrimaryButton"));
        actions.Children.Add(Ui.Button("Open selected",()=>Safe(window,()=>{if(list.SelectedItem is ImportedSubmission item)ReportView(window,item.Report,"Submitted report · read-only").ShowDialog();})));
        actions.Children.Add(Ui.Button("Review status / note",()=>Safe(window,()=>{
            if(list.SelectedItem is not ImportedSubmission item)throw new ArgumentException("Select a submission.");
            var (dialog,form)=Form(window,"Review submission",600,420);var state=Field(form,"Review status",new ComboBox{ItemsSource=new[]{"New","Reviewed","Needs correction"},SelectedItem=item.Status});
            var note=Field(form,"Owner review note (separate from submitted report)",new TextBox{Text=item.ReviewNote,AcceptsReturn=true,Height=140,TextWrapping=TextWrapping.Wrap});
            form.Children.Add(Ui.Button("Save review",()=>Safe(dialog,()=>{service.Review(item,(string)state.SelectedItem,note.Text);dialog.DialogResult=true;}),"PrimaryButton"));dialog.ShowDialog();Refresh();
        })));
        actions.Children.Add(Ui.Text("  All revisions are preserved. Imported records never replace working invoices.",11,"#64748B"));DockPanel.SetDock(actions,Dock.Top);root.Children.Add(actions);root.Children.Add(list);Refresh();Ui.WindowFrame(window,root,"Operator reports");window.ShowDialog();
    }
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
    internal static void Password(Window owner,WorkbookStore store,LoginSession session)
    {
        var (window,form)=Form(owner,"Password management",540,470);
        var current=Field(form,"Your current password",new PasswordBox());var next=Field(form,"New password (10–128 characters)",new PasswordBox());var confirm=Field(form,"Confirm new password",new PasswordBox());
        var reset=Field(form,"Owner recovery action",new CheckBox{Content="Reset Operator password instead",IsEnabled=session.Role=="Owner"});
        form.Children.Add(Ui.Button("Change password",()=>Safe(window,()=>{if(next.Password!=confirm.Password)throw new ArgumentException("Passwords do not match.");store.ChangePassword(session,current.Password,next.Password,reset.IsChecked==true);window.DialogResult=true;}),"PrimaryButton"));
        form.Children.Add(Ui.Text("Changing your own password signs out existing sessions. Owner recovery requires a valid backup; there is no hidden master-password bypass.",11,"#64748B"));
        if(window.ShowDialog()==true&&reset.IsChecked!=true)owner.Close();
    }
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
        var items=service.Reminders(party,asOf);var table=new DataGrid{ItemsSource=items,AutoGenerateColumns=true,IsReadOnly=true,CanUserDeleteRows=false,CanUserAddRows=false,Height=350};form.Children.Add(table);
        form.Children.Add(Ui.Text("Includes settled, BL-pending, due-soon and overdue invoices. Unallocated advances do not settle a specific invoice.",12,"#64748B"));window.ShowDialog();
    }
}
