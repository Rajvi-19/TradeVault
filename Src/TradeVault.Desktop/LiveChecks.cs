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
            var login=new LoginWindow(store);login.PreviewUsername("operator");Capture(login,System.IO.Path.Combine(directory,"login.png"),860,570);Capture(login,System.IO.Path.Combine(directory,"login-minimum.png"),760,540);login.Close();
            var session=store.Login("owner","Owner@123");AcceptanceChecks.Seed(store,session);var window=new MainWindow(store,session,database.Paths.Database);
            foreach(var name in new[]{"form","details","ledger"}){window.PreviewSheet(name);Capture(window,System.IO.Path.Combine(directory,name+".png"),1480,920);if(name=="form")Capture(window,System.IO.Path.Combine(directory,"form-minimum.png"),1100,720);}
            Capture(window,System.IO.Path.Combine(directory,"compact-window.png"),1000,600);window.Close();
            var op=store.Login("operator","Operator@123");var operatorWindow=new MainWindow(store,op,database.Paths.Database);Capture(operatorWindow,System.IO.Path.Combine(directory,"operator.png"),1440,880);operatorWindow.Close();
        }
        finally{System.IO.Directory.Delete(root,true);}
    }
    private static void Capture(System.Windows.Window window,string path,int width,int height)
    {
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
                var window=new MainWindow(store,owner,db.Paths.Database);window.Measure(new System.Windows.Size(1440,880));window.Arrange(new System.Windows.Rect(0,0,1440,880));window.UpdateLayout();window.CheckInteractions();window.CheckPartyInteractions();window.Close();
                store.Logout(owner);try{store.Load(owner,"form");throw new Exception("Expired session accepted");}catch(BusinessRuleException){}
            }
            using var reopened=new PortableDatabase(new(root));var s=new WorkbookStore(reopened);var session=s.Login("owner","Owner@123");Require(s.Load(session,"details").Rows.Count==2,"Persistence after reopen");
        }
        finally{System.IO.Directory.Delete(root,true);}
    }
}
