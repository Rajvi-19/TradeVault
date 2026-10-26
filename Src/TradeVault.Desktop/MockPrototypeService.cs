using System.Collections.ObjectModel;
using System.Globalization;

namespace TradeVault.Desktop;

// Synthetic fixtures only. No authentication, formulas, file access or persistence.
public sealed class MockPrototypeService : IPrototypeService
{
    public IReadOnlyList<Field> InvoiceFields { get; } = [
        new("Party", "Party", Width:180), new("Invoice", "Invoice number"),
        new("InvoiceDate", "Invoice date (ISO)"), new("Proforma", "Proforma number"),
        new("ProformaDate", "Proforma date (ISO)"), new("Country", "Country"),
        new("Original", "Original / USD"), new("IndiaCustoms", "India customs / USD"),
        new("PakCustoms", "Pakistan customs / USD"), new("Bank", "Bank contract / USD"),
        new("Shipping", "Shipping / USD"), new("Terms", "Terms / days"),
        new("IndiaContainer", "India → MY container", Width:180), new("IndiaBl", "India BL number"),
        new("IndiaBlDate", "India BL date (ISO)"), new("Arrival", "MY arrival date (ISO)"),
        new("Container", "MY → PK container", Width:180), new("Bl", "MY → PK BL number"),
        new("BlDate", "MY → PK BL date (ISO)", Width:185),
        new("Due", "Due date · calculated", true,185), new("Outstanding", "Outstanding / USD · calculated", true,235)
    ];
    public ObservableCollection<GridRow> Invoices { get; } = [];
    public ObservableCollection<GridRow> Allocations { get; } = [];
    public ReceiptSummary ReceiptSummary { get; } = new("15,000.00", "10,000.00", "5,000.00");
    public MockPrototypeService()
    {
        Invoices.Add(Row(("Party","Demo Indigo Ltd"),("Invoice","DEMO-001"),("InvoiceDate","2026-10-01"),
            ("Country","Malaysia"),("Original","24000.00"),("IndiaCustoms","18000.00"),("PakCustoms","18000.00"),
            ("Bank","18000.00"),("Shipping","3800.00"),("Terms","90"),("BlDate","2026-10-07"),
            ("Due","2027-01-05"),("Outstanding","12,000.00")));
        Invoices.Add(Row(("Party","Demo Violet Trading"),("Invoice","DEMO-002"),("InvoiceDate","2026-10-10"),
            ("Country","Malaysia"),("Original","32000.00"),("Bank","22000.00"),("Shipping","3800.00"),
            ("Terms","90"),("Due","Pending"),("Outstanding","Pending")));
        Allocations.Add(Row(("Invoice","DEMO-001"),("Component","Bank contract"),("Amount","8000.00")));
        Allocations.Add(Row(("Invoice","DEMO-001"),("Component","Difference"),("Amount","2000.00")));
    }
    public GridRow NewInvoice() => Row(("Shipping","3800.00"),("Terms","90"),("Due","Pending"),("Outstanding","Pending"));
    public ServiceResult Validate(GridRow row)
    {
        List<CellIssue> issues = [];
        foreach (var key in new[] { "Party", "Invoice" })
            if (string.IsNullOrWhiteSpace(row[key])) issues.Add(new(key, $"{key} is required."));
        foreach (var key in new[] { "Original", "IndiaCustoms", "PakCustoms", "Bank", "Shipping" })
            if (row[key].Length > 0 && (!decimal.TryParse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture, out var n) || n < 0))
                issues.Add(new(key, $"{key}: enter a non-negative USD amount, e.g. 3800.00."));
        if (!int.TryParse(row["Terms"], out var days) || days < 0) issues.Add(new("Terms", "Terms: enter a non-negative whole number of days."));
        foreach (var key in new[] { "InvoiceDate", "ProformaDate", "IndiaBlDate", "Arrival", "BlDate" })
            if (row[key].Length > 0 && !DateOnly.TryParseExact(row[key], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                issues.Add(new(key, $"{key}: use yyyy-MM-dd or leave blank if unknown."));
        row.SetIssues(issues);
        return new(issues, issues.Count == 0 ? "Mock validation passed." : "Correct the highlighted row; inputs are retained.");
    }
    public ServiceResult SaveInvoices()
    {
        var errors = Invoices.SelectMany(x => Validate(x).Issues).ToArray();
        return new(errors, errors.Length == 0 ? "Mock save accepted in memory only. Nothing was written to disk." : "Mock save blocked. Correct validation errors.");
    }
    public (IReadOnlyList<Field>, ObservableCollection<GridRow>) Report(bool ledger) => ledger
        ? (new Field[] { new("Particulars","Particulars",Width:330),new("Debit","Debit / USD",true),new("Credit","Credit / USD",true),new("Balance","Balance / USD",true) },
            new() { Row(("Particulars","DEMO-001 · synthetic invoice"),("Debit","24000.00"),("Credit","0.00"),("Balance","24000.00")), Row(("Particulars","Synthetic receipt"),("Debit","0.00"),("Credit","12000.00"),("Balance","12000.00")) })
        : (new Field[] {new("Invoice","Invoice",Width:180),new("Original","Original / USD",true),new("Bank","Bank contract / USD",true),new("Pending","Pending bank / USD",true),new("Difference","Pending difference / USD",true,210),new("Due","Due date",true)},
            new() {Row(("Invoice","DEMO-001"),("Original","24000.00"),("Bank","18000.00"),("Pending","10000.00"),("Difference","2000.00"),("Due","2027-01-05"))});
    public (IReadOnlyList<Field>, ObservableCollection<GridRow>) Submissions() => (
        new Field[] {new("Submission","Submission",Width:210),new("Revision","Revision"),new("Through","Data through"),new("Status","Review status",Width:200)},
        new() {Row(("Submission","DEMO-SUBMISSION-001"),("Revision","1"),("Through","2026-10-26"),("Status","Awaiting review"))});
    private static GridRow Row(params (string Key,string Value)[] cells)
    {
        var row = new GridRow(); foreach (var (key,value) in cells) row[key] = value; return row;
    }
}
