using TradeVault.Infrastructure;
using TradeVault.Domain;
namespace TradeVault.Desktop;
internal static class LiveChecks
{
    internal static void Render(string directory)
    {
        directory=System.IO.Path.GetFullPath(directory);System.IO.Directory.CreateDirectory(directory);
        var root=System.IO.Path.Combine(AppContext.BaseDirectory,"TestData",Guid.NewGuid().ToString("N"));
        try
        {
            using var database=new PortableDatabase(new(root));var store=new WorkbookStore(database);
            AcceptanceChecks.Seed(store);
            var masterAgent=store.CreateAgent("QA Regional Trading");var masterParty=store.CreateParty("QA Master Buyer");store.CreateRegisterInvoice(masterParty.Id,0,new("India",masterAgent.Name,"QA-MASTER-PI",new(2026,5,1),masterAgent.Id));
            var window=new MainWindow(store,database.Paths.Database);
            foreach(var name in new[]{"parties","agents","form","partyReceipt","agentReceipt","reports","details","ledger"}){window.PreviewSheet(name);Capture(window,System.IO.Path.Combine(directory,name+".png"),1480,920);if(name=="form")Capture(window,System.IO.Path.Combine(directory,"form-minimum.png"),1100,720);}
            Capture(window,System.IO.Path.Combine(directory,"compact-window.png"),1000,600);
            var partyDialog=new PartyDialog(store);var partySurface=(System.Windows.FrameworkElement)partyDialog.Content;partySurface.Measure(new(440,double.PositiveInfinity));Capture(partyDialog,System.IO.Path.Combine(directory,"new-party.png"),440,(int)Math.Ceiling(partySurface.DesiredSize.Height));partyDialog.Close();
            var invoiceDialog=new NewInvoiceDialog(store,Guid.Empty,store.Load("form").Revision);invoiceDialog.FillForCheck(new("India",masterAgent.Name,"QA-NEW-PI",DateOnly.FromDateTime(DateTime.Today)));var invoiceSurface=(System.Windows.FrameworkElement)invoiceDialog.Content;invoiceSurface.Measure(new(480,double.PositiveInfinity));Capture(invoiceDialog,System.IO.Path.Combine(directory,"new-invoice.png"),480,(int)Math.Ceiling(invoiceSurface.DesiredSize.Height));invoiceDialog.Close();
            var deleteDialog=new ConfirmDeleteWindow(window,"TEST-INV-006","[TEST] Sample party");var deleteSurface=(System.Windows.FrameworkElement)deleteDialog.Content;deleteSurface.Measure(new(560,double.PositiveInfinity));Capture(deleteDialog,System.IO.Path.Combine(directory,"delete-confirmation.png"),560,(int)Math.Ceiling(deleteSurface.DesiredSize.Height));deleteDialog.Close();
            AppTheme.SetDark(true);
            foreach(var name in new[]{"parties","agents","form","partyReceipt","agentReceipt","reports","details","ledger"}){window.PreviewSheet(name);Capture(window,System.IO.Path.Combine(directory,name+"-dark.png"),1100,720);}
            var darkParty=new PartyDialog(store);var darkSurface=(System.Windows.FrameworkElement)darkParty.Content;darkSurface.Measure(new(440,double.PositiveInfinity));Capture(darkParty,System.IO.Path.Combine(directory,"new-party-dark.png"),440,(int)Math.Ceiling(darkSurface.DesiredSize.Height));darkParty.Close();
            AppTheme.SetDark(false);
            window.Close();
            var party=store.CreateParty("QA generated reports");var controller=new WorksheetController(store);controller.Load("form",party.Id);var row=controller.Add();row["F9"]="QA-GENERATED-001";row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F17"]="2026-06-17";controller.Save();
            var generated=new MainWindow(store,database.Paths.Database);generated.PreviewParty("details",party.Id);Capture(generated,System.IO.Path.Combine(directory,"generated-details.png"),1440,880);generated.PreviewParty("ledger",party.Id);Capture(generated,System.IO.Path.Combine(directory,"generated-ledger.png"),1440,880);generated.Close();
            controller.Load("form",party.Id);
            foreach(var suffix in new[]{"002","003"}){var extra=controller.Add();extra["F9"]="QA-GENERATED-"+suffix;extra["F10"]="2026-06-01";extra["F5"]="18000";extra["F6"]="12000";extra["F7"]="11000";extra["F17"]="2026-06-17";}controller.Save();
            var paymentService=new ReportWorkflow(store);var firstId=ReportWorkflow.InvoiceKey(party.Id,paymentService.Invoices(party.Id)[0]);paymentService.RecordReceipt(party.Id,new(2026,8,1),3000,"Transfer received",[new(firstId,"Bank",2800),new(firstId,"Difference",200)]);
            var cancelled=paymentService.RecordReceipt(party.Id,new(2026,8,10),1000,"Deposit / advance",[new(firstId,"Bank",500)]);paymentService.ReverseReceipt(party.Id,cancelled.Id,new(2026,8,15),"Duplicate transfer corrected");
            var gapParty=store.CreateParty("QA fixed row positions");var gap=new WorksheetController(store);gap.Load("form",gapParty.Id);
            for(var i=1;i<=4;i++){var item=gap.Add();item["F9"]="POSITION-"+i;item["F10"]="2026-06-01";item["F5"]="15000";item["F6"]="10000";item["F7"]="9000";}
            gap.Save();gap.DeleteImmediately(gap.Rows[2]);var gapWindow=new MainWindow(store,database.Paths.Database);gapWindow.PreviewParty("form",gapParty.Id);Capture(gapWindow,System.IO.Path.Combine(directory,"blank-third-row.png"),1100,720);gapWindow.Close();

        }
        finally{System.IO.Directory.Delete(root,true);}
    }
    internal static void Capture(System.Windows.Window window,string path,int width,int height)
    {
        if(window.IsVisible){window.Width=width;window.Height=height;window.UpdateLayout();}
        var surface=(System.Windows.FrameworkElement)window.Content;
        surface.Measure(new(width,height));surface.Arrange(new(0,0,width,height));surface.UpdateLayout();
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);surface.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(surface);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var stream=System.IO.File.Create(path);encoder.Save(stream);
    }
    private static void Require(bool condition,string name){if(!condition)throw new InvalidOperationException(name);}
    public static void Run()
    {
        var root=System.IO.Path.Combine(AppContext.BaseDirectory,"TestData",Guid.NewGuid().ToString("N"));
        try
        {
            using(var db=new PortableDatabase(new(root)))
            {
                var store=new WorkbookStore(db);
                FixRegressionChecks.Run(store);
                InvoiceRegisterChecks.Run(store);
                var sheet=new WorksheetController(store);sheet.Load("details");Require(sheet.Rows.Count==0,"No dummy rows");
                var row=sheet.Add();row["B"]="TEST ONLY";row["C"]="15000";row["D"]="10000";row["E"]="3000";row["J"]="1200";row["L"]="2026-06-17";sheet.Save();
                Require(row["F"]=="7000.00"&&row["K"]=="3800.00"&&row["N"]=="2026-09-15","Details calculations");
                var stale=new WorksheetController(store);stale.Load("details");row["E"]="4000";sheet.Save();
                try{stale.Save();throw new Exception("Stale save accepted");}catch(BusinessRuleException){}
                sheet.Load("ledger");row=sheet.Add();row["B"]="TEST ONLY";row["C"]="100";row["D"]="25";row["I"]="2026-06-17";sheet.Opening=10;sheet.Save();Require(row["E"]=="85.00","Ledger calculation");
                sheet.Load("form");row=sheet.Add();row["F1"]="TEST ONLY";row["F9"]="TEST-1";row["F17"]="2026-06-17";sheet.Save();Require(row["F19"]=="2026-09-15"&&row["F20"]=="2026-08-16","Due and reminder");
                Require(ExcelFields.For("form").Count==21&&ExcelFields.For("details").Count==13&&ExcelFields.For("ledger").Count==9,"Exact field counts");
                var grid=new ExcelGrid(sheet.Fields,sheet.Rows);Require(grid.Table.Columns[19].IsReadOnly,"Calculated field lock");
                Require(DateCellConverter.Normalize("26/10/2026")=="2026-10-26","Indian date input");
                Require(DateCellConverter.Normalize("1-2-26")=="2026-02-01","Short date input uses day first");
                Require(DateCellConverter.Normalize("26-Oct-2026")=="2026-10-26","Named month input");
                Require(DateCellConverter.Normalize("31/02/2026")=="31/02/2026","Invalid date retained for validation");
                Require(!sheet.Fields.First(f=>f.Key=="F3").IsDate&&sheet.Fields.First(f=>f.Key=="F4").IsDate,"Identifiers remain text");
                var sheetGrid=new ExcelGrid(sheet.Fields,sheet.Rows,promote:draft=>sheet.Add(draft));
                Require(sheetGrid.Table.Items.Count>=101&&sheet.Rows.Count==1,"Blank worksheet does not create records");
                var draft=(GridRow)sheetGrid.Table.Items[1];draft["F1"]="BLANK ROW TEST";draft["F9"]="BLANK-2";
                Require(sheet.Rows.Count==2&&draft["_terms"]=="90","Typing promotes row with defaults");
                sheetGrid.Table.CurrentCell=new System.Windows.Controls.DataGridCellInfo(draft,sheetGrid.Table.Columns[17]);
                sheetGrid.Paste("26/10/2026");Require(draft["F17"]=="2026-10-26","Pasted dates normalized");
                var dateInput=new System.Windows.Controls.TextBox();
                dateInput.SetBinding(System.Windows.Controls.TextBox.TextProperty,new System.Windows.Data.Binding("[F17]"){Source=draft,Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.Explicit,Converter=new DateCellConverter()});
                dateInput.Text="5/11/2026";dateInput.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)!.UpdateSource();
                Require(draft["F17"]=="2026-11-05","Typed dates normalized through WPF binding");
                draft["F17"]=DateCellConverter.Normalize("17/6/2026");sheet.Save();
                Require(draft["F19"]=="2026-09-15","Derived due remains calculated");
                Require(store.Load("form").Rows.Count==3,"Only two entered rows and metadata persisted");
                var window=new MainWindow(store,db.Paths.Database);window.Measure(new System.Windows.Size(1440,880));window.Arrange(new System.Windows.Rect(0,0,1440,880));window.UpdateLayout();window.CheckMasterPages();window.CheckInvoiceFilters();window.CheckInteractions();window.CheckPartyInteractions();window.CheckWorkflowInteractions();window.CheckShortcutInteractions();window.CheckUndoRedoInteractions();window.CheckBlankRowInteractions();window.CheckThemeInteractions();window.CheckWindowViewport();window.Close();
            }
            using var reopened=new PortableDatabase(new(root));var s=new WorkbookStore(reopened);Require(s.Load("details").Rows.Count==2,"Persistence after reopen");
        }
        finally{System.IO.Directory.Delete(root,true);}
    }
}
