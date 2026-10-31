using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;

namespace TradeVault.Tests;

public sealed class InvoiceRegisterTests:IDisposable
{
    private readonly PortableDatabase database=new(new(Path.Combine(AppContext.BaseDirectory,"RegisterTests",Guid.NewGuid().ToString("N"))));
    private WorkbookStore Store(){var store=new WorkbookStore(database);store.CreateAgent("Agent");store.CreateAgent("Synthetic agent");return store;}
    public void Dispose()=>database.Dispose();
    [Fact]public void NewInvoicePersistsExactlyTwentyOneVisibleValuesAndLocksIdentity()
    {
        var store=Store();var party=store.CreateParty("Synthetic register buyer").Id;
        var id=store.CreateRegisterInvoice(party,0,new(" India "," Synthetic agent "," PI-001 ",new(2026,5,1)));
        var saved=store.Load("form",party);var row=Assert.Single(saved.Rows);
        Assert.Equal(21,RegisterInvoiceSchema.VisibleKeys.Length);Assert.All(RegisterInvoiceSchema.VisibleKeys,key=>Assert.True(row.ContainsKey(key)));
        Assert.Equal("India",row["F2"]);Assert.Equal("Synthetic agent",row["F27"]);Assert.Equal("PI-001",row["F3"]);Assert.Equal("2026-05-01",row["F4"]);Assert.Equal(id,row["_id"]);
        Assert.Empty(new ReportWorkflow(store).BuildCurrent(party).Tables[0].Rows);
        foreach(var key in RegisterInvoiceSchema.IdentityKeys)
        {
            var attempt=store.Load("form",party);attempt.Rows[0][key]=key=="F4"?"2026-05-02":"Changed";
            Assert.Throws<BusinessRuleException>(()=>store.Save("form",attempt.Revision,attempt.Rows,party));
        }
        row["F9"]="INV-001";row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F7"]="9000";row["F17"]="2026-06-17";
        store.Save("form",saved.Revision,saved.Rows,party);
        Assert.Equal("2026-09-15",Assert.Single(new ReportWorkflow(store).BuildCurrent(party).Tables[0].Rows)["N"]);
    }
    [Fact]public void RequiredIdentityAndStaleRevisionCannotCreateRecords()
    {
        var store=Store();var party=store.CreateParty("Synthetic validation buyer").Id;
        foreach(var request in new[]{new NewRegisterInvoice("","Agent","PI",new(2026,5,1)),new("India"," ","PI",new(2026,5,1)),new("India","Agent","",new(2026,5,1))})
            Assert.Throws<BusinessRuleException>(()=>store.CreateRegisterInvoice(party,0,request));
        Assert.Equal(0,store.Load("form",party).Revision);
        store.CreateRegisterInvoice(party,0,new("India","Agent","PI",new(2026,5,1)));
        Assert.Throws<BusinessRuleException>(()=>store.CreateRegisterInvoice(party,0,new("India","Agent","PI2",new(2026,5,1))));
        Assert.Single(store.Load("form",party).Rows);
    }
    [Fact]public void CustomDeadlineDrivesReportsRemindersAndOverdueAndCanBeReset()
    {
        var store=Store();var party=store.CreateParty("Synthetic custom deadline").Id;
        store.CreateRegisterInvoice(party,0,new("India","Agent","PI",new(2026,5,1)));
        var saved=store.Load("form",party);var row=saved.Rows[0];row["F9"]="INV";row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F7"]="9000";row["F17"]="2026-06-17";row["_dueOverride"]="2026-10-01";
        store.Save("form",saved.Revision,saved.Rows,party);var workflow=new ReportWorkflow(store);
        Assert.Equal("2026-10-01",Assert.Single(workflow.BuildCurrent(party).Tables[0].Rows)["N"]);
        Assert.Equal(4,workflow.SettlementAwareOverdue(party,row,new(2026,10,5)));
        Assert.Equal(new DateOnly(2026,10,1),Assert.Single(workflow.PaymentBalances(party,new(2026,10,5))).Due);
        row["F17"]="2026-07-01";Assert.Equal(new DateOnly(2026,10,1),RegisterInvoiceSchema.DueDate(row));
        row["_dueOverride"]="";Assert.Equal(new DateOnly(2026,9,29),RegisterInvoiceSchema.DueDate(row));
        row["_dueOverride"]="2026-05-01";Assert.Throws<BusinessRuleException>(()=>store.Save("form",saved.Revision+1,saved.Rows,party));
    }
    
}
