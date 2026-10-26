using TradeVault.Application;
using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

// Synthetic data only; test folders stay under the test binary, never host TEMP.
public sealed class BackendTests : IDisposable
{
    private readonly string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
    private readonly PortableDatabase database;
    private readonly TradeVaultService service;
    private static Usd Money(decimal amount) => Usd.FromDecimal(amount);
    private static readonly DateOnly Start = new(2026, 6, 1);
    public BackendTests()
    {
        database = new(new PortablePaths(root));
        service = new(database);
    }
    private Party Party(string name = "Synthetic Textile") => service.CreateParty(new(name, "MY"));
    private Invoice Invoice(Party party, string number = "EXP/001/26-27", decimal bank = 10000) =>
        service.CreateInvoice(new(party.Id, number, Start, Money(15000), Money(bank), new DateOnly(2026, 6, 17)));
    private static UpdateInvoice Change(Invoice i, int? terms = null, Usd? contract = null) =>
        new(i.Id, i.Version, i.Number, i.InvoiceDate, i.OriginalAmount, contract ?? i.BankContractAmount,
            i.FinalLegBlDate, terms ?? i.TermsDays, i.ShippingCharge);

    [Fact]
    public void DefaultsAndChangesPersistAcrossReopen()
    {
        var party = Party();
        var invoice = Invoice(party);
        Assert.Equal(new DateOnly(2026, 9, 15), invoice.DueDate);
        Assert.Equal(Money(3800), invoice.ShippingCharge);
        var updated = service.UpdateInvoice(Change(invoice, 60) with { ShippingCharge = Money(4100) });
        database.Dispose();
        using var reopened = new PortableDatabase(new(root));
        var saved = Assert.Single(new TradeVaultService(reopened).GetInvoices(party.Id));
        Assert.Equal(updated, saved);
        Assert.Equal(new DateOnly(2026, 8, 16), saved.DueDate);
    }

    [Fact]
    public void MultiplePartialReceiptsAndOneReceiptAcrossInvoices()
    {
        var party = Party();
        var first = Invoice(party);
        var second = Invoice(party, "EXP/002/26-27", 5000);
        service.RecordReceipt(new(party.Id, Start.AddDays(30), Money(3000), "R1", [new(first.Id, Money(3000))]));
        var receipt = service.RecordReceipt(new(party.Id, Start.AddDays(40), Money(8000), "R2",
            [new(first.Id, Money(6000)), new(second.Id, Money(1500))]));
        Assert.Equal(Money(500), receipt.Unallocated);
        var balances = service.GetBankContractBalances(party.Id, Start.AddDays(50));
        Assert.Equal(Money(1000), balances.Single(x => x.Invoice.Id == first.Id).Outstanding);
        Assert.Equal(Money(3500), balances.Single(x => x.Invoice.Id == second.Id).Outstanding);
        var receipts = service.GetReceipts(party.Id);
        Assert.Equal(2, receipts.Count);
        Assert.Equal(Money(500), receipts.Single(x => x.Id == receipt.Id).Unallocated);
    }

    [Fact]
    public void HistoricalBalanceExcludesFutureReceiptsAndInvoices()
    {
        var party = Party();
        var invoice = Invoice(party);
        service.RecordReceipt(new(party.Id, Start.AddDays(10), Money(1000), "later", [new(invoice.Id, Money(1000))]));
        Assert.Empty(service.GetBankContractBalances(party.Id, Start.AddDays(-1)));
        Assert.Equal(Money(10000), Assert.Single(service.GetBankContractBalances(party.Id, Start)).Outstanding);
        Assert.Equal(Money(9000), Assert.Single(service.GetBankContractBalances(party.Id, Start.AddDays(10))).Outstanding);
    }

