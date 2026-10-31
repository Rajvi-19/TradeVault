using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class TransferReceiptTests
{
    
    
    [Fact] public void MasterConstraintsAmountsAndDescriptionAreValidatedWithoutWrites()
    {
        using var db=new PortableDatabase(new(Path.Combine(AppContext.BaseDirectory,"TransferTests",Guid.NewGuid().ToString("N"))));var store=new WorkbookStore(db);var party=store.CreateParty("Synthetic party");var agent=store.CreateAgent("Synthetic agent");
        foreach(var value in new[]{0m,-1m,0.001m,1_000_000_000_001m})Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,10,1),value,"",agent.Id));
        Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Party,Guid.Empty.ToString(),new(2026,10,1),1,""));
        Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Agent,party.Id.ToString(),new(2026,10,1),1,""));
        Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Party,agent.Id,new(2026,10,1),1,""));
        Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,10,1),1,new string('x',501)));
        Assert.Throws<BusinessRuleException>(()=>store.GetTransferReceipts((TransferReceiptKind)42));Assert.Empty(store.GetTransferReceipts(TransferReceiptKind.Party));Assert.Empty(store.GetTransferReceipts(TransferReceiptKind.Agent));
    }
}
