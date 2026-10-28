using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TradeVault.Desktop;

public record Field(string Key, string Label, bool Calculated = false, int Width = 155)
{
    public bool IsNumeric=>!IsDate&&new[]{"AMOUNT","PAYMENT","CHARGES","TERMS","DAYS","BALANCE","DEBIT","CREDIT","SR. NO."}.Any(word=>Label.Contains(word,StringComparison.OrdinalIgnoreCase));
    public bool IsDate => Label.Contains("DATE",StringComparison.OrdinalIgnoreCase)
        && !Label.Contains("NO & DATE",StringComparison.OrdinalIgnoreCase)
        || Label.Equals("DUE",StringComparison.OrdinalIgnoreCase);
}
public record CellIssue(string Field, string Message);
public record ServiceResult(IReadOnlyList<CellIssue> Issues, string Message);
public record ReceiptSummary(string Total, string Allocated, string Unallocated);

// Desktop-only provisional boundary. Not a shared Application contract.
public interface IPrototypeService
{
    IReadOnlyList<Field> InvoiceFields { get; }
    ObservableCollection<GridRow> Invoices { get; }
    GridRow NewInvoice();
    ServiceResult Validate(GridRow row);
    ServiceResult SaveInvoices();
    ReceiptSummary ReceiptSummary { get; }
    ObservableCollection<GridRow> Allocations { get; }
    (IReadOnlyList<Field> Fields, ObservableCollection<GridRow> Rows) Report(bool ledger);
    (IReadOnlyList<Field> Fields, ObservableCollection<GridRow> Rows) Submissions();
}

public sealed class GridRow : INotifyPropertyChanged, INotifyDataErrorInfo
{
    public Dictionary<string,string> Snapshot()=>new(values);
    private readonly Dictionary<string, string> values = new();
    private IReadOnlyList<CellIssue> issues = [];
    public string this[string key]
    {
        get => values.GetValueOrDefault(key, "");
        set
        {
            if (this[key] == value) return;
            values[key] = value;
            PropertyChanged?.Invoke(this, new("Item[]"));
            Changed?.Invoke(this);
        }
    }
    public event Action<GridRow>? Changed;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    public bool HasErrors => issues.Count > 0;
    public IReadOnlyDictionary<string,string> Errors => issues.GroupBy(x=>x.Field).ToDictionary(g=>g.Key,g=>string.Join("\n",g.Select(x=>x.Message)));
    public IEnumerable GetErrors(string? propertyName) => string.IsNullOrEmpty(propertyName) ? issues.Select(x=>x.Message) : issues.Where(x=>propertyName==x.Field||propertyName==$"[{x.Field}]").Select(x=>x.Message);
    public void SetIssues(IReadOnlyList<CellIssue> next)
    {
        issues = next;
        ErrorsChanged?.Invoke(this, new("Item[]"));
        PropertyChanged?.Invoke(this, new(nameof(HasErrors)));
        PropertyChanged?.Invoke(this, new(nameof(Errors)));
    }
}
