using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TradeVault.Application;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

/// <summary>Register is the source; legacy independently entered reports are never overwritten.</summary>
public sealed partial class ReportWorkflow(WorkbookStore store,LoginSession session)
{
    private static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
    public string Root=>store.Database.Paths.Root;
    public ReportMapping? Mapping(Guid party)=>store.Document<ReportMapping>(session,"mapping",party.ToString());
    public decimal? RegisterDifference(Guid party,Dictionary<string,string> row)
    {
        var mapping=Mapping(party);
        if(mapping==null||string.IsNullOrWhiteSpace(row.GetValueOrDefault("F5"))||string.IsNullOrWhiteSpace(row.GetValueOrDefault(mapping.ContractField)))return null;
        return Amount(row,"F5",true)-Amount(row,mapping.ContractField,true);
    }
    public void Configure(Guid party,ReportMapping mapping)
    {
        Party(party);if(party==Guid.Empty)throw new BusinessRuleException("Assign records to a named party before enabling generated reports.");
        if(mapping.ContractField is not("F6" or "F7")||mapping.BankReceivedField is not("F22" or "F25"))throw new BusinessRuleException("Choose explicit amount sources.");
        Money(mapping.OpeningBank);Money(mapping.OpeningDifference);
        if(Receipts(party).Count>0&&Mapping(party)!=mapping)throw new BusinessRuleException("Mappings cannot change after receipts are recorded. Reverse/migrate them explicitly first.");
        store.CreateBackup(session);store.Mutate<ReportMapping>(session,"mapping",party.ToString(),_=>mapping);
    }
    private WorksheetParty Party(Guid id)=>store.GetParties(session).SingleOrDefault(p=>p.Id==id)??throw new BusinessRuleException("Party not found.");
    internal static decimal Money(decimal value){Usd.FromDecimal(value);return value;}
    private static decimal Amount(Dictionary<string,string> row,string key,bool required=false)
    {
        var text=row.GetValueOrDefault(key,"");if(string.IsNullOrWhiteSpace(text)){if(required)throw new BusinessRuleException($"{key}: amount is missing.");return 0;}
        if(!decimal.TryParse(text,NumberStyles.Number,Culture,out var value)||value<0||value>1_000_000_000_000m)throw new BusinessRuleException($"{key}: invalid USD amount.");return Money(value);
    }
    private static DateOnly? Date(Dictionary<string,string> row,string key,bool required=false)
    {
        var text=row.GetValueOrDefault(key,"");if(string.IsNullOrWhiteSpace(text)){if(required)throw new BusinessRuleException($"{key}: an effective date is required for historical reporting.");return null;}
        if(!DateOnly.TryParseExact(text,"yyyy-MM-dd",Culture,DateTimeStyles.None,out var date))throw new BusinessRuleException($"{key}: invalid date.");return date;
    }
    public static string InvoiceKey(Guid party,Dictionary<string,string> row)=>row.GetValueOrDefault("_id")??Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(party+"|"+row.GetValueOrDefault("F9","").Trim().ToUpperInvariant())));
    public List<Dictionary<string,string>> Invoices(Guid party)=>store.Load(session,"form",party).Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
    public List<PartyReceipt> Receipts(Guid party)=>store.Document<ReceiptBook>(session,"receipts",party.ToString())?.Receipts??[];
    public void EnsureInvoiceCanDelete(Guid party,Dictionary<string,string> row)
    {
        Party(party);var key=InvoiceKey(party,row);
        if(Receipts(party).Any(r=>r.Allocations.Any(a=>a.InvoiceId==key)))throw new BusinessRuleException("This invoice has payment history and cannot be deleted. Keep it for the account history; use the payment correction workflow when needed.");
    }
    public ReportCoverage Coverage(Guid party,DateOnly asOf)
    {
        var rows=Invoices(party);var dates=rows.Select(r=>DateOnly.TryParseExact(r.GetValueOrDefault("F10",""),"yyyy-MM-dd",Culture,DateTimeStyles.None,out var date)?(DateOnly?)date:null).ToList();
        return new(rows.Count,dates.Count(d=>d<=asOf),dates.Count(d=>d>asOf),dates.Count(d=>d==null),dates.Where(d=>d!=null).Max());
    }
    public void SetReportDate(Guid party,DateOnly date)
    {
        Party(party);var snapshot=store.Load(session,"form",party);var metadata=snapshot.Rows.SingleOrDefault(r=>r.ContainsKey("_metadata"));
        if(metadata==null){metadata=new(){["_metadata"]="1",["opening"]="0",["terms"]="90"};snapshot.Rows.Insert(0,metadata);}
        metadata["asOf"]=date.ToString("yyyy-MM-dd",Culture);store.Save(session,"form",snapshot.Revision,snapshot.Rows,party);
    }
    public List<InvoicePaymentBalance> PaymentBalances(Guid party,DateOnly date,Guid? replacing=null)
    {
        var receipts=Receipts(party);
        if(replacing is {} id)
        {
            var original=receipts.SingleOrDefault(r=>r.Id==id)??throw new BusinessRuleException("Choose a recorded payment.");
            if(original.Reverses!=null||receipts.Any(r=>r.Reverses==id)||date<original.Date)throw new BusinessRuleException("Choose an active payment and a correction date on or after its original date.");
            receipts=receipts.Where(r=>r.Id!=id).ToList();
        }
        var table=BuildGenerated(party,date,receipts,false).Tables[0];var invoices=Invoices(party);
        return table.Rows.Select(r=>new InvoicePaymentBalance(InvoiceKey(party,invoices.Single(i=>i.GetValueOrDefault("F9")==r["B"])),r["B"],string.IsNullOrEmpty(r["N"])?null:DateOnly.ParseExact(r["N"],"yyyy-MM-dd",Culture),decimal.Parse(r["F"],Culture),decimal.Parse(r["I"],Culture)-decimal.Parse(r["J"],Culture))).ToList();
    }
    public static decimal RemainingAfterPayment(InvoicePaymentBalance invoice,decimal bank,decimal difference)
    {
        Money(bank);Money(difference);if(bank<0||difference<0)throw new BusinessRuleException("Payment assignments cannot be negative.");
        if(bank>invoice.BankUnpaid||difference>invoice.DifferenceUnpaid)throw new BusinessRuleException("Payment exceeds the unpaid bank or difference amount for "+invoice.Invoice+".");
        return invoice.TotalUnpaid-bank-difference;
    }
    public PartyReceipt RecordReceipt(Guid party,DateOnly date,decimal amount,string reference,List<ReceiptSplit> splits)
    {
        Party(party);if(Mapping(party)==null)throw new BusinessRuleException("Configure generated reports first.");
        if(Money(amount)<=0||splits.Any(s=>s.Amount<=0||s.Component is not("Bank" or "Difference"))||splits.GroupBy(s=>(s.InvoiceId,s.Component)).Any(g=>g.Count()>1))throw new BusinessRuleException("Use positive amounts and unique invoice/component allocations.");
        foreach(var split in splits)Money(split.Amount);
        if(splits.Sum(s=>s.Amount)>amount||reference.Length>200)throw new BusinessRuleException("Allocations exceed the receipt or reference is too long.");
        var invoices=Invoices(party);foreach(var split in splits){var row=invoices.SingleOrDefault(r=>InvoiceKey(party,r)==split.InvoiceId)??throw new BusinessRuleException("Allocated invoice is not in this party.");if(Date(row,"F10",true)>date)throw new BusinessRuleException("Receipt predates invoice. Record an unallocated advance instead.");}
        var receipt=new PartyReceipt(Guid.NewGuid(),date,amount,reference.Trim(),splits);
        store.Mutate<ReceiptBook>(session,"receipts",party.ToString(),book=>{
            var next=new List<PartyReceipt>(book?.Receipts??[]){receipt};BuildGenerated(party,DateOnly.MaxValue,next,true);return new(next);
        });return receipt;
    }
    public void ReverseReceipt(Guid party,Guid id,DateOnly date,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>200)throw new BusinessRuleException("Enter a correction reason (up to 200 characters).");
        store.Mutate<ReceiptBook>(session,"receipts",party.ToString(),book=>{
            var list=new List<PartyReceipt>(book?.Receipts??[]);var original=list.SingleOrDefault(r=>r.Id==id)??throw new BusinessRuleException("Receipt not found.");
            if(original.Reverses!=null||date<original.Date||list.Any(r=>r.Reverses==id))throw new BusinessRuleException("Receipt is already reversed, is itself a reversal, or reversal date is too early.");
            list.Add(new(Guid.NewGuid(),date,-original.Amount,reason,original.Allocations.Select(a=>a with{Amount=-a.Amount}).ToList(),id));return new(list);
        });
    }
    public void CorrectReceipt(Guid party,Guid id,DateOnly effectiveDate,decimal amount,string reference,List<ReceiptSplit> splits)
    {
        Party(party);if(Money(amount)<=0||reference.Length>200||splits.Any(a=>a.Amount<=0||a.Component is not("Bank" or "Difference"))||splits.Sum(a=>a.Amount)>amount||splits.GroupBy(a=>(a.InvoiceId,a.Component)).Any(g=>g.Count()>1))throw new BusinessRuleException("Invalid correction amounts or duplicate allocations.");
        var invoices=Invoices(party);foreach(var allocation in splits){Money(allocation.Amount);var row=invoices.SingleOrDefault(r=>InvoiceKey(party,r)==allocation.InvoiceId)??throw new BusinessRuleException("Invoice not in party.");if(Date(row,"F10",true)>effectiveDate)throw new BusinessRuleException("Correction allocation predates invoice.");}
        store.Mutate<ReceiptBook>(session,"receipts",party.ToString(),book=>{
            var list=new List<PartyReceipt>(book?.Receipts??[]);var original=list.SingleOrDefault(r=>r.Id==id)??throw new BusinessRuleException("Receipt not found.");
            if(original.Reverses!=null||list.Any(r=>r.Reverses==id)||effectiveDate<original.Date)throw new BusinessRuleException("Receipt already reversed or correction date is too early.");
            list.Add(new(Guid.NewGuid(),effectiveDate,-original.Amount,"Correction reversal: "+reference,original.Allocations.Select(a=>a with{Amount=-a.Amount}).ToList(),id));
            list.Add(new(Guid.NewGuid(),effectiveDate,amount,"Correction: "+reference,splits));BuildGenerated(party,DateOnly.MaxValue,list,true);return new(list);
        });
    }
    public int? SettlementAwareOverdue(Guid party,Dictionary<string,string> row,DateOnly asOf)
    {
        var mapping=Mapping(party);if(mapping==null||!row.ContainsKey("F5")||!row.ContainsKey(mapping.ContractField))return null;
        var due=DueDates.Calculate(Date(row,"F17"),int.TryParse(row.GetValueOrDefault("_terms"),out var terms)?terms:90);if(due==null)return 0;
        var bank=Amount(row,mapping.BankReceivedField);var diff=Amount(row,"F23");
        var bankDate=bank==0?(DateOnly?)null:Date(row,mapping.BankReceivedField=="F25"?"F26":"F24");var diffDate=diff==0?(DateOnly?)null:Date(row,"F24");
        if(bank>0&&bankDate==null||diff>0&&diffDate==null)return null;
        var key=InvoiceKey(party,row);var allocated=Receipts(party).Where(r=>r.Date<=asOf).SelectMany(r=>r.Allocations.Where(a=>a.InvoiceId==key)).Sum(a=>a.Amount);
        var outstanding=Amount(row,"F5",true)-(mapping.DeductRegisteredShipping?Amount(row,"F18",true):0)-(bankDate<=asOf?bank:0)-(diffDate<=asOf?diff:0)-allocated;
        return outstanding>0?Math.Max(0,asOf.DayNumber-due.Value.DayNumber):0;
    }
    public ReportBundle Build(Guid party,DateOnly asOf,bool generated)
    {
        Party(party);return generated?BuildGenerated(party,asOf,Receipts(party),false):BuildLegacy(party,asOf);
    }
    public ReportBundle BuildCurrent(Guid party,bool generated=true)
    {
        Party(party);var today=DateOnly.FromDateTime(DateTime.Today);
        if(!generated)return BuildLegacy(party,today);
        var all=BuildGenerated(party,DateOnly.MaxValue,Receipts(party),false);
        return Bundle(party,today,"All saved records",all.Tables,["Includes all saved invoices and recorded payments, including future-dated entries. Prepared on "+today.ToString("yyyy-MM-dd",Culture)+"; no manual reporting-date filter.",..all.Notes]);
    }
    private ReportBundle Bundle(Guid party,DateOnly asOf,string mode,List<ReportTable> tables,List<string> notes)
    {
        using var c=store.Database.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT id FROM app_instance LIMIT 1";var source=Guid.Parse((string)cmd.ExecuteScalar()!);
        var submission=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{source}|{party}|{asOf:yyyy-MM-dd}|{mode}"))[..16]);
        return new(1,"register-v1",source,party,Party(party).Name,asOf,DateTimeOffset.UtcNow,submission,0,mode,tables,notes);
    }
    public List<ReminderItem> Reminders(Guid party,DateOnly asOf)
    {
        var report=Build(party,asOf,true);return report.Tables[0].Rows.Select(r=>{
            var due=string.IsNullOrEmpty(r.GetValueOrDefault("N"))?(DateOnly?)null:DateOnly.ParseExact(r["N"],"yyyy-MM-dd",Culture);
            var outstanding=decimal.Parse(r["F"],Culture)+decimal.Parse(r["I"],Culture)-decimal.Parse(r["J"],Culture);
            var overdue=outstanding>0&&due is {} d?Math.Max(0,asOf.DayNumber-d.DayNumber):0;
            return new ReminderItem(r["B"],due,outstanding,overdue,outstanding<=0?"Settled":due==null?"BL pending":overdue>0?"Overdue":due.Value.DayNumber-asOf.DayNumber<=30?"Due within 30 days":"Not yet due");
        }).ToList();
    }
    private ReportBundle BuildGenerated(Guid party,DateOnly asOf,List<PartyReceipt> receipts,bool validationOnly)
    {
        var mapping=Mapping(party)??throw new BusinessRuleException("Configure amount sources before generating reports.");
        var rows=Invoices(party);var duplicates=rows.GroupBy(r=>r.GetValueOrDefault("F9","").Trim(),StringComparer.OrdinalIgnoreCase).FirstOrDefault(g=>g.Key.Length==0||g.Count()>1);
        if(duplicates!=null)throw new BusinessRuleException("Register has missing or duplicate invoice numbers. Use a unique invoice/container reference.");
        var details=new List<Dictionary<string,string>>();var entries=new List<(DateOnly Date,string Key,string Text,decimal Debit,decimal Credit)>();
        var notes=new List<string>{"Generated from saved invoice register and dated receipt allocations. Independent legacy worksheets are preserved.","Contract source: "+mapping.ContractField+"; baseline receipt source: "+mapping.BankReceivedField+". Shipping deductions "+(mapping.DeductRegisteredShipping?"explicitly approved":"excluded")+".","Historical results use current invoice values, filtered by effective dates. Corrected invoice versions are not reconstructed.","Existing cumulative receipts are treated as single baseline movements on their recorded dates; earlier payment history is not invented."};
        decimal runningDifference=asOf>=mapping.OpeningDate?mapping.OpeningDifference:0;
        if(asOf>=mapping.OpeningDate){var opening=mapping.OpeningBank+mapping.OpeningDifference;entries.Add((mapping.OpeningDate,"0","Opening balance",Math.Max(0,opening),Math.Max(0,-opening)));}
        foreach(var row in rows.OrderBy(r=>r.GetValueOrDefault("F10","")).ThenBy(r=>r.GetValueOrDefault("F9",""),StringComparer.Ordinal))
        {
            var key=InvoiceKey(party,row);var invoice=row.GetValueOrDefault("F9","");var date=Date(row,"F10",true)!.Value;
            var original=Amount(row,"F5",true);var contract=Amount(row,mapping.ContractField,true);var shipping=mapping.DeductRegisteredShipping?Amount(row,"F18",true):0;
            if(original-contract-shipping<0)throw new BusinessRuleException(invoice+": difference after approved shipping is negative. Review the mapping.");
            var baselineBank=Amount(row,mapping.BankReceivedField);var baselineDiff=Amount(row,"F23");
            var bankDate=baselineBank==0?(DateOnly?)null:Date(row,mapping.BankReceivedField=="F25"?"F26":"F24",true);
            var diffDate=baselineDiff==0?(DateOnly?)null:Date(row,"F24",true);
            if(bankDate<date||diffDate<date)throw new BusinessRuleException(invoice+": baseline receipt predates invoice.");
            var allocations=receipts.SelectMany(r=>r.Allocations.Where(a=>a.InvoiceId==key).Select(a=>(r.Date,a.Component,a.Amount))).ToList();
            var allBank=baselineBank+allocations.Where(a=>a.Component=="Bank").Sum(a=>a.Amount);var allDiff=baselineDiff+allocations.Where(a=>a.Component=="Difference").Sum(a=>a.Amount);
            if(allBank>contract||allDiff>original-contract-shipping||allBank<0||allDiff<0)throw new BusinessRuleException(invoice+": receipts exceed an outstanding component.");
            // Validate every effective-date checkpoint, not only the final balance after future reversals.
            foreach(var component in new[]{"Bank","Difference"})
            {
                var movements=allocations.Where(a=>a.Component==component).Select(a=>(a.Date,a.Amount)).ToList();
                var baseline=component=="Bank"?baselineBank:baselineDiff;var effective=component=="Bank"?bankDate:diffDate;
                if(effective is {} baselineDate)movements.Add((baselineDate,baseline));
                decimal paid=0;var ceiling=component=="Bank"?contract:original-contract-shipping;
                foreach(var day in movements.GroupBy(a=>a.Date).OrderBy(g=>g.Key))
                {paid+=day.Sum(a=>a.Amount);if(paid<0||paid>ceiling)throw new BusinessRuleException($"{invoice}: {component} receipts exceed the component balance on {day.Key:dd-MM-yyyy}.");}
            }
            if(date>asOf)continue;
            var bank=(bankDate<=asOf?baselineBank:0)+allocations.Where(a=>a.Date<=asOf&&a.Component=="Bank").Sum(a=>a.Amount);
            var diff=(diffDate<=asOf?baselineDiff:0)+allocations.Where(a=>a.Date<=asOf&&a.Component=="Difference").Sum(a=>a.Amount);
            var terms=int.TryParse(row.GetValueOrDefault("_terms","90"),out var days)?days:90;var due=DueDates.Calculate(Date(row,"F17"),terms);
            runningDifference+=original-contract-shipping-diff;
            details.Add(new(){["A"]=(details.Count+1).ToString(),["B"]=invoice,["C"]=F(original),["D"]=F(contract),["E"]=F(bank),["F"]=F(contract-bank),["G"]=F(original-contract),["H"]=F(shipping),["I"]=F(original-contract-shipping),["J"]=F(diff),["K"]=F(runningDifference),["L"]=row.GetValueOrDefault("F17",""),["M"]=terms.ToString(),["N"]=due?.ToString("yyyy-MM-dd")??""});
            entries.Add((date,key+"1",invoice,original,0));if(shipping!=0)entries.Add((date,key+"2","Approved shipping / "+invoice,0,shipping));
            if(bankDate<=asOf)entries.Add((bankDate!.Value,key+"3","Baseline bank receipt / "+invoice,0,baselineBank));
            if(diffDate<=asOf)entries.Add((diffDate!.Value,key+"4","Baseline difference receipt / "+invoice,0,baselineDiff));
            foreach(var allocation in allocations.Where(a=>a.Date<=asOf))entries.Add((allocation.Date,key+"5"+allocation.Component,allocation.Component+" receipt / "+invoice,0,allocation.Amount));
        }
        var knownKeys=rows.Select(r=>InvoiceKey(party,r)).ToHashSet();if(receipts.SelectMany(r=>r.Allocations).Any(a=>!knownKeys.Contains(a.InvoiceId)))throw new BusinessRuleException("A receipt references a missing invoice. Restore the invoice or reverse the receipt first.");
        foreach(var receipt in receipts.Where(r=>r.Date<=asOf)){var remainder=receipt.Amount-receipt.Allocations.Sum(a=>a.Amount);if(remainder!=0)entries.Add((receipt.Date,receipt.Id+"advance","Unallocated advance / "+receipt.Reference,0,remainder));}
        var ledger=new List<Dictionary<string,string>>();decimal balance=0;
        foreach(var entry in entries.OrderBy(e=>e.Date).ThenBy(e=>e.Key,StringComparer.Ordinal)){balance+=entry.Debit-entry.Credit;ledger.Add(new(){["B"]=entry.Date.ToString("yyyy-MM-dd")+" · "+entry.Text,["C"]=F(entry.Debit),["D"]=F(entry.Credit),["E"]=F(balance),["G"]="",["H"]="",["I"]="",["J"]="",["K"]=""});}
        if(asOf<mapping.OpeningDate)notes.Add("Opening balances are excluded before their effective date.");
        var unallocated=receipts.Where(r=>r.Date<=asOf).Sum(r=>r.Amount-r.Allocations.Sum(a=>a.Amount));notes.Add($"Unallocated advances USD {F(unallocated)} are credited in the party ledger but do not settle a specific invoice. Closing party balance USD {F(balance)}.");
        notes.Add($"Opening bank USD {F(mapping.OpeningBank)} and opening difference USD {F(mapping.OpeningDifference)} effective {mapping.OpeningDate:yyyy-MM-dd}; opening balances are not invoice-aged.");
        var detailTable=Table("Account Details",details);detailTable.Totals["K"]=runningDifference;
        return Bundle(party,asOf,"Generated",[detailTable,Table("Debit Credit",ledger)],notes);
    }
    private ReportBundle BuildLegacy(Guid party,DateOnly asOf)
    {
        var tables=new List<ReportTable>();foreach(var name in new[]{"details","ledger"})tables.Add(Table(name=="details"?"Account Details":"Debit Credit",store.Load(session,name,party).Rows.Where(r=>!r.ContainsKey("_metadata")).ToList()));
        return Bundle(party,asOf,"Manual snapshot",tables,["Saved independently entered worksheet values. Reporting date is a label, not a historical transaction filter. No automatic reconciliation claimed."]);
    }
    private static string F(decimal value)=>value.ToString("0.00",Culture);
    public static List<ReportColumn> Columns(string name)
    {
        var details=name=="Account Details";
        var keys=details?new[]{"A","B","C","D","E","F","G","H","I","J","K","L","M","N"}:new[]{"B","C","D","E","G","H","I","J","K"};
        var labels=details?new[]{"SR. NO.","OUR CUSTOMS INVOICE NO & DATE","ORIGINAL AMOUNT $","BANK CONTRACT AMOUNT $","REC. INVOICE  PAYMENT IN MALAYSIA A/C","PENDING INVOICE PAYMENT IN MALAYSIA A/C","DIFFRENCE AMAOUNT $","SHIPMENT PAYMENT $","PER BILL DIFF. AFTER SHIPMENT PAYMENT  USD","REC. DIFF. AMOUNT","TOTAL DIFF. AMOUNT PENDING $","BL DATE. MLY TO PAK","TERMS","DUE DATE"}:new[]{"Transaction description","Amount added / USD (Debit)","Amount deducted / USD (Credit)","Running amount owed / USD","Payment due date","Bank amount / USD","Bill of lading date","Payment terms / days","Payment due date"};
        return keys.Select((k,i)=>new ReportColumn(k,labels[i],(details?k is "L" or "N":k is "G" or "I" or "K")?"date":(details?k is not("B"):k is "C" or "D" or "E" or "H" or "J")?"number":"text")).ToList();
    }
    private static ReportTable Table(string name,List<Dictionary<string,string>> rows)
    {
        var totals=new Dictionary<string,decimal>();foreach(var key in name=="Account Details"?new[]{"C","D","E","F","G","H","I","J"}:new[]{"C","D"})
        {decimal sum=0;foreach(var row in rows){var text=row.GetValueOrDefault(key,"");if(text.Length>0){if(!decimal.TryParse(text,NumberStyles.Number,Culture,out var n))throw new BusinessRuleException("Invalid numeric report cell: "+key);sum+=n;}}totals[key]=sum;}
        var balanceKey=name=="Account Details"?"K":"E";if(rows.Count>0&&decimal.TryParse(rows.Last().GetValueOrDefault(balanceKey),NumberStyles.Number,Culture,out var balance))totals[balanceKey]=balance;
        return new(name,Columns(name),rows,totals);
    }
}
