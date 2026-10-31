using System.Collections.ObjectModel;
using System.Globalization;
using TradeVault.Domain;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

public sealed class WorksheetController(WorkbookStore store)
{
    public ObservableCollection<GridRow> Rows { get; } = [];
    public IEnumerable<GridRow> Records=>Rows.Where(row=>row["_blank"]!="1");
    public string Name { get; private set; } = "form";
    public Guid PartyId {get;private set;}
    public string PartyName {get;private set;}="Unassigned records";
    public int Revision { get; private set; }
    public decimal Opening { get; set; }
    public int Terms { get; set; } = 90;
    public DateOnly AsOf { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public IReadOnlyList<Field> Fields => ExcelFields.For(Name);
    public void Load(string name,Guid? partyId=null)
    {
        var selected=partyId??PartyId;
        var saved = store.Load( name,selected);
        var party=store.GetParties().Single(p=>p.Id==selected);
        PartyId=selected;PartyName=party.Name;Name = name; Revision = saved.Revision; Rows.Clear(); Opening = 0; Terms = 90;AsOf=DateOnly.FromDateTime(DateTime.Today);
        foreach (var data in saved.Rows)
        {
            if (data.ContainsKey("_metadata"))
            {
                Opening = decimal.Parse(data["opening"], CultureInfo.InvariantCulture);
                Terms = int.Parse(data["terms"], CultureInfo.InvariantCulture);
                if(data.TryGetValue("asOf",out var asOf)&&DateOnly.TryParseExact(asOf,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var reportDate))AsOf=reportDate;
                continue;
            }
            if(data.GetValueOrDefault("_blank")=="1")continue;
            var row = new GridRow(); foreach (var cell in data) row[cell.Key] = cell.Value;
            if(name=="form"&&row["_blank"]!="1"){if(row["_id"].Length==0)row["_id"]=ReportWorkflow.InvoiceKey(PartyId,data);if(row["_terms"].Length==0)row["_terms"]=Terms.ToString();}
            row["_autoDue"]=row["F19"];Rows.Add(row);
        }
        Calculate();
    }
    public GridRow Add(GridRow? draft=null)
    {
        var row = draft??new GridRow();
        if(Name=="form"){row["_id"]=Guid.NewGuid().ToString();row["_terms"]=Terms.ToString();}
        if(Name=="form"&&PartyId!=Guid.Empty&&string.IsNullOrWhiteSpace(row["F1"]))row["F1"]=PartyName;
        if (Name == "details") { if(string.IsNullOrEmpty(row["M"]))row["M"] = "90"; }
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
    public IReadOnlyList<string> Validate(bool requiredFields=true,bool publish=true)
    {
        if(Name=="form")foreach(var row in Records)if(row.Snapshot().ContainsKey("_autoDue")&&row["F19"]!=row["_autoDue"])row["_dueOverride"]=row["F19"];
        var errors = new List<string>();
        var money = Name switch { "form" => new[] { "F5","F6","F7","F8","F22","F23","F25" }, "details" => ["C","D","E","J"], _ => ["C","D","H"] };
        var dates = Name switch { "form" => new[] { "F4","F10","F13","F14","F17","F19","F24","F26" }, "details" => ["L"], _ => ["I","G"] };
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];ActivateBlank(row);if(row["_blank"]=="1"){if(publish)row.SetIssues([]);continue;} var issues = new List<CellIssue>();
            var required = Name == "form" ? (row["_newInvoice"]=="1"?new[]{"F1","F2","F27","F3","F4"}:new[] {"F1","F9"}) : new[] {"B"};
            foreach (var key in required) if (requiredFields && string.IsNullOrWhiteSpace(row[key])) issues.Add(new(key, $"{(Fields.FirstOrDefault(f=>f.Key==key)?.Label ?? key)} is required."));
            if(requiredFields&&Name=="form"&&PartyId!=Guid.Empty&&!string.IsNullOrWhiteSpace(row["F1"])&&!string.Equals(row["F1"].Trim(),PartyName,StringComparison.OrdinalIgnoreCase))issues.Add(new("F1","Party name must match the selected party. Switch the party selector to enter other parties."));
            foreach (var key in money)
            {
                if (string.IsNullOrWhiteSpace(row[key])) continue;
                if (!decimal.TryParse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount < 0 || decimal.Round(amount,2) != amount || amount > 1_000_000_000_000m)
                    issues.Add(new(key, $"{(Fields.FirstOrDefault(f=>f.Key==key)?.Label ?? key)}: enter non-negative USD with at most two decimals (maximum 1 trillion)."));
            }
            foreach (var key in dates) if (!string.IsNullOrWhiteSpace(row[key]) && !DateOnly.TryParseExact(row[key], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) issues.Add(new(key, $"{(Fields.FirstOrDefault(f=>f.Key==key)?.Label ?? key)}: use yyyy-MM-dd; leave unknown dates blank."));
            var term = Name == "details" ? "M" : Name == "ledger" ? "J" : "";
            if (term != "" && (!int.TryParse(row[term],out var days) || days < 0 || days > 3650)) issues.Add(new(term,"Terms must be 0–3650 calendar days."));
            var blKey=Name=="form"?"F17":Name=="details"?"L":"I";
            if(DateOnly.TryParseExact(row[blKey],"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var bl))
            {
                var termDays=term==""?90:int.TryParse(row[term],out var entered)?entered:90;
                try{DueDates.Calculate(bl,termDays);}catch(BusinessRuleException ex){issues.Add(new(blKey,ex.Message));}
            }
            if(requiredFields&&Name=="form")issues.AddRange(InvoiceRules.Validate(row.Snapshot()).Select(issue=>new CellIssue(issue.Field,issue.Message)));
            if(publish)row.SetIssues(issues); errors.AddRange(issues.Select(e=>$"Row {i+1}: {e.Message}"));
        }
        if (Terms is < 0 or > 3650) errors.Add("Default terms must be 0–3650 days.");
        return errors;
    }
    public void Calculate()
    {
        Validate(false);
        decimal running = Opening;
        var workflow=Name=="form"?new ReportWorkflow(store):null;
        bool runningKnown=true;
        for (int i=0; i<Rows.Count; i++)
        {
            var row = Rows[i];
            if(row["_blank"]=="1")continue;
            if(row.HasErrors)
            {
                foreach(var field in Fields.Where(f=>f.Calculated))row[field.Key]="";
                runningKnown=false;continue;
            }
            if (Name == "details")
            {
                row["A"]=(i+1).ToString();
                var hasContract=!string.IsNullOrWhiteSpace(row["D"]);
                var hasOriginal=!string.IsNullOrWhiteSpace(row["C"]);
                if(hasContract)Set(row,"F",Number(row,"D")-Number(row,"E"));else row["F"]="";
                if(hasOriginal&&hasContract)Set(row,"G",Number(row,"C")-Number(row,"D"));else row["G"]="";
                if(hasOriginal&&hasContract)Set(row,"I",Number(row,"C")-Number(row,"D"));else{row["I"]="";runningKnown=false;}
                if(runningKnown){running += Number(row,"I")-Number(row,"J");Set(row,"K",running);}else row["K"]="";
                Due(row,"L","M","N");
            }
            else if (Name == "ledger") { if(runningKnown){running+=Number(row,"C")-Number(row,"D");Set(row,"E",running);}else row["E"]=""; Due(row,"I","J","K");row["G"]=row["K"]; }
            else
            {
                var difference=workflow!.RegisterDifference(row.Snapshot());if(difference is {} calculated)Set(row,"F8",calculated);else row["F8"]="";
                row["F19"]=RegisterInvoiceSchema.DueDate(row.Snapshot())?.ToString("yyyy-MM-dd")??"";row["_autoDue"]=row["F19"];
                var due = Date(row,"F19");
                row["F20"] = due is {} d && d.DayNumber >= 30 ? d.AddDays(-30).ToString("yyyy-MM-dd") : "";
                row["F21"] = due is {} deadline ? (new ReportWorkflow(store).SettlementAwareOverdue(PartyId,row.Snapshot(),AsOf)??Math.Max(0,AsOf.DayNumber-deadline.DayNumber)).ToString() : "";
            }
        }
    }
    public void Save()
    {
        var errors = Validate(); if (errors.Count > 0) throw new BusinessRuleException(string.Join("\n",errors.Take(8)));
        if(Name=="form"&&Records.Where(r=>r["F9"].Trim().Length>0).GroupBy(r=>r["F9"].Trim(),StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new BusinessRuleException("Duplicate invoice references. Include financial year/container suffix when distinct.");
        Calculate();
        var data = new List<Dictionary<string,string>> { new() { ["_metadata"]="1", ["opening"]=Opening.ToString(CultureInfo.InvariantCulture), ["terms"]=Terms.ToString(CultureInfo.InvariantCulture),["asOf"]=AsOf.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) } };
        data.AddRange(Rows.Select(row => {if(row["_blank"]=="1")return new Dictionary<string,string>{{"_blank","1"}};var data=Fields.ToDictionary(f=>f.Key,f=>row[f.Key]);if(Name=="form"){data["_id"]=row["_id"];data["_terms"]=row["_terms"];PreserveRevertedMetadata(row,data);}return data;}));
        Revision = store.Save(Name,Revision,data,PartyId);
    }
    public bool DeleteImmediately(GridRow row)
    {
        var index=Rows.IndexOf(row);if(index<0)throw new BusinessRuleException("Select a stored record or draft row first.");
        Revision=store.DeleteWorksheetRecord(Name,PartyId,Revision,index,Name=="form"?ReportWorkflow.InvoiceKey(PartyId,row.Snapshot()):null);
        Rows.RemoveAt(index);Calculate();
        return HasSavedDifferences();
    }
    internal void ActivateBlank(GridRow row)
    {
        if(row["_blank"]!="1"||!Fields.Any(f=>!f.Calculated&&!string.IsNullOrWhiteSpace(row[f.Key])))return;
        row["_blank"]="";
        if(Name=="form")
        {
            row["_id"]=Guid.NewGuid().ToString();row["_terms"]=Terms.ToString();
            if(PartyId!=Guid.Empty)row["F1"]=PartyName;
        }
        else if(Name=="details"){row["M"]="90";}
        else row["J"]="90";
    }
    internal WorksheetEditState CaptureEditState()
    {
        var keys=Fields.Where(f=>!f.Calculated).Select(f=>f.Key).Concat(Name=="form"?new[]{"_id","_terms"}:Array.Empty<string>()).Append("_blank").ToArray();
        return new(Rows.Select(row=>{var data=keys.ToDictionary(key=>key,key=>row[key]);if(Name=="form")PreserveRevertedMetadata(row,data);return data;}).ToList(),Opening,Terms);
    }
    private static void PreserveRevertedMetadata(GridRow row,Dictionary<string,string> data)
    {
        var snapshot=row.Snapshot();foreach(var key in new[]{"F22","F23","F24","F25","F26","F27","_agentId","_dueOverride","_newInvoice"})if(snapshot.TryGetValue(key,out var value))data[key]=value;
    }
    internal void RestoreEditState(WorksheetEditState state,int revision)
    {
        Rows.Clear();Opening=state.Opening;Terms=state.Terms;Revision=revision;
        foreach(var data in state.Rows){var row=new GridRow();foreach(var cell in data)row[cell.Key]=cell.Value;row["_autoDue"]=row["F19"];Rows.Add(row);}
        Calculate();
    }
    internal bool HasSavedDifferences()
    {
        var saved=new WorksheetController(store);saved.Load(Name,PartyId);
        var current=CaptureEditState();var stored=saved.CaptureEditState();
        while(current.Rows.Count>stored.Rows.Count&&current.Rows[^1].GetValueOrDefault("_blank")=="1")current.Rows.RemoveAt(current.Rows.Count-1);
        return current.Signature!=stored.Signature;
    }
}
