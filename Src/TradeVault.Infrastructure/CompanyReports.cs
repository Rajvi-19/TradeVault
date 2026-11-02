using System.Globalization;
using TradeVault.Application;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public enum CompanyReportKind { AccountDetails, PartyLedger, AgentLedger }
public sealed record CompanyReport(ReportTable Table,string Subject,IReadOnlyList<string> Notes,IReadOnlyDictionary<string,string> Summary);
public sealed record MasterLedgerAmounts(decimal Received,decimal? Pending,decimal? OriginalOrPartyReceipts);

/// <summary>Read-only projections. Missing allocation/charge data is never converted to a zero receipt.</summary>
public sealed class CompanyReports(WorkbookStore store)
{
    public MasterLedgerAmounts MasterAmounts(bool agent,string masterId)
    {
        var rows=Build(agent?CompanyReportKind.AgentLedger:CompanyReportKind.PartyLedger,masterId).Table.Rows;
        var received=rows.Sum(r=>Number(r,agent?"G":"D")??0);
        var pending=rows.Count==0?0m:Number(rows.Last(),agent?"H":"E");
        decimal? total=agent?rows.Sum(r=>Number(r,"F")??0):pending.HasValue?rows.Sum(r=>Number(r,"C")??0):null;
        return new(received,pending,total);
    }
    public static string SummaryLabel(string key)=>key switch
    {
        "Invoices"=>"Number of invoices","Paid by party"=>"Party paid to agent","Expenses"=>"Total expenses","Agent received"=>"Agent received after expenses","Sent to company"=>"Agent sent to company","Pending with agent"=>"Money still with agent",
        "Unallocated receipts / advance"=>"Advance payment","Invoice debits"=>"Total invoice amount",
        "Party receipts"=>"Payments from party","Net recorded balance"=>"Amount still owed by party",
        "Party funds after expenses"=>"Party payments after expenses","Bank transfer charges"=>"Bank charges",
        "Net received by agent"=>"Net money received by agent","Agent remittances"=>"Money sent to company",_=>key
    };
    private static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
    private static ReportColumn C(string key,string label,string type="text")=>new(key,label,type);
    public static List<ReportColumn> Columns(CompanyReportKind kind)=>kind switch
    {
        CompanyReportKind.AccountDetails=>[C("A","SR. NO.","integer"),C("B","Our customs invoice no. & date"),C("C","Original amount / USD","number"),C("D","Bank contract amount / USD","number"),C("E","Received invoice payment in agent account / USD","number"),C("O","Received from agent to company / USD","number"),C("H","Expense / USD","number"),C("F","Pending invoice payment in agent account / USD","number"),C("L","Agent country BL date","date"),C("N","Due date","date")],
        CompanyReportKind.PartyLedger=>[C("B","Particulars"),C("C","Debit / USD","number"),C("D","Credit / USD","number"),C("G","Due","date"),C("I","BL date","date"),C("K","Due date","date")],
        CompanyReportKind.AgentLedger=>[C("B","Date","date"),C("C","Description of payment"),C("D","Party sent contract & difference / USD","number"),C("E","Bank transfer charges / USD","number"),C("F","Deposit agent account / USD","number"),C("G","Credit company account / USD","number"),C("H","Pending with agent / USD","number")],
        _=>throw new BusinessRuleException("Choose a report.")
    };
    private static string Money(decimal? value)=>value?.ToString("0.00",Culture)??"";
    private static decimal? Number(Dictionary<string,string> row,string key)=>decimal.TryParse(row.GetValueOrDefault(key),NumberStyles.Number,Culture,out var number)?number:null;
    private static DateOnly? Date(Dictionary<string,string> row,string key)=>DateOnly.TryParseExact(row.GetValueOrDefault(key),"yyyy-MM-dd",Culture,DateTimeStyles.None,out var day)?day:null;
    private List<Dictionary<string,string>> Invoices(Guid party)=>store.Load("form",party).Rows.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1").ToList();
    public CompanyReport Build(CompanyReportKind kind,string masterId)
    {
        if(kind==CompanyReportKind.AgentLedger)return AgentLedger(masterId);
        var party=store.GetParties().FirstOrDefault(p=>!p.IsUnassigned&&p.Id.ToString()==masterId)??throw new BusinessRuleException("Select a party from Party Master.");
        return kind==CompanyReportKind.AccountDetails?AccountDetails(party):kind==CompanyReportKind.PartyLedger?PartyLedger(party):throw new BusinessRuleException("Choose a report.");
    }
    private CompanyReport AccountDetails(WorksheetParty party)
    {
        var invoices=Invoices(party.Id).OrderBy(r=>Date(r,"F10")??Date(r,"F4")??DateOnly.MaxValue).ThenBy(r=>r.GetValueOrDefault("F3")).ToList();
        var rows=new List<Dictionary<string,string>>();var index=0;
        var paymentBook=store.GetTransferReceipts(TransferReceiptKind.Party).Where(r=>r.MasterId==party.Id.ToString()).ToList();
        var agentAllocations=store.GetTransferReceipts(TransferReceiptKind.Agent).SelectMany(r=>r.Allocations??[]).ToList();
        var legacy=new ReportWorkflow(store).Receipts(party.Id);
        foreach(var invoice in invoices)
        {
            var original=Number(invoice,"F5");var contract=Number(invoice,"F6");var key=ReportWorkflow.InvoiceKey(party.Id,invoice);var splits=paymentBook.SelectMany(r=>r.Allocations??[]).Where(a=>a.InvoiceId==key);var old=legacy.SelectMany(r=>r.Allocations).Where(a=>a.InvoiceId==key);var receivedBank=(Number(invoice,"F25")??0)+splits.Sum(a=>a.Bank)+old.Where(a=>a.Component=="Bank").Sum(a=>a.Amount);var receivedDifference=(Number(invoice,"F23")??0)+splits.Sum(a=>a.Difference)+old.Where(a=>a.Component=="Difference").Sum(a=>a.Amount);var date=Date(invoice,"F10");var bl=Date(invoice,"F17");var due=RegisterInvoiceSchema.DueDate(invoice);
            var expense=paymentBook.Sum(r=>r.InvoiceBankCharge(key))+store.GetExpenseReceipts().Where(r=>r.PartyId==party.Id&&r.InvoiceId==key).Sum(r=>r.Amount);
            var companyReceived=agentAllocations.Where(a=>a.InvoiceId==key).Sum(a=>a.Bank+a.Difference);
            rows.Add(new(){["A"]=(++index).ToString(Culture),["B"]=(invoice.GetValueOrDefault("F9","")+" "+(date?.ToString("dd-MM-yyyy")??"")+" / "+invoice.GetValueOrDefault("F3","")).Trim(),["C"]=Money(original),["D"]=Money(contract),["O"]=Money(companyReceived),["E"]=Money(receivedBank+receivedDifference),["F"]=Money(receivedBank+receivedDifference-companyReceived-expense),["H"]=Money(expense),["I"]="",["J"]=Money(receivedDifference),["K"]="",["L"]=bl?.ToString("yyyy-MM-dd")??"",["M"]=bl is {} start&&due is {} end?(end.DayNumber-start.DayNumber).ToString(Culture):"",["N"]=due?.ToString("yyyy-MM-dd")??""});
        }
        return new(new("Account Details",Columns(CompanyReportKind.AccountDetails),rows,new()),party.Name,
            ["New agent receipts allocate by oldest invoice date within the selected agent, bank-contract first then difference. Excess remains unallocated. Older agent receipts without allocations are not assigned retroactively.","Party payments allocate by oldest proforma date within the selected party and receiving agent: bank-contract first, then difference. Excess remains an advance.","Expense includes the proportional share of recorded Party Receipt bank charges. Expense receipts assigned to this invoice are added to the same total. Charges relating to unallocated advances are not assigned to an invoice.","Pending with agent is total allocated party receipts (bank plus difference) minus allocated company receipts, invoice bank charges and expense receipts. Negative pending indicates company receipts exceed recorded party receipts.","Account totals include advance payments and all recorded expenses for this party. Company payments include only allocations linked to this party; invoice-row totals exclude unallocated advances.","Older receipts without a receiving agent remain unallocated and are excluded from agent-specific allocations."],
            new Dictionary<string,string>{
                {"Invoices",rows.Count.ToString(Culture)},
                {"Paid by party","USD "+Money(paymentBook.Sum(r=>r.UsdAmount)+legacy.Sum(r=>r.Amount)+invoices.Sum(r=>(Number(r,"F25")??0)+(Number(r,"F23")??0)))},
                {"Expenses","USD "+Money(paymentBook.Sum(r=>r.BankTransferCharge??0)+store.GetExpenseReceipts().Where(r=>r.PartyId==party.Id).Sum(r=>r.Amount))},
                {"Agent received","USD "+Money(paymentBook.Sum(r=>r.NetReceivedUsd)+legacy.Sum(r=>r.Amount)+invoices.Sum(r=>(Number(r,"F25")??0)+(Number(r,"F23")??0))-store.GetExpenseReceipts().Where(r=>r.PartyId==party.Id).Sum(r=>r.Amount))},
                {"Sent to company","USD "+Money(rows.Sum(r=>decimal.Parse(r["O"],Culture)))},
                {"Pending with agent","USD "+Money(paymentBook.Sum(r=>r.NetReceivedUsd)+legacy.Sum(r=>r.Amount)+invoices.Sum(r=>(Number(r,"F25")??0)+(Number(r,"F23")??0))-store.GetExpenseReceipts().Where(r=>r.PartyId==party.Id).Sum(r=>r.Amount)-rows.Sum(r=>decimal.Parse(r["O"],Culture)))}
            });
    }
    private CompanyReport PartyLedger(WorksheetParty party)
    {
        var movements=new List<(DateOnly? Date,string Id,Dictionary<string,string> Row,decimal? Debit,decimal Credit)>();
        foreach(var invoice in Invoices(party.Id))
        {
            var date=Date(invoice,"F10");var due=RegisterInvoiceSchema.DueDate(invoice);var bl=Date(invoice,"F17");var amount=Number(invoice,"F5");
            movements.Add((date,invoice.GetValueOrDefault("_id",""),new(){["B"]=(date?.ToString("dd-MM-yyyy")??"Date missing")+" · "+(string.IsNullOrWhiteSpace(invoice.GetValueOrDefault("F9"))?invoice.GetValueOrDefault("F3",""):invoice["F9"]),["C"]=Money(amount),["D"]="",["G"]=due?.ToString("yyyy-MM-dd")??"",["H"]=Money(Number(invoice,"F6")),["I"]=bl?.ToString("yyyy-MM-dd")??"",["J"]=bl is {} start&&due is {} end?(end.DayNumber-start.DayNumber).ToString(Culture):"",["K"]=due?.ToString("yyyy-MM-dd")??""},amount,0));
        }
        foreach(var receipt in store.GetTransferReceipts(TransferReceiptKind.Party).Where(r=>r.MasterId==party.Id.ToString()))
            movements.Add((receipt.PaymentDate,receipt.Id.ToString(),new(){["B"]=$"{receipt.PaymentDate:dd-MM-yyyy} · Party receipt · {receipt.Description}",["C"]="",["D"]=Money(receipt.UsdAmount)},0,receipt.UsdAmount));
        foreach(var receipt in new ReportWorkflow(store).Receipts(party.Id))
            movements.Add((receipt.Date,receipt.Id.ToString(),new(){["B"]=$"{receipt.Date:dd-MM-yyyy} · Historical receipt · {receipt.Reference}",["C"]="",["D"]=Money(receipt.Amount)},0,receipt.Amount));
        foreach(var invoice in Invoices(party.Id))foreach(var (amountKey,dateKey) in new[]{("F25","F26"),("F23","F24")})
        {
            var received=Number(invoice,amountKey)??0;if(received==0)continue;var date=Date(invoice,dateKey);
            movements.Add((date,invoice.GetValueOrDefault("_id","")+amountKey,new(){["B"]=(date?.ToString("dd-MM-yyyy")??"Date missing")+" · Historical baseline receipt · "+invoice.GetValueOrDefault("F9",""),["C"]="",["D"]=Money(received)},0,received));
        }
        decimal running=0;var known=true;var rows=new List<Dictionary<string,string>>();
        foreach(var item in movements.OrderBy(m=>m.Date??DateOnly.MaxValue).ThenBy(m=>m.Credit>0?1:0).ThenBy(m=>m.Id,StringComparer.Ordinal))
        {known&=item.Debit.HasValue;running+=(item.Debit??0)-item.Credit;item.Row["E"]=known?Money(running):"";rows.Add(item.Row);}
        return new(new("Party Ledger",Columns(CompanyReportKind.PartyLedger),rows,new()),party.Name,
            ["Running balance is net recorded invoice debits minus current Party Receipt credits. No opening-balance adjustment is included.","Historical receipts and baseline payments are included once. Agent-to-company remittances do not reduce party debt a second time. Shipping deductions remain unavailable.","Any missing invoice amount makes the following running balance unavailable; blank is not zero."],
            new Dictionary<string,string>{{"Invoice debits",movements.Any(m=>!m.Debit.HasValue)?"Unavailable":"USD "+Money(movements.Sum(m=>m.Debit??0))},{"Party receipts","USD "+Money(movements.Sum(m=>m.Credit))},{"Net recorded balance",known?"USD "+Money(running):"Unavailable"}});
    }
    private CompanyReport AgentLedger(string masterId)
    {
        var agent=store.GetAgents().FirstOrDefault(a=>a.Id==masterId)??throw new BusinessRuleException("Select an agent from Agent Master.");
        var receipts=store.GetTransferReceipts(TransferReceiptKind.Agent).Where(r=>r.MasterId==masterId).ToList();
        var incoming=store.GetTransferReceipts(TransferReceiptKind.Party).Where(r=>r.ReceivingAgentId==masterId).ToList();
        var expenses=store.GetExpenseReceipts().Where(r=>r.AgentId==masterId).ToList();
        var rows=incoming.Select(r=>(r.PaymentDate,r.RecordedAt,r.Id,Row:new Dictionary<string,string>{["B"]=r.PaymentDate.ToString("yyyy-MM-dd"),["C"]=r.Name+" · "+r.Description,["D"]=Money(r.UsdAmount),["E"]=Money(r.BankTransferCharge??0),["F"]=Money(r.NetReceivedUsd),["G"]="",["H"]=""}))
            .Concat(receipts.Select(r=>(r.PaymentDate,r.RecordedAt,r.Id,Row:new Dictionary<string,string>{["B"]=r.PaymentDate.ToString("yyyy-MM-dd"),["C"]=r.Description,["D"]="",["E"]="",["F"]="",["G"]=Money(r.UsdAmount),["H"]=""})))
            .Concat(expenses.Select(r=>(PaymentDate:r.Date,r.RecordedAt,r.Id,Row:new Dictionary<string,string>{["B"]=r.Date.ToString("yyyy-MM-dd"),["C"]="Expense · "+r.Description,["D"]=Money(-r.Amount),["E"]="0.00",["F"]=Money(-r.Amount),["G"]="",["H"]=""})))
            .OrderBy(r=>r.PaymentDate).ThenBy(r=>r.RecordedAt).ThenBy(r=>r.Id).Select(r=>r.Row).ToList();
        decimal pending=0;
        foreach(var row in rows){pending+=(Number(row,"F")??0)-(Number(row,"G")??0);row["H"]=Money(pending);}
        var unlinked=store.GetTransferReceipts(TransferReceiptKind.Party).Count(r=>string.IsNullOrEmpty(r.ReceivingAgentId));
        return new(new("Agent Ledger",Columns(CompanyReportKind.AgentLedger),rows,new()),agent.Name,
            ["Expense rows reduce the party funds held by the linked agent. Pending with agent = party receipts minus recorded bank transfer charges, expense receipts and remittances to company. Opening balances and unrecorded historical charges are excluded. A negative balance means expenses and company remittances exceed recorded party receipts.",
             $"{unlinked} older Party Receipt(s) have no receiving agent. They remain unlinked and are not assigned by guessing.",
             "Legacy receipt books are preserved but have no reliable receiving-agent link and are not silently imported into this agent statement."],
            new Dictionary<string,string>{{"Party funds after expenses","USD "+Money(incoming.Sum(r=>r.UsdAmount)-expenses.Sum(r=>r.Amount))},{"Bank transfer charges","USD "+Money(incoming.Sum(r=>r.BankTransferCharge??0))},{"Net received by agent","USD "+Money(incoming.Sum(r=>r.NetReceivedUsd)-expenses.Sum(r=>r.Amount))},{"Agent remittances","USD "+Money(receipts.Sum(r=>r.UsdAmount))},{"Pending with agent","USD "+Money(pending)}});
    }
}












