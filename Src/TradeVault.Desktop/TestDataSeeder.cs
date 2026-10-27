using TradeVault.Infrastructure;
namespace TradeVault.Desktop;

/// <summary>Explicit opt-in only; never called by ordinary application startup.</summary>
internal static class TestDataSeeder
{
    internal static void Run(WorkbookStore store,PortableDatabase database)
    {
        var session=store.Login("owner","Owner@123");
        var parties=new[]{("[TEST] Meridian Textile Trading","Malaysia"),("[TEST] Bengal Colour Mills","Bangladesh"),("[TEST] Gulf Fabric Industries","United Arab Emirates")};
        try
        {
            if(store.GetParties(session).Any(p=>parties.Any(sample=>sample.Item1==p.Name)))throw new InvalidOperationException("Test parties already exist. Nothing was inserted; this prevents duplicate test records.");
            var backup=database.CreateBackup();
            for(int p=0;p<parties.Length;p++)
            {
                var party=store.CreateParty(session,parties[p].Item1);
                var factor=1+p*0.15m;
                var samples=new[]{
                    new Sample("001",new(2026,5,13),new DateOnly(2026,6,17),51680m*factor,40800m*factor,40800m*factor,(51680m-40800m)*factor-3800m,3800m,new DateOnly(2026,9,10)),
                    new Sample("002",new(2026,6,6),new DateOnly(2026,8,1),74845m*factor,50250.50m*factor,20000m,2000m,3800m,new DateOnly(2026,10,12)),
                    new Sample("003",new(2026,7,29),new DateOnly(2026,10,7),61200m*factor,40800m*factor,0m,0m,3800m,null),
                    new Sample("004",new(2026,10,20),null,88957.50m*factor,52391.25m*factor,0m,0m,4200m,null)
                };
                var sheet=new WorksheetController(store,session);sheet.Load("form",party.Id);sheet.AsOf=new(2026,10,27);
                foreach(var sample in samples)
                {
                    var row=sheet.Add();var reference=$"TEST-EXP-{p+1}-{sample.Number}/26-27";
                    row["F2"]=parties[p].Item2;row["F3"]=$"TEST-PI-{p+1}-{sample.Number}/26-27";row["F4"]=sample.InvoiceDate.AddDays(-14).ToString("yyyy-MM-dd");
                    row["F5"]=Money(sample.Original);row["F6"]=Money(sample.Contract);row["F7"]=Money(sample.Contract);row["F8"]=Money(sample.Original-sample.Contract);
                    row["F9"]=reference;row["F10"]=sample.InvoiceDate.ToString("yyyy-MM-dd");row["F11"]=$"TEST-IN-{p+1}{sample.Number}";row["F12"]=$"TEST-BLIN-{p+1}{sample.Number}";
                    row["F13"]=sample.InvoiceDate.AddDays(7).ToString("yyyy-MM-dd");row["F14"]=sample.InvoiceDate.AddDays(20).ToString("yyyy-MM-dd");
                    row["F15"]=$"TEST-MY-{p+1}{sample.Number}";row["F16"]=sample.BlDate is null?"":$"TEST-BLMY-{p+1}{sample.Number}";row["F17"]=sample.BlDate?.ToString("yyyy-MM-dd")??"";
                    row["F18"]=Money(sample.Shipping);row["F22"]=Money(sample.BankReceived);row["F23"]=Money(sample.DifferenceReceived);row["F24"]=sample.PaymentDate?.ToString("yyyy-MM-dd")??"";
                    row["F25"]=Money(sample.BankReceived);row["F26"]=sample.PaymentDate?.ToString("yyyy-MM-dd")??"";
                }
                sheet.Save();sheet.Load("details",party.Id);sheet.AsOf=new(2026,10,27);
                foreach(var sample in samples)
                {
                    var row=sheet.Add();row["B"]=$"TEST-EXP-{p+1}-{sample.Number}/26-27 DTD {sample.InvoiceDate:dd-MM-yyyy}";
                    row["C"]=Money(sample.Original);row["D"]=Money(sample.Contract);row["E"]=Money(sample.BankReceived);row["H"]=Money(sample.Shipping);row["J"]=Money(sample.DifferenceReceived);row["L"]=sample.BlDate?.ToString("yyyy-MM-dd")??"";
                }
                sheet.Save();sheet.Load("ledger",party.Id);sheet.Opening=0;sheet.AsOf=new(2026,10,27);
                foreach(var sample in samples)
                {
                    var invoice=sheet.Add();invoice["B"]=$"TEST-EXP-{p+1}-{sample.Number}/26-27 DTD {sample.InvoiceDate:dd-MM-yyyy}";invoice["C"]=Money(sample.Original);invoice["H"]=Money(sample.Contract);invoice["I"]=sample.BlDate?.ToString("yyyy-MM-dd")??"";
                    var charge=sheet.Add();charge["B"]=$"TEST shipping deduction / invoice {sample.Number}";charge["D"]=Money(sample.Shipping);
                    if(sample.BankReceived>0)
                    {
                        var parts=sample.Number=="002"?2:1;
                        for(int n=0;n<parts;n++){var payment=sheet.Add();payment["B"]=$"TEST bank receipt {n+1}/{parts} / invoice {sample.Number} / {sample.PaymentDate:dd-MM-yyyy}";payment["D"]=Money(sample.BankReceived/parts);}
                    }
                    if(sample.DifferenceReceived>0){var receipt=sheet.Add();receipt["B"]=$"TEST difference receipt / invoice {sample.Number} / {sample.PaymentDate:dd-MM-yyyy}";receipt["D"]=Money(sample.DifferenceReceived);}
                }
                sheet.Save();
            }
            Console.WriteLine($"Inserted 3 TEST parties, 12 invoices, 12 detail rows and 39 ledger rows. Existing parties untouched. Backup: {backup}");
        }
        finally{store.Logout(session);}
    }
    private static string Money(decimal value)=>decimal.Round(value,2,MidpointRounding.AwayFromZero).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
    private sealed record Sample(string Number,DateOnly InvoiceDate,DateOnly? BlDate,decimal Original,decimal Contract,decimal BankReceived,decimal DifferenceReceived,decimal Shipping,DateOnly? PaymentDate);
}
