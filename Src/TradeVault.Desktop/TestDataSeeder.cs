using TradeVault.Infrastructure;
namespace TradeVault.Desktop;

/// <summary>Explicit opt-in only; never called by ordinary application startup.</summary>
internal static class TestDataSeeder
{
    internal static void RunWorkflow(WorkbookStore store,PortableDatabase database)
    {

        {
            var names=new[]{"[TEST] Indigo Textile Imports — Workflow","[TEST] Crescent Dye Traders — Workflow","[TEST] Ocean Colour Distributors — Workflow"};
            var existing=store.GetParties().Select(p=>p.Name).ToHashSet();
            if(names.All(existing.Contains)){Console.WriteLine("Workflow sample parties already exist. No records changed.");return;}
            var backup=store.CreateBackup();Console.WriteLine("Pre-seed backup: "+backup);
            for(int p=0;p<names.Length;p++)
            {
                if(existing.Contains(names[p]))continue;
                var party=store.CreateParty(names[p]);var workflow=new ReportWorkflow(store);workflow.Configure(party.Id,new("F6","F22",true,new(2026,1,1)));
                var c=new WorksheetController(store);c.Load("form",party.Id);c.AsOf=new(2026,10,27);
                decimal factor=1+p*.2m;
                var samples=new[]{
                    new Sample("001",new(2026,6,1),new DateOnly(2026,6,17),15000*factor,10000*factor,0,0,3800,null),
                    new Sample("002",new(2026,6,15),new DateOnly(2026,7,1),24000*factor,16000*factor,0,0,3800,null),
                    new Sample("003",new(2026,8,1),new DateOnly(2026,8,1),30000*factor,20000*factor,0,0,3800,null),
                    new Sample("004",new(2026,10,20),null,12000*factor,8000*factor,0,0,0,null)};
                foreach(var s in samples)
                {
                    var r=c.Add();r["F2"]="Pakistan via Malaysia (TEST)";r["F3"]=$"TEST-PI-W{p+1}-{s.Number}";r["F4"]=s.InvoiceDate.AddDays(-7).ToString("yyyy-MM-dd");r["F9"]=$"TEST-INV-W{p+1}-{s.Number}";r["F10"]=s.InvoiceDate.ToString("yyyy-MM-dd");r["F5"]=Money(s.Original);r["F6"]=Money(s.Contract);
                    r["F7"]=Money(s.Contract);r["F11"]=$"TEST-CN-IN-{p+1}{s.Number}";r["F12"]=$"TEST-BL-IN-{p+1}{s.Number}";r["F13"]=s.InvoiceDate.ToString("yyyy-MM-dd");r["F14"]=s.InvoiceDate.AddDays(7).ToString("yyyy-MM-dd");r["F15"]=$"TEST-CN-MY-{p+1}{s.Number}";r["F16"]=s.BlDate==null?"":$"TEST-BL-MY-{p+1}{s.Number}";r["F17"]=s.BlDate?.ToString("yyyy-MM-dd")??"";r["F18"]=Money(s.Shipping);r["F22"]="0.00";r["F23"]="0.00";r["F25"]="0.00";
                    // TEST arrivals precede the second-leg BL; no shipment identifier claims real transport.
                    if(s.BlDate is {} bl)r["F14"]=bl.ToString("yyyy-MM-dd");
                }
                c.Save();var rows=workflow.Invoices(party.Id);string Key(int i)=>ReportWorkflow.InvoiceKey(party.Id,rows[i]);
                workflow.RecordReceipt(party.Id,new(2026,9,10),samples[0].Original-samples[0].Shipping,"TEST fully settled invoice",[new(Key(0),"Bank",samples[0].Contract),new(Key(0),"Difference",samples[0].Original-samples[0].Contract-samples[0].Shipping)]);
                workflow.RecordReceipt(party.Id,new(2026,10,10),5000,"TEST partial receipt",[new(Key(1),"Bank",4500),new(Key(1),"Difference",500)]);
                workflow.RecordReceipt(party.Id,new(2026,10,20),2000,"TEST shared receipt plus advance",[new(Key(1),"Bank",1500),new(Key(2),"Bank",400)]);
                var report=workflow.Build(party.Id,new(2026,10,27),true);
                if(report.Tables[0].Rows.Count!=4||workflow.Reminders(party.Id,new(2026,10,27)).Select(r=>r.Status).Distinct().Count()!=4)throw new InvalidOperationException("Sample workflow verification failed.");
                Console.WriteLine($"Added {names[p]}: 4 invoices, 3 receipts, USD 100 unallocated; closing balance {report.Tables[1].Totals["E"]:0.00}.");
            }
        }
    }
    internal static void Run(WorkbookStore store,PortableDatabase database)
    {

        var parties=new[]{("[TEST] Meridian Textile Trading","Malaysia"),("[TEST] Bengal Colour Mills","Bangladesh"),("[TEST] Gulf Fabric Industries","United Arab Emirates")};
        {
            if(store.GetParties().Any(p=>parties.Any(sample=>sample.Item1==p.Name)))throw new InvalidOperationException("Test parties already exist. Nothing was inserted; this prevents duplicate test records.");
            var backup=database.CreateBackup();
            for(int p=0;p<parties.Length;p++)
            {
                var party=store.CreateParty(parties[p].Item1);
                var factor=1+p*0.15m;
                var samples=new[]{
                    new Sample("001",new(2026,5,13),new DateOnly(2026,6,17),51680m*factor,40800m*factor,40800m*factor,(51680m-40800m)*factor-3800m,3800m,new DateOnly(2026,9,10)),
                    new Sample("002",new(2026,6,6),new DateOnly(2026,8,1),74845m*factor,50250.50m*factor,20000m,2000m,3800m,new DateOnly(2026,10,12)),
                    new Sample("003",new(2026,7,29),new DateOnly(2026,10,7),61200m*factor,40800m*factor,0m,0m,3800m,null),
                    new Sample("004",new(2026,10,20),null,88957.50m*factor,52391.25m*factor,0m,0m,4200m,null)
                };
                var sheet=new WorksheetController(store);sheet.Load("form",party.Id);sheet.AsOf=new(2026,10,27);
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
    }
    private static string Money(decimal value)=>decimal.Round(value,2,MidpointRounding.AwayFromZero).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
    private sealed record Sample(string Number,DateOnly InvoiceDate,DateOnly? BlDate,decimal Original,decimal Contract,decimal BankReceived,decimal DifferenceReceived,decimal Shipping,DateOnly? PaymentDate);
}
