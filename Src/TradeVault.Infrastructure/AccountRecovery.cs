using System.Security.Cryptography;
using TradeVault.Domain;
namespace TradeVault.Infrastructure;
public sealed partial class WorkbookStore
{
    public bool HasRecoveryCode(LoginSession session)
    {
        CheckSession(session);using var c=database.Open();using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM account_recovery WHERE username=$u AND hash IS NOT NULL";cmd.Parameters.AddWithValue("$u",session.Username);return Convert.ToInt32(cmd.ExecuteScalar())==1;
    }
    public string GenerateRecoveryCode(LoginSession session)
    {
        CheckSession(session);var raw=Convert.ToHexString(RandomNumberGenerator.GetBytes(20));var salt=RandomNumberGenerator.GetBytes(16);
        var hash=Rfc2898DeriveBytes.Pbkdf2(raw,salt,600000,HashAlgorithmName.SHA256,32);
        using var c=database.Open();using var t=c.BeginTransaction();using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText="INSERT INTO account_recovery(username,salt,hash) VALUES($u,$s,$h) ON CONFLICT(username) DO UPDATE SET salt=$s,hash=$h,failures=0,blocked_until=NULL";
        cmd.Parameters.AddWithValue("$u",session.Username);cmd.Parameters.AddWithValue("$s",salt);cmd.Parameters.AddWithValue("$h",hash);cmd.ExecuteNonQuery();
        Log(c,t,session.Username,"RecoveryCodeRotated");t.Commit();
        return string.Join("-",Enumerable.Range(0,8).Select(i=>raw.Substring(i*5,5)));
    }
    public void RecoverPassword(string username,string recoveryCode,string replacement)
    {
        if(replacement.Length is <6 or >128)throw new BusinessRuleException("Use 6–128 characters for the new password.");
        var normalized=recoveryCode.Replace("-","").Replace(" ","").Trim().ToUpperInvariant();
        using var c=database.Open();using var t=c.BeginTransaction();using var cmd=c.CreateCommand();cmd.Transaction=t;
        cmd.CommandText="SELECT salt,hash,failures,blocked_until FROM account_recovery WHERE username=$u COLLATE BINARY";cmd.Parameters.AddWithValue("$u",username.Trim());
        byte[] salt=new byte[16],hash=new byte[32];int failures=0;bool exists=false,validHash=false;string? blocked=null;
        using(var r=cmd.ExecuteReader())if(r.Read()){exists=true;validHash=!r.IsDBNull(1);if(validHash){salt=(byte[])r[0];hash=(byte[])r[1];}failures=r.GetInt32(2);blocked=r.IsDBNull(3)?null:r.GetString(3);}
        if(blocked!=null&&DateTimeOffset.Parse(blocked)>DateTimeOffset.UtcNow)throw new BusinessRuleException("Too many recovery attempts. Try again after 30 seconds.");
        var candidate=Rfc2898DeriveBytes.Pbkdf2(normalized,salt,600000,HashAlgorithmName.SHA256,32);
        if(!validHash||!CryptographicOperations.FixedTimeEquals(hash,candidate))
        {
            if(exists){cmd.CommandText="UPDATE account_recovery SET failures=$f,blocked_until=$b WHERE username=$u";cmd.Parameters.AddWithValue("$f",(failures+1)%5);cmd.Parameters.AddWithValue("$b",failures>=4?(object)DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"):DBNull.Value);cmd.ExecuteNonQuery();}
            t.Commit();throw new BusinessRuleException("Username or recovery code is incorrect, used, or not set up.");
        }
        var passwordSalt=RandomNumberGenerator.GetBytes(16);var passwordHash=Rfc2898DeriveBytes.Pbkdf2(replacement,passwordSalt,600000,HashAlgorithmName.SHA256,32);
        cmd.CommandText="UPDATE credentials SET salt=$s,hash=$h WHERE username=$u; UPDATE account_recovery SET salt=NULL,hash=NULL,failures=0,blocked_until=NULL WHERE username=$u";
        cmd.Parameters.AddWithValue("$s",passwordSalt);cmd.Parameters.AddWithValue("$h",passwordHash);cmd.ExecuteNonQuery();
        Log(c,t,username.Trim(),"PasswordRecovered");t.Commit();
        foreach(var token in sessions.Where(p=>p.Value.Username==username.Trim()).Select(p=>p.Key).ToArray())sessions.Remove(token);
    }
}
