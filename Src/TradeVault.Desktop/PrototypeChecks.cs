namespace TradeVault.Desktop;

// Dependency-free checks invoked on the WPF STA thread with --self-test.
internal static class PrototypeChecks
{
    public static void Run()
    {
        var service=new MockPrototypeService();
        var plan=PastePlan.Create("A\tB\r\nC\tD\r\n",0,0,2,service.InvoiceFields);
        Require(plan.Count==4 && plan[3].Value=="D","rectangular paste");
        Reject(()=>PastePlan.Create("A\tB\nC",0,0,2,service.InvoiceFields));
        Reject(()=>PastePlan.Create("A\nB\nC",0,0,2,service.InvoiceFields));
        Reject(()=>PastePlan.Create("A",0,19,2,service.InvoiceFields));
        var row=service.NewInvoice(); Require(row["Terms"]=="90" && row["Shipping"]=="3800.00","defaults");
        row["Party"]="Synthetic"; row["Invoice"]="TEST"; row["Shipping"]="bad";
        Require(service.Validate(row).Issues.Any(x=>x.Field=="Shipping") && row.HasErrors,"service errors");
        row["Shipping"]="3800.00"; Require(service.Validate(row).Issues.Count==0,"error recovery");
        var grid=new ExcelGrid(service.InvoiceFields,service.Invoices);
        Require(grid.Table.Columns[19].IsReadOnly && !grid.Table.Columns[0].IsReadOnly,"locked columns");
        Require(grid.Table.FrozenColumnCount==2,"frozen identifiers");
        var report=service.Report(false); Require(new ExcelGrid(report.Item1,report.Item2,true).Table.IsReadOnly,"report locked");
        grid.Table.CurrentCell=new System.Windows.Controls.DataGridCellInfo(service.Invoices[0],grid.Table.Columns[0]);
        grid.Paste("Paste party A\tPASTE-1\nPaste party B\tPASTE-2");
        Require(service.Invoices[0]["Invoice"]=="PASTE-1" && service.Invoices[1]["Party"]=="Paste party B","grid paste applies rectangle");
        grid.Table.CurrentCell=new System.Windows.Controls.DataGridCellInfo(service.Invoices[0],grid.Table.Columns[18]);
        grid.Paste("2026-11-01\tforbidden");
        Require(service.Invoices[0]["BlDate"]=="2026-10-07","locked paste is atomic");
        var window=new MainWindow(service); window.CheckScreens(); window.Close();
    }
    private static void Require(bool condition,string name) { if (!condition) throw new InvalidOperationException(name); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Expected rejected paste"); }
}
