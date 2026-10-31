using System.Globalization;
using TradeVault.Application;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class WorkbookStore
{
    private List<FifoReceiptAllocation> AllocatePartyReceipt(TransferReceipt receipt,List<TransferReceipt> existing)
    {
        var party=Guid.Parse(receipt.MasterId);var candidates=Load("form",party).Rows.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"&&r.GetValueOrDefault("_agentId")==receipt.ReceivingAgentId)
            .Select(r=>(Row:r,Date:DateOnly.TryParseExact(r.GetValueOrDefault("F4"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?(DateOnly?)date:null))
            .Where(r=>r.Date!=null&&r.Date<=receipt.PaymentDate).OrderBy(r=>r.Date).ThenBy(r=>r.Row.GetValueOrDefault("F3"),StringComparer.Ordinal).ThenBy(r=>r.Row.GetValueOrDefault("_id"),StringComparer.Ordinal);
        var allocations=new List<FifoReceiptAllocation>();var left=receipt.UsdAmount;
        var legacy=Document<ReceiptBook>("receipts",party.ToString())?.Receipts??[];
        foreach(var (row,_) in candidates)
        {
            if(left<=0)break;
            if(!decimal.TryParse(row.GetValueOrDefault("F5"),NumberStyles.Number,CultureInfo.InvariantCulture,out var original)||!decimal.TryParse(row.GetValueOrDefault("F6"),NumberStyles.Number,CultureInfo.InvariantCulture,out var contract)||original<contract)break;
            var id=ReportWorkflow.InvoiceKey(party,row);
            decimal Earlier(string key)=>decimal.TryParse(row.GetValueOrDefault(key),NumberStyles.Number,CultureInfo.InvariantCulture,out var value)?value:0;
            if(new[]{("F25","F26"),("F23","F24")}.Any(pair=>Earlier(pair.Item1)>0&&!DateOnly.TryParseExact(row.GetValueOrDefault(pair.Item2),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)))break;
            decimal Reserved(bool bankComponent)
            {
                var component=bankComponent?"Bank":"Difference";var amountKey=bankComponent?"F25":"F23";var dateKey=bankComponent?"F26":"F24";
                var movements=existing.Where(r=>r.MasterId==receipt.MasterId).Select(r=>(Date:r.PaymentDate,Amount:(r.Allocations??[]).Where(a=>a.InvoiceId==id).Sum(a=>bankComponent?a.Bank:a.Difference)))
                    .Concat(legacy.Select(r=>(Date:r.Date,Amount:r.Allocations.Where(a=>a.InvoiceId==id&&a.Component==component).Sum(a=>a.Amount)))).ToList();
                if(Earlier(amountKey)>0)movements.Add((DateOnly.ParseExact(row[dateKey],"yyyy-MM-dd",CultureInfo.InvariantCulture),Earlier(amountKey)));
                var running=movements.Where(m=>m.Date<=receipt.PaymentDate).Sum(m=>m.Amount);var peak=running;
                foreach(var day in movements.Where(m=>m.Date>receipt.PaymentDate).GroupBy(m=>m.Date).OrderBy(g=>g.Key)){running+=day.Sum(m=>m.Amount);peak=Math.Max(peak,running);}
                return peak;
            }
            var bank=contract-Reserved(true);
            var difference=original-contract-Reserved(false);
            if(bank<0||difference<0)throw new BusinessRuleException("Existing payments exceed invoice amounts. Correct the historical records before allocating another receipt.");
            var payBank=Math.Min(left,bank);left-=payBank;var payDifference=Math.Min(left,difference);left-=payDifference;
            if(payBank+payDifference>0)allocations.Add(new(id,payBank,payDifference));
        }
        return allocations;
    }
    private void GuardFifoInvoices(Guid party,List<Dictionary<string,string>> rows,Dictionary<string,Dictionary<string,string>> previous)
    {
        var partyKeys=previous.Keys.ToHashSet();
        var allocated=GetTransferReceipts(TransferReceiptKind.Party).Where(r=>r.MasterId==party.ToString()).Concat(GetTransferReceipts(TransferReceiptKind.Agent)).SelectMany(r=>r.Allocations??[]).Select(a=>a.InvoiceId).Where(partyKeys.Contains).Distinct();
        foreach(var id in allocated)
        {
            var next=rows.FirstOrDefault(r=>ReportWorkflow.InvoiceKey(party,r)==id)??throw new BusinessRuleException("An invoice with allocated receipts cannot be deleted or cut.");
            if(previous.TryGetValue(id,out var before))foreach(var key in new[]{"F5","F6","F25","F23","F24","F26","_agentId"})
                if(before.GetValueOrDefault(key,"")!=next.GetValueOrDefault(key,""))throw new BusinessRuleException("Invoice amounts and agent linked to receipts are protected. Keep the saved payment allocation intact.");
        }
    }
}
