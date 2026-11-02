using TradeVault.Infrastructure;
using TradeVault.Domain;
using Xunit;
namespace TradeVault.Tests;
public sealed class ReceiptChargeTests
{
    [Fact] public void ChargeDefaultsToZeroValidatesAndPersistsWithoutChangingGrossReceipt()
    {
        var paths=new PortablePaths(Path.Combine(AppContext.BaseDirectory,"ChargeTests",Guid.NewGuid().ToString("N")));
        using(var db=new PortableDatabase(paths))
        {
            var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");
            var zero=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,11,1),100,"Zero",agent.Id);Assert.Equal(0m,zero.BankTransferCharge);
            var charged=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,11,1),1000,"Charged",agent.Id,bankTransferCharge:10);Assert.Equal(1000,charged.UsdAmount);Assert.Equal(10m,charged.BankTransferCharge);
            foreach(var charge in new[]{-1m,0.001m})Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,11,1),100,"Invalid",agent.Id,bankTransferCharge:charge));Assert.Equal(2,store.GetTransferReceipts(TransferReceiptKind.Party).Count);
        }
        using(var db=new PortableDatabase(paths)){var entries=new WorkbookStore(db).GetTransferReceipts(TransferReceiptKind.Party);Assert.Contains(entries,r=>r.BankTransferCharge==0);Assert.Contains(entries,r=>r.BankTransferCharge==10);}
    }
    [Fact]public void ChargesSplitAcrossPaidInvoicesAndReduceAgentBalanceButNotPartyDebt()
    {
        using var db=new PortableDatabase(new(Path.Combine(AppContext.BaseDirectory,"ChargeTests",Guid.NewGuid().ToString("N"))));var store=new WorkbookStore(db);var party=store.CreateParty("Buyer");var agent=store.CreateAgent("Agent");
        foreach(var (number,amount) in new[]{("PI-1",600), ("PI-2",400)})
        {
            var snap=store.Load("form",party.Id);store.CreateRegisterInvoice(party.Id,snap.Revision,new("Pakistan",agent.Name,number,new(2026,1,1),agent.Id));snap=store.Load("form",party.Id);var row=snap.Rows.Single(r=>r.GetValueOrDefault("F3")==number);row["F5"]=amount.ToString();row["F6"]=amount.ToString();store.Save("form",snap.Revision,snap.Rows,party.Id);
        }
        var payment=store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,2,1),1000,"Wire",agent.Id,bankTransferCharge:10);Assert.Equal(1000,payment.AllocatedUsd);Assert.Equal(990,payment.NetReceivedUsd);
        store.AddTransferReceipt(TransferReceiptKind.Agent,agent.Id,new(2026,2,2),500,"Remit");var reports=new CompanyReports(store);var details=reports.Build(CompanyReportKind.AccountDetails,party.Id.ToString());Assert.Equal(new[]{"6.00","4.00"},details.Table.Rows.Select(r=>r["H"]));Assert.Equal(new[]{"94.00","396.00"},details.Table.Rows.Select(r=>r["F"]));
        var ledger=reports.Build(CompanyReportKind.AgentLedger,agent.Id);Assert.Equal("10.00",ledger.Table.Rows[0]["E"]);Assert.Equal("990.00",ledger.Table.Rows[0]["F"]);Assert.Equal("USD 490.00",ledger.Summary["Pending with agent"]);Assert.Equal(490m,reports.MasterAmounts(true,agent.Id).Pending);Assert.Equal(990m,reports.MasterAmounts(true,agent.Id).OriginalOrPartyReceipts);Assert.Equal(0m,reports.MasterAmounts(false,party.Id.ToString()).Pending);
        Assert.Throws<BusinessRuleException>(()=>store.AddTransferReceipt(TransferReceiptKind.Party,party.Id.ToString(),new(2026,2,3),10,"Invalid",agent.Id,bankTransferCharge:11));
    }
    [Fact]public void CentsReconcileAndAdvanceChargeIsNotInventedAsInvoiceExpense()
    {
        var receipt=new TransferReceipt(Guid.NewGuid(),"buyer","Buyer",new(2026,1,1),3,"",DateTimeOffset.UtcNow,Allocations:[new("one",1,0),new("two",1,0),new("three",1,0)],BankTransferCharge:0.01m);
        Assert.Equal(0.01m,new[]{"one","two","three"}.Sum(receipt.InvoiceBankCharge));
        var advance=receipt with{UsdAmount=4,BankTransferCharge=1,Allocations=[new("one",1,0),new("two",1,0)]};Assert.Equal(0.50m,advance.InvoiceBankCharge("one")+advance.InvoiceBankCharge("two"));Assert.Equal(0m,(receipt with{BankTransferCharge=null}).InvoiceBankCharge("one"));
    }}


