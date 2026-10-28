using TradeVault.Infrastructure;
using TradeVault.Domain;
using Xunit;
namespace TradeVault.Tests;

public sealed class InvoiceRulesTests
{
    [Theory]
    [InlineData("F4","2026-07-02","F10","2026-07-01")]
    [InlineData("F10","2026-07-02","F13","2026-07-01")]
    [InlineData("F13","2026-07-02","F14","2026-07-01")]
    [InlineData("F14","2026-07-02","F17","2026-07-01")]
    [InlineData("F10","2026-07-02","F24","2026-07-01")]
    [InlineData("F10","2026-07-02","F26","2026-07-01")]
    public void RejectsReversedDatesAndAcceptsSameDay(string first,string firstDate,string last,string lastDate)
    {
        var row=new Dictionary<string,string>{{first,firstDate},{last,lastDate}};
        Assert.Contains(InvoiceRules.Validate(row),i=>i.Field==last);
        row[last]=firstDate;Assert.Empty(InvoiceRules.Validate(row));
    }
    [Theory][InlineData("F22","F24")][InlineData("F23","F24")][InlineData("F25","F26")]
    public void PaymentNeedsInvoiceAndEffectiveDate(string amount,string date)
    {
        var row=new Dictionary<string,string>{{amount,"100"}};Assert.Contains(InvoiceRules.Validate(row),i=>i.Field=="F10");Assert.Contains(InvoiceRules.Validate(row),i=>i.Field==date);
        row["F10"]="2026-07-01";row[date]="2026-07-01";Assert.Empty(InvoiceRules.Validate(row));
    }
    [Theory][InlineData("-1")][InlineData("1.001")][InlineData("1000000000001")][InlineData("bad")]
    public void RejectsInvalidUsd(string value)=>Assert.Contains(InvoiceRules.Validate(new Dictionary<string,string>{{"F22",value}}),i=>i.Field=="F22");
    [Fact]public void DatabaseSaveRejectsInvalidPaymentWithoutChangingRevision()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"RulesTests",Guid.NewGuid().ToString("N"));using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);var session=store.Login("operator","Operator@123");var party=store.CreateParty(session,"Rules test");
        var row=new Dictionary<string,string>{{"F9","RULE-1"},{"F10","2026-07-02"},{"F22","100"},{"F24","2026-07-01"}};
        Assert.Throws<BusinessRuleException>(()=>store.Save(session,"form",0,[row],party.Id));Assert.Equal(0,store.Load(session,"form",party.Id).Revision);
        row["F24"]="2026-07-02";Assert.Equal(1,store.Save(session,"form",0,[row],party.Id));
    }
}
