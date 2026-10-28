using Microsoft.Data.Sqlite;
using TradeVault.Domain;
using TradeVault.Infrastructure;
using Xunit;
namespace TradeVault.Tests;
public sealed class AccountRecoveryTests:IDisposable
{
    private readonly string root=Path.Combine(AppContext.BaseDirectory,"AccountTests",Guid.NewGuid().ToString("N"));
    [Fact]public void UsernameIsCaseSensitiveAndRecoveryCodeIsSingleUse()
    {
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);
        Assert.Throws<BusinessRuleException>(()=>store.Login("Owner","Owner@123"));
        var session=store.Login("owner","Owner@123");Assert.False(store.HasRecoveryCode(session));
        var code=store.GenerateRecoveryCode(session);Assert.True(store.HasRecoveryCode(session));
        Assert.Throws<BusinessRuleException>(()=>store.RecoverPassword("Owner",code,"New-Test-Password-123"));
        Assert.Throws<BusinessRuleException>(()=>store.RecoverPassword("owner","wrong","New-Test-Password-123"));
        store.RecoverPassword("owner",code,"New-Test-Password-123");
        Assert.Throws<BusinessRuleException>(()=>store.GetParties(session));
        Assert.Throws<BusinessRuleException>(()=>store.Login("owner","Owner@123"));
        var signedIn=store.Login("owner","New-Test-Password-123");Assert.False(store.HasRecoveryCode(signedIn));
        Assert.Throws<BusinessRuleException>(()=>store.RecoverPassword("owner",code,"Another-Password-123"));
        Assert.Equal("Operator",store.Login("operator","Operator@123").Role);
    }
    [Fact]public void RecoverySurvivesRestartAndRotationInvalidatesOldCode()
    {
        string oldCode,code;
        using(var db=new PortableDatabase(new(root)))
        {var store=new WorkbookStore(db);var session=store.Login("operator","Operator@123");oldCode=store.GenerateRecoveryCode(session);code=store.GenerateRecoveryCode(session);Assert.NotEqual(oldCode,code);}
        using var reopened=new PortableDatabase(new(root));var restored=new WorkbookStore(reopened);
        Assert.Throws<BusinessRuleException>(()=>restored.RecoverPassword("operator",oldCode,"New-Operator-Password"));
        restored.RecoverPassword("operator",code,"New-Operator-Password");Assert.Equal("Operator",restored.Login("operator","New-Operator-Password").Role);
        using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=reopened.Paths.Database,Pooling=false}.ToString());c.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT hash FROM account_recovery WHERE username='operator'";Assert.Equal(DBNull.Value,cmd.ExecuteScalar());
    }
    [Fact]public void FailedRecoveryCooldownPersistsAndSecretsAreNotStoredPlaintext()
    {
        string code;
        using(var db=new PortableDatabase(new(root)))
        {
            var store=new WorkbookStore(db);code=store.GenerateRecoveryCode(store.Login("owner","Owner@123"));
            for(int i=0;i<5;i++)Assert.Throws<BusinessRuleException>(()=>store.RecoverPassword("owner","bad","Replacement-Password"));
        }
        using var reopened=new PortableDatabase(new(root));var reloaded=new WorkbookStore(reopened);
        var error=Assert.Throws<BusinessRuleException>(()=>reloaded.RecoverPassword("owner",code,"Replacement-Password"));Assert.Contains("Too many",error.Message);
        Assert.DoesNotContain(code,System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(reopened.Paths.Database)));
    }
    [Fact]public void NormalPasswordChangeAlsoInvalidatesRecoveryAndUnauthenticatedGenerationFails()
    {
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);var session=store.Login("owner","Owner@123");var code=store.GenerateRecoveryCode(session);
        store.ChangePassword(session,"Owner@123","New-Owner-Password");
        Assert.Throws<BusinessRuleException>(()=>store.GenerateRecoveryCode(session));
        Assert.Throws<BusinessRuleException>(()=>store.RecoverPassword("owner",code,"Unauthorized-New-Password"));
        Assert.Equal("Owner",store.Login("owner","New-Owner-Password").Role);
    }
    public void Dispose(){Directory.Delete(root,true);}
    [Fact]public void SixCharactersAcceptedAndFiveRejectedForChangeAndRecovery()
    {
        using var db=new PortableDatabase(new(root));var store=new WorkbookStore(db);var session=store.Login("owner","Owner@123");
        Assert.Throws<BusinessRuleException>(()=>store.ChangePassword(session,"Owner@123","12345"));
        store.ChangePassword(session,"Owner@123","123456");session=store.Login("owner","123456");
        var code=store.GenerateRecoveryCode(session);
        Assert.Throws<BusinessRuleException>(()=>store.RecoverPassword("owner",code,"12345"));
        store.RecoverPassword("owner",code,"654321");Assert.Equal("Owner",store.Login("owner","654321").Role);
    }
}
