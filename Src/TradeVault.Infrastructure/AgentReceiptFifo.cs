using System.Globalization;
using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public sealed partial class WorkbookStore
{
    // Agent remittances are a separate payment leg and never settle party debt again.
    private List<FifoReceiptAllocation> AllocateAgentReceipt(TransferReceipt receipt,List<TransferReceipt> existing)
    {
        var candidates=GetParties().Where(p=>!p.IsUnassigned)
            .SelectMany(p=>Load("form",p.Id).Rows.Select(row=>(Party:p.Id,Row:row)))
            .Where(x=>!x.Row.ContainsKey("_metadata")&&x.Row.GetValueOrDefault("_blank")!="1"&&x.Row.GetValueOrDefault("_agentId")==receipt.MasterId)
            .Select(x=>(x.Party,x.Row,Date:DateOnly.TryParseExact(x.Row.GetValueOrDefault("F4"),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?(DateOnly?)d:null))
            .Where(x=>x.Date!=null&&x.Date<=receipt.PaymentDate).OrderBy(x=>x.Date)
            .ThenBy(x=>x.Row.GetValueOrDefault("F3"),StringComparer.Ordinal).ThenBy(x=>x.Party).ThenBy(x=>x.Row.GetValueOrDefault("_id"),StringComparer.Ordinal);
        var result=new List<FifoReceiptAllocation>();var left=receipt.UsdAmount;
        foreach(var item in candidates)
        {
            if(left<=0)break;
            if(!decimal.TryParse(item.Row.GetValueOrDefault("F5"),NumberStyles.Number,CultureInfo.InvariantCulture,out var original)||!decimal.TryParse(item.Row.GetValueOrDefault("F6"),NumberStyles.Number,CultureInfo.InvariantCulture,out var contract)||contract<0||original<contract)break;
            var id=ReportWorkflow.InvoiceKey(item.Party,item.Row);
            var previous=existing.Where(r=>r.MasterId==receipt.MasterId).SelectMany(r=>r.Allocations??[]).Where(a=>a.InvoiceId==id).ToList();
            var bank=contract-previous.Sum(a=>a.Bank);var difference=original-contract-previous.Sum(a=>a.Difference);
            if(bank<0||difference<0)throw new BusinessRuleException("Agent receipts exceed invoice amounts. Correct the records before adding another receipt.");
            var payBank=Math.Min(left,bank);left-=payBank;var payDifference=Math.Min(left,difference);left-=payDifference;
            if(payBank+payDifference>0)result.Add(new(id,payBank,payDifference));
        }
        return result;
    }
}
