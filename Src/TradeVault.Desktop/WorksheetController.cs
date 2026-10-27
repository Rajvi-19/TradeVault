using System.Collections.ObjectModel;
using System.Globalization;
using TradeVault.Domain;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

public sealed class WorksheetController(WorkbookStore store, LoginSession session)
{
    public ObservableCollection<GridRow> Rows { get; } = [];
    public string Name { get; private set; } = "form";
    public int Revision { get; private set; }
    public decimal Opening { get; set; }
    public int Terms { get; set; } = 90;
    public DateOnly AsOf { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public IReadOnlyList<Field> Fields => ExcelFields.For(Name);
    public void Load(string name)
    {
        var saved = store.Load(session, name); Name = name; Revision = saved.Revision; Rows.Clear(); Opening = 0; Terms = 90;
        foreach (var data in saved.Rows)
        {
            if (data.ContainsKey("_metadata"))
            {
                Opening = decimal.Parse(data["opening"], CultureInfo.InvariantCulture);
                Terms = int.Parse(data["terms"], CultureInfo.InvariantCulture); continue;
            }
            var row = new GridRow(); foreach (var cell in data) row[cell.Key] = cell.Value; Rows.Add(row);
        }
        Calculate();
    }
    public GridRow Add(GridRow? draft=null)
    {
        var row = draft??new GridRow();
        if (Name == "form" && string.IsNullOrEmpty(row["F18"])) row["F18"] = "3800.00";
        if (Name == "details") { if(string.IsNullOrEmpty(row["H"]))row["H"] = "3800.00"; if(string.IsNullOrEmpty(row["M"]))row["M"] = "90"; }
        if (Name == "ledger" && string.IsNullOrEmpty(row["J"])) row["J"] = "90";
        Rows.Add(row); return row;
    }
    private static decimal Number(GridRow row, string key) => string.IsNullOrWhiteSpace(row[key]) ? 0 : decimal.Parse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture);
    private static void Set(GridRow row, string key, decimal value) => row[key] = value.ToString("0.00", CultureInfo.InvariantCulture);
    private static DateOnly? Date(GridRow row, string key) => string.IsNullOrWhiteSpace(row[key]) ? null : DateOnly.ParseExact(row[key], "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static void Due(GridRow row, string source, string terms, string target, int fallback = 90)
    {
        var date = Date(row, source);
        var days = string.IsNullOrWhiteSpace(terms) ? fallback : (string.IsNullOrWhiteSpace(row[terms]) ? fallback : int.Parse(row[terms], CultureInfo.InvariantCulture));
        row[target] = DueDates.Calculate(date, days)?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
    }
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var money = Name switch { "form" => new[] { "F5","F6","F7","F8","F18","F22","F23","F25" }, "details" => ["C","D","E","H","J"], _ => ["C","D","H"] };
        var dates = Name switch { "form" => new[] { "F4","F10","F13","F14","F17","F24","F26" }, "details" => ["L"], _ => ["I","G"] };
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i]; var issues = new List<CellIssue>();
            var required = Name == "form" ? new[] {"F1","F9"} : new[] {"B"};
            foreach (var key in required) if (string.IsNullOrWhiteSpace(row[key])) issues.Add(new(key, $"{Fields.First(f=>f.Key==key).Label} is required."));
            foreach (var key in money)
            {
                if (string.IsNullOrWhiteSpace(row[key])) continue;
                if (!decimal.TryParse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0 || decimal.Round(amount,2) != amount || amount > 1_000_000_000_000m)
                    issues.Add(new(key, $"{Fields.First(f=>f.Key==key).Label}: enter non-negative USD with at most two decimals (maximum 1 trillion)."));
            }
            foreach (var key in dates) if (!string.IsNullOrWhiteSpace(row[key]) && !DateOnly.TryParseExact(row[key], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) issues.Add(new(key, $"{Fields.First(f=>f.Key==key).Label}: use yyyy-MM-dd; leave unknown dates blank."));
            var term = Name == "details" ? "M" : Name == "ledger" ? "J" : "";
            if (term != "" && (!int.TryParse(row[term],out var days) || days < 0 || days > 3650)) issues.Add(new(term,"Terms must be 0–3650 calendar days."));
            row.SetIssues(issues); errors.AddRange(issues.Select(e=>$"Row {i+1}: {e.Message}"));
        }
        if (Terms is < 0 or > 3650) errors.Add("Default terms must be 0–3650 days.");
        return errors;
    }
    public void Calculate()
    {
        if (Validate().Count != 0)
        {
            foreach(var row in Rows) foreach(var field in Fields.Where(f=>f.Calculated)) row[field.Key]="";
            return;
        }
        decimal running = Opening;
        for (int i=0; i<Rows.Count; i++)
        {
            var row = Rows[i];
            if (Name == "details")
            {
                row["A"]=(i+1).ToString(); Set(row,"F",Number(row,"D")-Number(row,"E"));
                Set(row,"G",Number(row,"C")-Number(row,"D"));
                Set(row,"I",Number(row,"C")-Number(row,"D")-Number(row,"H"));
                running += Number(row,"I")-Number(row,"J"); Set(row,"K",running); Due(row,"L","M","N");
            }
            else if (Name == "ledger") { running+=Number(row,"C")-Number(row,"D"); Set(row,"E",running); Due(row,"I","J","K"); }
            else
            {
                Due(row,"F17","","F19",Terms);
                var due = Date(row,"F19");
                row["F20"] = due is {} d && d.DayNumber >= 30 ? d.AddDays(-30).ToString("yyyy-MM-dd") : "";
                row["F21"] = due is {} deadline ? Math.Max(0,AsOf.DayNumber-deadline.DayNumber).ToString() : "";
            }
        }
    }
    public void Save()
    {
        var errors = Validate(); if (errors.Count > 0) throw new BusinessRuleException(string.Join("\n",errors.Take(8)));
        Calculate();
        var data = new List<Dictionary<string,string>> { new() { ["_metadata"]="1", ["opening"]=Opening.ToString(CultureInfo.InvariantCulture), ["terms"]=Terms.ToString(CultureInfo.InvariantCulture) } };
        data.AddRange(Rows.Select(row => Fields.ToDictionary(f=>f.Key,f=>row[f.Key])));
        Revision = store.Save(session,Name,Revision,data);
    }
}