    [Fact]
    public void OverAllocationRollsBackEntireReceipt()
    {
        var party = Party();
        var first = Invoice(party);
        var second = Invoice(party, "EXP/002", 100);
        Assert.Throws<BusinessRuleException>(() => service.RecordReceipt(new(party.Id, Start, Money(500), "invalid",
            [new(first.Id, Money(100)), new(second.Id, Money(200))])));
        Assert.Empty(service.GetReceipts(party.Id));
        Assert.Equal(Money(10000), service.GetBankContractBalances(party.Id, Start).Single(x => x.Invoice.Id == first.Id).Outstanding);
        Assert.Throws<BusinessRuleException>(() => service.RecordReceipt(new(party.Id, Start, Money(50), "invalid", [new(first.Id, Money(100))])));
    }

    [Fact]
    public void CrossPartyAndDuplicateAllocationsAreRejected()
    {
        var party = Party();
        var invoice = Invoice(Party("Other"));
        Assert.Throws<BusinessRuleException>(() => service.RecordReceipt(new(party.Id, Start, Money(100), "wrong party", [new(invoice.Id, Money(100))])));
        invoice = Invoice(party);
        Assert.Throws<BusinessRuleException>(() => service.RecordReceipt(new(party.Id, Start, Money(100), "duplicate",
            [new(invoice.Id, Money(50)), new(invoice.Id, Money(50))])));
    }

    [Fact]
    public void OverdueStopsWhenFullyPaidAndUnknownBlHasNoDueDate()
    {
        var party = Party();
        var invoice = Invoice(party);
        var day = new DateOnly(2026, 9, 20);
        Assert.Equal(5, Assert.Single(service.GetBankContractBalances(party.Id, day)).OverdueDays);
        service.RecordReceipt(new(party.Id, day, Money(10000), "settled", [new(invoice.Id, Money(10000))]));
        Assert.Equal(0, Assert.Single(service.GetBankContractBalances(party.Id, day)).OverdueDays);
        var unknown = service.CreateInvoice(new(party.Id, "no BL", Start, Money(10), Money(10)));
        Assert.Null(unknown.DueDate);
    }

    [Fact]
    public void StaleEditsDuplicatesAndReducingAllocatedAmountsAreRejected()
    {
        var party = Party();
        var invoice = Invoice(party);
        service.UpdateInvoice(Change(invoice, 30));
        Assert.Throws<BusinessRuleException>(() => service.UpdateInvoice(Change(invoice, 60)));
        Assert.Throws<BusinessRuleException>(() => service.CreateInvoice(new(party.Id, invoice.Number.ToLowerInvariant(), Start, Money(10), Money(10))));
        service.RecordReceipt(new(party.Id, Start, Money(500), "allocated", [new(invoice.Id, Money(500))]));
        var current = Assert.Single(service.GetInvoices(party.Id));
        Assert.Throws<BusinessRuleException>(() => service.UpdateInvoice(Change(current, contract: Money(400))));
    }

    [Fact]
    public void BackupCanBeReopenedAndSecondInstanceIsBlocked()
    {
        var party = Party();
        Invoice(party);
        Assert.Throws<IOException>(() => new PortableDatabase(new(root)));
        var backup = database.CreateBackup();
        var restoreRoot = Path.Combine(root, "Restore");
        var restorePaths = new PortablePaths(restoreRoot);
        Directory.CreateDirectory(restorePaths.Data);
        File.Copy(backup, restorePaths.Database);
        using var restored = new PortableDatabase(restorePaths);
        Assert.Single(new TradeVaultService(restored).GetInvoices(party.Id));
    }

    [Fact]
    public void InvalidPrecisionDatesAndNegativeValuesAreRejected()
    {
        Assert.Throws<BusinessRuleException>(() => Money(1.001m));
        Assert.Throws<BusinessRuleException>(() => DueDates.Calculate(Start, -1));
        Assert.Throws<BusinessRuleException>(() => DueDates.Calculate(DateOnly.MaxValue, 90));
        var party = Party();
        Assert.Throws<BusinessRuleException>(() => service.CreateInvoice(new(party.Id, "negative", Start, Money(-1), Money(100))));
        var invoice = Invoice(party);
        Assert.Throws<BusinessRuleException>(() => service.RecordReceipt(new(party.Id, Start.AddDays(-1), Money(10), "advance", [new(invoice.Id, Money(10))])));
    }

    public void Dispose()
    {
        database.Dispose();
        Directory.Delete(root, recursive: true);
    }
}
