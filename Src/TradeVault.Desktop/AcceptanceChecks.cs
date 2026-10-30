using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TradeVault.Domain;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal static class AcceptanceChecks
{
    private sealed record Result(string Id,string Feature,string Status,string Evidence);
    internal static void Seed(WorkbookStore store)
    {
        var sheet=new WorksheetController(store){AsOf=new(2026,10,26)};
        sheet.Load("form");
        for(int i=1;i<=12;i++)
        {
            var row=sheet.Add();row["F1"]=i%2==0?"QA Indigo Exports":"QA Violet Trading";row["F2"]="Malaysia";
            row["F3"]=$"QA-PI-{i:000}";row["F4"]="2026-05-01";row["F5"]="15000.00";row["F6"]="10000.00";row["F7"]="10000.00";
            row["F8"]="5000.00";row["F9"]=$"QA-INV-{i:000}";row["F10"]="2026-06-01";row["F11"]=$"QA-IN-{i:000}";row["F12"]=$"QA-BL-IN-{i:000}";
            row["F13"]="2026-06-01";row["F14"]="2026-06-12";row["F15"]=$"QA-MY-{i:000}";row["F16"]=$"QA-BL-MY-{i:000}";
            row["F17"]="2026-06-17";row["F22"]="3000.00";row["F23"]="1200.00";row["F24"]="2026-07-01";row["F25"]="3000.00";row["F26"]="2026-07-01";
        }
        sheet.Save();sheet.Load("details");
        for(int i=1;i<=12;i++){var row=sheet.Add();row["B"]=$"QA-INV-{i:000} / 01-Jun-2026";row["C"]="15000";row["D"]="10000";row["E"]="3000";row["J"]="1200";row["L"]="2026-06-17";}
        sheet.Save();sheet.Load("ledger");sheet.Opening=100;
        for(int i=1;i<=12;i++){
            var invoice=sheet.Add();invoice["B"]=$"QA-INV-{i:000}";invoice["C"]="15000";invoice["I"]="2026-06-17";
            var receipt=sheet.Add();receipt["B"]=$"QA receipt {i:000}";receipt["D"]="3000";
            var shipping=sheet.Add();shipping["B"]=$"QA shipping {i:000}";shipping["D"]="3800";
        }
        sheet.Save();
    }
    internal static void LaunchWorkspace()
    {
        var root=System.IO.Path.Combine(AppContext.BaseDirectory,"TestData",Guid.NewGuid().ToString("N"));
        var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);Seed(store);
        var window=new MainWindow(store,db.Paths.Database);window.Title="TradeVault QA — SYNTHETIC DATA ONLY | "+Ui.BuildLabel;
        window.Closed+=(_,_)=>{db.Dispose();Clean(root);System.Windows.Application.Current.Shutdown();};window.Show();
    }
    private static void Clean(string root)
    {
        var parent=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"TestData"))+System.IO.Path.DirectorySeparatorChar;
        var target=System.IO.Path.GetFullPath(root);
        if(!target.StartsWith(parent,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe QA cleanup path");
        // Retain isolated synthetic databases as QA evidence. No production folder is touched.
    }
    internal static bool Run(string output)
    {
        output=System.IO.Path.GetFullPath(output);System.IO.Directory.CreateDirectory(output);
        var results=new List<Result>();
        void Check(string id,string feature,Action test,string evidence)
        {try{test();results.Add(new(id,feature,"PASS",evidence));}catch(Exception ex){results.Add(new(id,feature,"FAIL",ex.Message));}}
        static void Assert(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        static void Reject(Action test){try{test();}catch(BusinessRuleException){return;}throw new InvalidOperationException("Expected business validation rejection");}
        var root=System.IO.Path.Combine(AppContext.BaseDirectory,"TestData",Guid.NewGuid().ToString("N"));
        try
        {
            using(var db=new PortableDatabase(new(root)))
            {
                var store=new WorkbookStore(db);
                Check("D01","Fresh datasets empty",()=>Assert(store.Load("form").Rows.Count==0,"Unexpected business rows"),"Fresh isolated SQLite database contains no company rows.");
                Seed(store);var sheet=new WorksheetController(store);sheet.Load("form");
                Check("D02","Synthetic 12-invoice save/reload",()=>Assert(sheet.Rows.Count==12&&sheet.Rows[0]["F9"]=="QA-INV-001","Reload mismatch"),"12 invoices with 26 source fields persisted/reloaded.");
                Check("D03","Shipping default",()=>Assert(sheet.Rows.All(r=>r["F18"]=="3800.00"),"Default differs"),"All 12 invoice shipping inputs start at USD 3,800.");
                Check("D04","BL + terms due date",()=>Assert(sheet.Rows.All(r=>r["F19"]=="2026-09-15"),"Due date mismatch"),"17-Jun-2026 + 90 days = 15-Sep-2026.");
                Check("D05","Reminder and as-of overdue",()=>{sheet.AsOf=new(2026,10,26);sheet.Calculate();Assert(sheet.Rows[0]["F20"]=="2026-08-16"&&sheet.Rows[0]["F21"]=="41","Reminder/overdue mismatch");},"Reminder 16-Aug; overdue 41 days as of 26-Oct.");
                Check("D06","Editable terms",()=>{sheet.Rows[0]["_terms"]="60";sheet.Calculate();Assert(sheet.Rows[0]["F19"]=="2026-08-16","Terms ignored");sheet.Rows[0]["_terms"]="90";sheet.Calculate();},"Changing terms to 60 recalculates due to 16-Aug.");
                Check("D07","Editable shipping persistence",()=>{sheet.Rows[0]["F18"]="4200.00";sheet.Save();sheet.Load("form");Assert(sheet.Rows[0]["F18"]=="4200.00","Shipping not saved");},"USD 4,200 remains after reload; historical default does not overwrite.");
                Check("D08","Invalid USD blocked; revision unchanged",()=>{var revision=sheet.Revision;sheet.Rows[0]["F5"]="-1";Reject(sheet.Save);Assert(sheet.Revision==revision,"Rejected save changed revision");sheet.Load("form");},"Negative amount rejected without saving.");
                Check("D09","Sub-cent amount blocked",()=>{sheet.Rows[0]["F5"]="1.001";Reject(sheet.Save);sheet.Load("form");},"USD precision beyond cents is rejected.");
                Check("D10","Required party/invoice validation",()=>{sheet.Rows[0]["F1"]="";Reject(sheet.Save);sheet.Load("form");},"Missing party blocks save and valid persisted data remains.");
                Check("D11","Invalid date blocked",()=>{sheet.Rows[0]["F4"]="2026-02-31";Reject(sheet.Save);sheet.Load("form");},"Invalid calendar day rejected.");
                Check("D12","Unknown dates remain blank",()=>{sheet.Rows[0]["F17"]="";sheet.Calculate();Assert(sheet.Rows[0]["F19"]==""&&sheet.Rows[0]["F20"]=="","Invented due date");sheet.Load("form");},"No BL date produces no due/reminder date.");
                Check("D13","Stale update rejected",()=>{var other=new WorksheetController(store);other.Load("form");sheet.Save();Reject(other.Save);},"Revision conflict rejected instead of overwriting changes.");
                Check("D14","Deletion saved and reloaded",()=>{sheet.Rows.RemoveAt(11);sheet.Save();sheet.Load("form");Assert(sheet.Rows.Count==11,"Deleted row returned");},"Delete underlying record, save and reload retains 11 records.");
                Check("C01","Details formula results",()=>{sheet.Load("details");Assert(sheet.Rows.Count==12&&sheet.Rows.All(r=>r["F"]=="7000.00"&&r["I"]=="1200.00"&&r["K"]=="0.00"),"Details mismatch");},"Per row: pending bank 7,000; net difference 1,200; received difference 1,200; running pending 0.");
                Check("C02","Cumulative receipt input recalculates",()=>{sheet.Rows[0]["E"]="5000";sheet.Calculate();Assert(sheet.Rows[0]["F"]=="5000.00","Receipt not reflected");sheet.Load("details");},"Changing manual bank-received input 3,000 to 5,000 reduces pending to 5,000. This is not receipt-allocation UI.");
                Check("C03","Corrected first-row difference rule",()=>{sheet.Rows[0]["J"]="0";sheet.Calculate();Assert(sheet.Rows[0]["K"]=="1200.00"&&sheet.Rows[1]["K"]=="1200.00","First-row running balance mismatch");sheet.Load("details");},"First invoice follows the same net-minus-receipts rule as later invoices.");
                Check("C04","Ledger debits/credits and opening",()=>{sheet.Load("ledger");Assert(sheet.Rows.Count==36&&sheet.Rows.Last()["E"]=="98500.00","Ledger total mismatch");},"Opening 100 + 12 × (15,000 − 3,000 − 3,800) = 98,500.");
                Check("C05","Source workbook field counts",()=>Assert(ExcelFields.For("form").Count==26&&ExcelFields.For("details").Count==14&&ExcelFields.For("ledger").Count==9,"Field mismatch"),"26 invoice, 14 details, 9 ledger columns.");
                Check("U01","Date normalization",()=>Assert(DateCellConverter.Normalize("26/10/2026")=="2026-10-26"&&DateCellConverter.Normalize("26-Oct-2026")=="2026-10-26","Date normalization failed"),"Day-first and named month normalize to ISO.");
                Check("U02","Invalid date input retained",()=>Assert(DateCellConverter.Normalize("31/02/2026")=="31/02/2026","Invalid date silently converted"),"Invalid date preserved so validation can explain it.");
                Check("U12","Calendar date validation is immediate",()=>{
                    var editor=new DateCellEditor{Value="54-04-2007"};Assert(editor.HasInvalidDate&&!editor.TryCommitDate(),"Day 54 was accepted");
                    editor.Value="31-02-2026";Assert(editor.HasInvalidDate&&!editor.TryCommitDate(),"February 31 was accepted");
                    editor.Value="29-02-2024";Assert(!editor.HasInvalidDate&&editor.TryCommitDate()&&editor.Value=="2024-02-29","Valid leap day rejected");editor.Value="";Assert(editor.TryCommitDate(),"Blank optional date rejected");
                },"Impossible days and month lengths are flagged immediately; real leap days and blank optional dates remain valid.");
                sheet.Load("form");var grid=new ExcelGrid(sheet.Fields,sheet.Rows,promote:draft=>sheet.Add(draft));
                var surface=new Grid();surface.Children.Add(grid);surface.Measure(new(1200,600));surface.Arrange(new(0,0,1200,600));surface.UpdateLayout();System.Windows.Application.Current.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
                Check("U03","Continuous blank sheet without stored blanks",()=>Assert(grid.Table.Items.Count>=sheet.Rows.Count+100&&sheet.Rows.Count==11,"Blank rows persisted"),"100 spare rows available independently of stored rows.");
                Check("U04","Rectangular paste",()=>{grid.Table.CurrentCell=new(sheet.Rows[0],grid.Table.Columns[0]);grid.Paste("QA Paste One\tMalaysia\nQA Paste Two\tMalaysia");Assert(sheet.Rows[0]["F1"]=="QA Paste One"&&sheet.Rows[1]["F1"]=="QA Paste Two","Paste failed");},"2×2 range updates the selected input cells.");
                Check("U05","Paste date normalization",()=>{grid.Table.CurrentCell=new(sheet.Rows[0],grid.Table.Columns[3]);grid.Paste("1/2/2026");Assert(sheet.Rows[0]["F4"]=="2026-02-01","Pasted date mismatch");},"1/2/2026 is interpreted as 1-Feb-2026.");
                Check("U06","Calculated cells locked; mixed paste atomic",()=>{var before=sheet.Rows[0]["F18"];grid.Table.CurrentCell=new(sheet.Rows[0],grid.Table.Columns[17]);grid.Paste("9999\tforbidden");Assert(grid.Table.Columns[18].IsReadOnly&&sheet.Rows[0]["F18"]==before,"Locked paste changed data");},"Paste spanning shipping and locked due date is rejected before any changes.");
                Check("U07","Malformed paste rejected",()=>{var before=sheet.Rows[0]["F1"];grid.Table.CurrentCell=new(sheet.Rows[0],grid.Table.Columns[0]);grid.Paste("bad\tshape\nragged");Assert(sheet.Rows[0]["F1"]==before,"Partial malformed paste");},"Ragged range does not change cells.");
                Check("U08","Find / filter and clear",()=>{grid.FilterForCheck("QA Paste One");Assert(grid.Table.Items.Count==1,"Filter mismatch");grid.FilterForCheck("");Assert(grid.Table.Items.Count>100,"Clear filter mismatch");},"Matching one real row then clearing restores full worksheet.");
                Check("U09","Frozen identifiers and resizable columns",()=>Assert(grid.Table.FrozenColumnCount==2&&grid.Table.CanUserResizeColumns,"Grid settings mismatch"),"Two frozen identifiers; column resizing enabled.");
                Check("U10","Typing promotes row and preserves defaults",()=>{var row=grid.Table.Items.Cast<GridRow>().Last();row["F1"]="QA Typed";row["F9"]="QA-TYPED";Assert(sheet.Rows.Contains(row)&&row["F18"]=="3800.00","Promotion failed");},"A spare row becomes a record only after input.");
                sheet.Load("form");
                Check("U11","Read-only grid rejects paste",()=>{var view=new ExcelGrid(sheet.Fields,sheet.Rows,true);var before=sheet.Rows[0]["F1"];view.Paste("forbidden");Assert(view.Table.IsReadOnly&&sheet.Rows[0]["F1"]==before,"Readonly modified");},"Read-only view refuses programmatic paste.");
                Check("U13","Tab commits valid dates and skips calculated cells",()=>{
                    var row=sheet.Rows[0];row["F4"]="54-04-2007";grid.Table.CurrentCell=new(row,grid.Table.Columns[3]);grid.MoveByTab(false);Assert(grid.Table.CurrentColumn==grid.Table.Columns[3],"Tab left an invalid date");
                    row["F4"]="2024-02-29";grid.MoveByTab(false);Assert(grid.Table.CurrentColumn==grid.Table.Columns[4],"Tab did not select next input");
                    grid.Table.CurrentCell=new(row,grid.Table.Columns[6]);grid.MoveByTab(false);Assert(grid.Table.CurrentColumn==grid.Table.Columns[8],"Tab did not skip locked difference");grid.MoveByTab(true);Assert(grid.Table.CurrentColumn==grid.Table.Columns[6],"Shift Tab did not return to prior input");
                    grid.Table.CurrentCell=new(row,grid.Table.Columns[^1]);row["F26"]="";grid.MoveByTab(false);Assert(grid.Table.CurrentColumn==grid.Table.Columns[0]&&grid.Table.CurrentItem!=row,"Tab did not wrap to next row");
                },"Invalid date keeps current cell; Tab/Shift+Tab navigate input columns and Tab wraps to next row.");
                Check("U14","Invalid pasted date is atomic",()=>{
                    var row=sheet.Rows[0];var before=row["F3"];grid.Table.CurrentCell=new(row,grid.Table.Columns[2]);grid.Paste("SHOULD-NOT-CHANGE\t54-04-2007");Assert(row["F3"]==before,"Invalid date paste partially changed data");
                },"A range containing an impossible date is rejected before changing any input.");
                Check("U15","Row header selects the entire visible record",()=>{
                    var row=sheet.Rows[0];Assert(grid.SelectRecordRow(row),"Row selection failed");Assert(grid.Table.SelectedCells.Count==grid.Table.Columns.Count(c=>c.Visibility==Visibility.Visible)&&grid.Table.SelectedCells.All(c=>ReferenceEquals(c.Item,row)),"Not all record cells selected");
                },"Row-header handler selects every visible column of one record, including calculated cells.");
                Check("U16","Cell cut clears only selected editable values",()=>{
                    var row=sheet.Rows[0];var original=row["F2"];var count=sheet.Rows.Count;string copied="";grid.Table.SelectedCells.Clear();grid.Table.CurrentCell=new(row,grid.Table.Columns[1]);grid.Table.SelectedCells.Add(grid.Table.CurrentCell);row["F2"]="CUT-CELL";grid.ClipboardWriter=text=>copied=text;
                    Assert(grid.WholeSelectedRecord==null,"Single cell classified as a whole record");grid.CutSelectedCells();Assert(copied=="CUT-CELL"&&row["F2"]==""&&sheet.Rows.Count==count,"Cell cut removed a record or lost clipboard text");
                    row["F2"]="KEEP-CELL";grid.ClipboardWriter=_=>throw new InvalidOperationException("Clipboard busy");try{grid.CutSelectedCells();throw new Exception("Clipboard failure ignored");}catch(InvalidOperationException){}Assert(row["F2"]=="KEEP-CELL","Clipboard failure cleared data");
                    grid.Table.SelectedCells.Clear();grid.Table.CurrentCell=new(row,grid.Table.Columns[7]);grid.Table.SelectedCells.Add(grid.Table.CurrentCell);try{grid.ClearSelectedCells();throw new Exception("Calculated cell cleared");}catch(ArgumentException){}grid.ClipboardWriter=null;row["F2"]=original;
                },"Ctrl+X path copies before clearing; cells do not remove rows, clipboard failure is non-destructive, and formulas stay locked.");
                Check("U17","Whole-record copy includes all editable dates",GridInteractionChecks.CopyDates,"WPF clipboard bindings round-trip all seven input dates; calculated dates stay locked.");
                Check("U18","Ctrl-click row selection toggles multiple records",GridInteractionChecks.MultipleRows,"Two nonadjacent rows remain selected; Ctrl toggles and plain click replaces selection.");
                Check("U19","Horizontal wheel scrolls worksheet in both directions",GridInteractionChecks.HorizontalScroll,"Horizontal wheel deltas move the sheet right and left while retaining frozen columns.");
                Check("U20","Native Windows touchpad messages reach the worksheet",GridInteractionChecks.NativeHorizontalScroll,"Real HWND dispatch covers horizontal mouse/pointer wheels, shifted-wheel drivers and legacy horizontal scrolling without cached mouse-over.");
                Check("U21","Cleared row stays in place without shifting later records",GridInteractionChecks.RenumberAfterDelete,"Clearing row 3 keeps it blank and record 4 at row 4; worksheet position numbers stay unchanged.");
                var main=new MainWindow(store,db.Paths.Database);
                Check("W04","Workspace fits a 1366 by 768 desktop",()=>{var area=new Rect(0,0,1366,728);var size=Ui.WorkspaceSize(area);Assert(size.Width<=area.Width&&size.Height<=area.Height,"Startup window extends outside work area");},"Startup size accounts for taskbar space so caption and report navigation remain on screen.");
                Check("W05","All three worksheet tabs remain visible",main.CheckNavigationLayout,"Invoice register, Account details and Debit & credit button bounds are inside the 1366 by 728 workspace.");
                Check("W01","Visible caption controls on workspace",()=>Assert(new Window[]{main}.All(w=>w.WindowStyle==WindowStyle.None&&w.ResizeMode==ResizeMode.CanResize&&System.Windows.Shell.WindowChrome.GetWindowChrome(w)!=null),"Custom caption missing"),"The workspace window have explicit minimize/maximize/close controls and resize chrome; OS click acceptance is separate.");
                Check("W02","All sheet layouts stay compact",()=>{foreach(var name in new[]{"form","details","ledger"}){main.PreviewSheet(name);var content=(FrameworkElement)main.Content;content.Measure(new(1000,600));content.Arrange(new(0,0,1000,600));content.UpdateLayout();Assert(main.SheetTop<290,"Worksheet starts too low");}},"Worksheet component begins above 290px including the explicit 36px caption at 1000×600 content size.");
                Check("W03","UI Save/reload handlers",main.CheckInteractions,"Real MainWindow input → Save → reload round trip; Save disabled after success.");
                Check("U22","Worksheet undo and redo preserve edits and stored records",main.CheckUndoRedoInteractions,"Keyboard undo/redo covers formulas, pasted dates, cell cuts, deletion, redo invalidation, unrelated drafts and reload scope.");
                Check("U23","Cut and delete preserve reusable blank row positions",main.CheckBlankRowInteractions,"Delete/cut, undo/redo, reopening and re-entry preserve lower record positions; empty rows are excluded from reports.");
                Check("U24","Square theme toggle updates existing worksheet without data changes",main.CheckThemeInteractions,"Two clicks switch dark/light in place; button follows Save; inputs/revision/undo history unchanged.");
                Check("U25","Theme preference survives reload and invalid settings are harmless",ThemeChecks.Preference,"Preference stored only in isolated application-local test folder; dark/light reload and invalid-file fallback verified.");
                Check("U26","Light and dark theme text contrast",ThemeChecks.Contrast,"Body, muted and accent text meet 4.5:1 against surfaces; white primary-button text also meets 4.5:1.");
                Check("C06","Register difference is calculated and protected",()=>{
                    var party=store.CreateParty("[TEST] Calculated difference");var wf=new ReportWorkflow(store);
                    var c=new WorksheetController(store);c.Load("form",party.Id);var row=c.Add();row["F9"]="AUTO-DIFF";row["F5"]="15000";row["F6"]="10000";row["F7"]="9000";row["F8"]="999";c.Calculate();Assert(row["F8"]=="6000.00"&&c.Fields.Single(f=>f.Key=="F8").Calculated,"Difference not calculated/locked");
                    row["F5"]="16000";c.Save();c.Load("form",party.Id);Assert(c.Rows[0]["F8"]=="7000.00","Difference did not refresh/persist");
                    c.Load("ledger",party.Id);var movement=c.Add();movement["B"]="TEST DUE";movement["I"]="2026-06-17";c.Calculate();Assert(movement["G"]=="2026-09-15"&&movement["G"]==movement["K"],"Due columns differ");
                },"Difference uses original minus Pakistan customs independently of the fixed F6 bank-contract source, overrides stale input, updates after edit/reload and locks F8. Both legacy ledger due columns follow BL + terms.");
                main.Close();
            }
            using var reopened=new PortableDatabase(new(root));var persistedStore=new WorkbookStore(reopened);
            Check("D15","Database reopen persistence",()=>Assert(persistedStore.Load("details").Rows.Count==13,"Missing persisted rows"),"12 detail rows plus metadata survive database disposal/reopen.");
        }
        catch(Exception ex){results.Add(new("HARNESS","Acceptance harness","FAIL",ex.Message));}
        finally{Clean(root);}
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"desktop-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        var text=new StringBuilder("# TradeVault desktop acceptance results\n\nSynthetic data only; production database was not opened.\n\n");
        text.AppendLine($"Build: {Ui.BuildLabel}. PASS: {results.Count(r=>r.Status=="PASS")}; FAIL: {results.Count(r=>r.Status=="FAIL")}.\n");
        text.AppendLine("| ID | Feature | Result | Evidence |\n|---|---|---|---|");
        foreach(var result in results)text.AppendLine($"| {result.Id} | {result.Feature} | {result.Status} | {result.Evidence.Replace("|","/").Replace("\n"," ")} |");
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"desktop-results.md"),text.ToString());
        Console.WriteLine($"Acceptance: {results.Count(r=>r.Status=="PASS")} passed; {results.Count(r=>r.Status=="FAIL")} failed.");return results.All(r=>r.Status=="PASS");
    }
}
