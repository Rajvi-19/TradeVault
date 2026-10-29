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
            var login=new LoginWindow(store);login.PreviewUsername("operator");Capture(login,System.IO.Path.Combine(directory,"login.png"),480,490);Capture(login,System.IO.Path.Combine(directory,"login-minimum.png"),400,460);login.Close();
            var session=store.Login("owner","Owner@123");AcceptanceChecks.Seed(store,session);var window=new MainWindow(store,session,database.Paths.Database);
            foreach(var name in new[]{"form","details","ledger"}){window.PreviewSheet(name);Capture(window,System.IO.Path.Combine(directory,name+".png"),1480,920);if(name=="form")Capture(window,System.IO.Path.Combine(directory,"form-minimum.png"),1100,720);}
            Capture(window,System.IO.Path.Combine(directory,"compact-window.png"),1000,600);
            var partyDialog=new PartyDialog(store,session);var partySurface=(System.Windows.FrameworkElement)partyDialog.Content;partySurface.Measure(new(440,double.PositiveInfinity));Capture(partyDialog,System.IO.Path.Combine(directory,"new-party.png"),440,(int)Math.Ceiling(partySurface.DesiredSize.Height));partyDialog.Close();
            var deleteDialog=new ConfirmDeleteWindow(window,"TEST-INV-006","[TEST] Sample party");var deleteSurface=(System.Windows.FrameworkElement)deleteDialog.Content;deleteSurface.Measure(new(560,double.PositiveInfinity));Capture(deleteDialog,System.IO.Path.Combine(directory,"delete-confirmation.png"),560,(int)Math.Ceiling(deleteSurface.DesiredSize.Height));deleteDialog.Close();
            var inbox=new ImportedReportsWindow(window,new ReportWorkflow(store,session));Capture(inbox,System.IO.Path.Combine(directory,"operator-reports.png"),1180,750);inbox.Close();window.Close();
            var party=store.CreateParty(session,"QA generated reports");var controller=new WorksheetController(store,session);controller.Load("form",party.Id);var row=controller.Add();row["F9"]="QA-GENERATED-001";row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F17"]="2026-06-17";controller.Save();
            new ReportWorkflow(store,session).Configure(party.Id,new("F6","F22",true,new(2026,1,1)));var generated=new MainWindow(store,session,database.Paths.Database);generated.PreviewParty("details",party.Id);Capture(generated,System.IO.Path.Combine(directory,"generated-details.png"),1440,880);generated.PreviewParty("ledger",party.Id);Capture(generated,System.IO.Path.Combine(directory,"generated-ledger.png"),1440,880);generated.Close();
            var paymentView=new PaymentsWindow(window,new ReportWorkflow(store,session),party.Id);Capture(paymentView,System.IO.Path.Combine(directory,"payments.png"),1180,700);Capture(paymentView,System.IO.Path.Combine(directory,"payments-small.png"),1000,600);paymentView.Close();
            var op=store.Login("operator","Operator@123");var operatorWindow=new MainWindow(store,op,database.Paths.Database);Capture(operatorWindow,System.IO.Path.Combine(directory,"operator.png"),1440,880);operatorWindow.Close();
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
                AuthUiChecks.Run(store);
                try{store.Login("owner","wrong");throw new Exception("Bad password accepted");}catch(BusinessRuleException){}
                var owner=store.Login("owner","Owner@123");var op=store.Login("operator","Operator@123");
                FixRegressionChecks.Run(store,owner);
                try{store.Load(op,"operator-reports");throw new Exception("Operator access accepted");}catch(BusinessRuleException){}
                var sheet=new WorksheetController(store,owner);sheet.Load("details");Require(sheet.Rows.Count==0,"No dummy rows");
                var row=sheet.Add();row["B"]="TEST ONLY";row["C"]="15000";row["D"]="10000";row["E"]="3000";row["J"]="1200";row["L"]="2026-06-17";sheet.Save();
                Require(row["F"]=="7000.00"&&row["K"]=="0.00"&&row["N"]=="2026-09-15","Details calculations");
                var stale=new WorksheetController(store,owner);stale.Load("details");row["E"]="4000";sheet.Save();
                try{stale.Save();throw new Exception("Stale save accepted");}catch(BusinessRuleException){}
                sheet.Load("ledger");row=sheet.Add();row["B"]="TEST ONLY";row["C"]="100";row["D"]="25";row["I"]="2026-06-17";sheet.Opening=10;sheet.Save();Require(row["E"]=="85.00","Ledger calculation");
                sheet.Load("form");row=sheet.Add();row["F1"]="TEST ONLY";row["F9"]="TEST-1";row["F17"]="2026-06-17";sheet.Save();Require(row["F19"]=="2026-09-15"&&row["F20"]=="2026-08-16","Due and reminder");
                Require(ExcelFields.For("form").Count==26&&ExcelFields.For("details").Count==14&&ExcelFields.For("ledger").Count==9,"Exact field counts");
                var grid=new ExcelGrid(sheet.Fields,sheet.Rows);Require(grid.Table.Columns[18].IsReadOnly,"Calculated field lock");
                Require(DateCellConverter.Normalize("26/10/2026")=="2026-10-26","Indian date input");
                Require(DateCellConverter.Normalize("1-2-26")=="2026-02-01","Short date input uses day first");
                Require(DateCellConverter.Normalize("26-Oct-2026")=="2026-10-26","Named month input");
                Require(DateCellConverter.Normalize("31/02/2026")=="31/02/2026","Invalid date retained for validation");
                Require(!sheet.Fields.First(f=>f.Key=="F3").IsDate&&sheet.Fields.First(f=>f.Key=="F4").IsDate,"Identifiers remain text");
                var sheetGrid=new ExcelGrid(sheet.Fields,sheet.Rows,promote:draft=>sheet.Add(draft));
                Require(sheetGrid.Table.Items.Count>=101&&sheet.Rows.Count==1,"Blank worksheet does not create records");
                var draft=(GridRow)sheetGrid.Table.Items[1];draft["F1"]="BLANK ROW TEST";draft["F9"]="BLANK-2";
                Require(sheet.Rows.Count==2&&draft["F18"]=="3800.00","Typing promotes row with defaults");
                sheetGrid.Table.CurrentCell=new System.Windows.Controls.DataGridCellInfo(draft,sheetGrid.Table.Columns[3]);
                sheetGrid.Paste("26/10/2026");Require(draft["F4"]=="2026-10-26","Pasted dates normalized");
                var dateInput=new System.Windows.Controls.TextBox();
                dateInput.SetBinding(System.Windows.Controls.TextBox.TextProperty,new System.Windows.Data.Binding("[F4]"){Source=draft,Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.Explicit,Converter=new DateCellConverter()});
                dateInput.Text="5/11/2026";dateInput.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)!.UpdateSource();
                Require(draft["F4"]=="2026-11-05","Typed dates normalized through WPF binding");
                draft["F17"]=DateCellConverter.Normalize("17/6/2026");sheet.Save();
                Require(draft["F19"]=="2026-09-15","Derived due remains calculated");
                Require(store.Load(owner,"form").Rows.Count==3,"Only two entered rows and metadata persisted");
                var window=new MainWindow(store,owner,db.Paths.Database);window.Measure(new System.Windows.Size(1440,880));window.Arrange(new System.Windows.Rect(0,0,1440,880));window.UpdateLayout();window.CheckInteractions();window.CheckPartyInteractions();window.CheckWorkflowInteractions();window.CheckShortcutInteractions();window.Close();
                store.Logout(owner);try{store.Load(owner,"form");throw new Exception("Expired session accepted");}catch(BusinessRuleException){}
            }
            using var reopened=new PortableDatabase(new(root));var s=new WorkbookStore(reopened);var session=s.Login("owner","Owner@123");Require(s.Load(session,"details").Rows.Count==2,"Persistence after reopen");
        }
        finally{System.IO.Directory.Delete(root,true);}
    }
}
