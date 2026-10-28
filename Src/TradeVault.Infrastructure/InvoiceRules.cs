using System.Globalization;

namespace TradeVault.Infrastructure;

public sealed record InvoiceIssue(string Field,string Message);

/// <summary>Rules shared by worksheet Save and the database boundary; incomplete drafts remain editable.</summary>
public static class InvoiceRules
{
    public static List<InvoiceIssue> Validate(IReadOnlyDictionary<string,string> row)
    {
        var issues=new List<InvoiceIssue>();
        string Value(string key)=>row.GetValueOrDefault(key,"").Trim();
        DateOnly? Date(string key)=>DateOnly.TryParseExact(Value(key),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:null;
        decimal Amount(string key)=>decimal.TryParse(Value(key),NumberStyles.Number,CultureInfo.InvariantCulture,out var amount)?amount:0;
        void Order(string earlier,string later,string message){if(Date(earlier) is {} first&&Date(later) is {} last&&last<first)issues.Add(new(later,message));}
        foreach(var key in new[]{"F4","F10","F13","F14","F17","F24","F26"})
            if(Value(key).Length>0&&Date(key)==null)issues.Add(new(key,"Enter a complete valid date (DD-MM-YYYY)."));
        foreach(var key in new[]{"F5","F6","F7","F18","F22","F23","F25"})
            if(Value(key).Length>0&&(!decimal.TryParse(Value(key),NumberStyles.Number,CultureInfo.InvariantCulture,out var amount)||amount<0||amount>1_000_000_000_000m||decimal.Round(amount,2)!=amount))issues.Add(new(key,"Enter non-negative USD with at most two decimal places (maximum 1 trillion)."));
        if(Value("_terms").Length>0&&(!int.TryParse(Value("_terms"),out var terms)||terms<0||terms>3650))issues.Add(new("_terms","Payment terms must be 0–3650 calendar days."));
        Order("F4","F10","Invoice date cannot precede the proforma invoice date.");
        Order("F10","F13","India BL date cannot precede the invoice date.");
        Order("F13","F14","Malaysia arrival date cannot precede the India BL date.");
        Order("F14","F17","Malaysia BL date cannot precede Malaysia arrival.");
        Order("F10","F17","Malaysia BL date cannot precede the invoice date.");
        Order("F13","F17","Malaysia BL date cannot precede the India BL date.");
        foreach(var (amounts,dateKey) in new[]{(new[]{"F22","F23"},"F24"),(new[]{"F25"},"F26")})
        {
            if(amounts.Any(key=>Amount(key)>0))
            {
                if(Date("F10")==null)issues.Add(new("F10","Invoice date is required before recording invoice payments. Use Receipts for an unallocated advance."));
                if(Date(dateKey)==null)issues.Add(new(dateKey,"Payment date is required for a received amount."));
            }
            Order("F10",dateKey,"Payment date cannot precede the invoice date. Record pre-invoice money as an unallocated advance in Receipts.");
        }
        if(Date("F17") is {} bl)
        {
            var days=int.TryParse(Value("_terms"),out var termDays)?termDays:90;
            if(days>=0&&days<=3650&&bl.DayNumber>DateOnly.MaxValue.DayNumber-days)issues.Add(new("F17","BL date plus payment terms exceeds the supported date range."));
        }
        return issues;
    }
}
