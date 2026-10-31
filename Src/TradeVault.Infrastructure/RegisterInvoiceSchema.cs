using System.Globalization;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed record NewRegisterInvoice(string CountryName,string AgentName,string ProformaInvoiceNumber,DateOnly ProformaInvoiceDate,string? AgentId=null);

/// <summary>Stable worksheet keys keep existing records and receipt allocations compatible.</summary>
public static class RegisterInvoiceSchema
{
    public static readonly string[] VisibleKeys=["F1","F27","F3","F4","F2","F5","F6","F7","F8","F9","F10","F11","F12","F13","F14","F15","F16","F17","F19","F20","F21"];
    public static readonly string[] IdentityKeys=["F1","F27","F3","F4","F2"];
    public static DateOnly? DueDate(IReadOnlyDictionary<string,string> row)
    {
        var custom=row.GetValueOrDefault("_dueOverride","");
        if(custom.Length>0)
        {
            if(!DateOnly.TryParseExact(custom,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var due))throw new BusinessRuleException("Payment due date must be a complete valid date.");
            return due;
        }
        return DueDates.Calculate(DateOnly.TryParseExact(row.GetValueOrDefault("F17"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var bl)?bl:null,90);
    }
    internal static string Required(string value,string label,int maximum)
    {
        value=(value??"").Trim();
        if(value.Length==0||value.Length>maximum)throw new BusinessRuleException($"{label} is required (maximum {maximum} characters).");
        return value;
    }
}

public sealed partial class WorkbookStore
{
    public string CreateRegisterInvoice(Guid party,int expectedRevision,NewRegisterInvoice request)
    {
        var country=RegisterInvoiceSchema.Required(request.CountryName,"Country name",200);
        var agent=RequireAgent(RegisterInvoiceSchema.Required(request.AgentName,"Agent name",200),request.AgentId);
        var proforma=RegisterInvoiceSchema.Required(request.ProformaInvoiceNumber,"Proforma invoice number",100);
        var snapshot=Load("form",party);
        if(snapshot.Revision!=expectedRevision)throw new BusinessRuleException("Records changed. Reload before creating an invoice.");
        var id=Guid.NewGuid().ToString();
        var row=new Dictionary<string,string>{{"_id",id},{"_terms","90"},{"_dueOverride",""},{"_newInvoice","1"},{"F1",GetParties().Single(p=>p.Id==party).Name},{"F2",country},{"F27",agent.Name},{"_agentId",agent.Id},{"F3",proforma},{"F4",request.ProformaInvoiceDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)}};
        var blank=snapshot.Rows.FindIndex(row=>row.GetValueOrDefault("_blank")=="1");
        if(blank>=0)snapshot.Rows[blank]=row;else snapshot.Rows.Add(row);
        Save("form",snapshot.Revision,snapshot.Rows,party);return id;
    }
}
