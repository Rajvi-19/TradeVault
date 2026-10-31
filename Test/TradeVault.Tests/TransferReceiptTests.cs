using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class TransferReceiptTests
{
    [Fact]public void UsdValueIsOptionalStoredTextAndDoesNotChangeReceiptOrAllocation()
    {
        var paths=new PortablePaths(Path.Combine(AppContext.BaseDirectory,"TransferTests",Guid.NewGuid().ToString("N")));Guid receiptId;
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"PI-1",new(2026,6,1),agent.Id));var snapshot=store.Load("form",party.Id);snapshot.Rows[0]["F5"]="1000";snapshot.Rows[0]["F6"]="600";store.Save("form",snapshot.Revision,snapshot.Rows,party.Id);
            var receipt=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,7,1),800,"Payment",agent.Id,"  280.50 PKR  ");receiptId=receipt.Id;
            Assert.Equal(800,receipt.UsdAmount);Assert.Equal("280.50 PKR",receipt.UsdValue);var split=Assert.Single(receipt.Allocations!);Assert.Equal(600,split.Bank);Assert.Equal(200,split.Difference);
            Assert.Throws<TradeVault.Domain.BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,7,2),1,"",agent.Id,new string('x',101)));Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Party));
            var backup=store.CreateBackup();store.RestoreBackup(backup);
        }
        using(var db=new PortableDatabase(paths)){var receipt=Assert.Single(new WorkbookStore(db).GetTransferReceipts(TransferReceiptKind.Party));Assert.Equal(receiptId,receipt.Id);Assert.Equal("280.50 PKR",receipt.UsdValue);Assert.Equal(800,receipt.UsdAmount);}
    }
    [Fact] public void IndependentPaymentLegsPersistReopenAndRestoreWithoutChangingInvoices()
    {
        var paths=new PortablePaths(Path.Combine(AppContext.BaseDirectory,"TransferTests",Guid.NewGuid().ToString("N")));
        Guid partyId;string agentId;Guid receiptId;
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var party=store.CreateParty("Synthetic Pakistan buyer");var agent=store.CreateAgent("Synthetic Malaysia agent");partyId=party.Id;agentId=agent.Id;
            store.CreateRegisterInvoice(party.Id,0,new("Pakistan",agent.Name,"PI-001",new(2026,10,1),agent.Id));var before=store.Load("form",party.Id);
            receiptId=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,10,15),12500.50m,"  Shipping charge  ",agent.Id).Id;
            store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,10,20),10000m,"");
            Assert.Equal(before.Revision,store.Load("form",party.Id).Revision);Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Party));Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Agent));
            var backup=store.CreateBackup();store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,10,21),5m,null,agent.Id);store.RestoreBackup(backup);Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Party));
        }
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var party=Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Party));Assert.Equal(receiptId,party.Id);Assert.Equal(partyId.ToString(),party.MasterId);Assert.Equal(12500.50m,party.UsdAmount);Assert.Equal("Shipping charge",party.Description);Assert.Equal(agentId,Assert.Single(store.GetTransferReceipts(TransferReceiptKind.Agent)).MasterId);
        }
    }
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
