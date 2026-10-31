using TradeVault.Domain;

namespace TradeVault.Infrastructure;

public enum TransferReceiptKind { Party, Agent }
public sealed record FifoReceiptAllocation(string InvoiceId,decimal Bank,decimal Difference);
public sealed record TransferReceipt(Guid Id,string MasterId,string Name,DateOnly PaymentDate,decimal UsdAmount,string Description,DateTimeOffset RecordedAt,string? ReceivingAgentId=null,List<FifoReceiptAllocation>? Allocations=null,string? UsdValue=null)
{
    [System.Text.Json.Serialization.JsonIgnore] public decimal AllocatedUsd=>(Allocations??[]).Sum(a=>a.Bank+a.Difference);
    [System.Text.Json.Serialization.JsonIgnore] public decimal UnallocatedUsd=>UsdAmount-AllocatedUsd;
    [System.Text.Json.Serialization.JsonIgnore] public string UsdValueDisplay=>UsdValue??"";
}
public sealed record TransferReceiptBook(List<TransferReceipt> Entries);

public sealed partial class WorkbookStore
{
    private static string TransferKind(TransferReceiptKind kind)=>kind switch
    {
        TransferReceiptKind.Party=>"party_transfer_receipts",
        TransferReceiptKind.Agent=>"agent_transfer_receipts",
        _=>throw new BusinessRuleException("Choose a valid receipt type.")
    };
    public IReadOnlyList<TransferReceipt> GetTransferReceipts(TransferReceiptKind kind)=>
        (Document<TransferReceiptBook>(TransferKind(kind),"all")?.Entries??[])
        .OrderByDescending(entry=>entry.PaymentDate).ThenByDescending(entry=>entry.RecordedAt).ToList();

    public TransferReceipt AddTransferReceipt(TransferReceiptKind kind,string masterId,DateOnly date,decimal usd,string? description,string? receivingAgentId=null,string? usdValue=null)
    {
        var documentKind=TransferKind(kind);
        string name;
        if(kind==TransferReceiptKind.Party)
        {
            var party=GetParties().FirstOrDefault(p=>!p.IsUnassigned&&p.Id.ToString()==masterId)
                ??throw new BusinessRuleException("Select a party from Party Master.");name=party.Name;
        }
        else name=GetAgents().FirstOrDefault(agent=>agent.Id==masterId)?.Name
            ??throw new BusinessRuleException("Select an agent from Agent Master.");
        if(usd<=0||usd>1_000_000_000_000m||decimal.Round(usd,2)!=usd)
            throw new BusinessRuleException("Enter a positive USD amount with at most two decimal places (maximum 1 trillion).");
        description=(description??"").Trim();
        if(description.Length>500)throw new BusinessRuleException("Reference / description must be 500 characters or fewer.");
        if(kind==TransferReceiptKind.Party&&!GetAgents().Any(a=>a.Id==receivingAgentId))throw new BusinessRuleException("Select the receiving agent from Agent Master.");
        usdValue=usdValue?.Trim();
        if(usdValue?.Length>100)throw new BusinessRuleException("USD value must be 100 characters or fewer.");
        var receipt=new TransferReceipt(Guid.NewGuid(),masterId,name,date,usd,description,DateTimeOffset.UtcNow,kind==TransferReceiptKind.Party?receivingAgentId:null,UsdValue:usdValue);
        Mutate<TransferReceiptBook>(documentKind,"all",book=>
        {
            if(kind==TransferReceiptKind.Party)receipt=receipt with{Allocations=AllocatePartyReceipt(receipt,book?.Entries??[])};
            else receipt=receipt with{Allocations=AllocateAgentReceipt(receipt,book?.Entries??[])};
            return new([..book?.Entries??[],receipt]);
        });
        return receipt;
    }
    public TransferReceipt AssignReceivingAgent(Guid receiptId,string agentId)
    {
        if(!GetAgents().Any(a=>a.Id==agentId))throw new BusinessRuleException("Select an agent from Agent Master.");
        TransferReceipt? updated=null;
        Mutate<TransferReceiptBook>(TransferKind(TransferReceiptKind.Party),"all",book=>
        {
            var entries=book?.Entries.ToList()??[];var index=entries.FindIndex(r=>r.Id==receiptId);
            if(index<0)throw new BusinessRuleException("Receipt not found. Reload the table.");
            var receipt=entries[index];if(!string.IsNullOrEmpty(receipt.ReceivingAgentId))throw new BusinessRuleException("This receipt already has a receiving agent. Its saved allocation is protected.");
            updated=receipt with{ReceivingAgentId=agentId};updated=updated with{Allocations=AllocatePartyReceipt(updated,entries)};entries[index]=updated;return new(entries);
        });
        return updated!;
    }
}
