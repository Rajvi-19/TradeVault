using System.Reflection;
using System.IO;
using System.Text.Json;
using TradeVault.Desktop;
using TradeVault.Domain;
using TradeVault.Infrastructure;

internal static class Program
{
    private sealed record Finding(string Id,string Status,string Scenario,string Observation);
    [STAThread]
    static int Main(string[] args)
    {
        var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        var root=Path.Combine(AppContext.BaseDirectory,"AuditData",Guid.NewGuid().ToString("N"));
        var findings=new List<Finding>();
        void Check(string id,string scenario,Func<(bool Ok,string Observation)> test,bool gap=false)
        {
            try{var result=test();findings.Add(new(id,result.Ok?(gap?"GAP CONFIRMED":"PASS"):"FAIL",scenario,result.Observation));}
            catch(Exception ex){findings.Add(new(id,"FAIL",scenario,ex.GetBaseException().Message));}
        }
        using var database=new PortableDatabase(new(root));var store=new WorkbookStore(database);var session=store.Login("owner","Owner@123");
        Check("P01","Seed three named parties in isolated database",()=>
        {
            var type=typeof(MainWindow).Assembly.GetType("TradeVault.Desktop.TestDataSeeder")!;
            type.GetMethod("Run",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[store,database]);
            var parties=store.GetParties(session).Where(p=>!p.IsUnassigned).ToArray();
            int invoices=0,details=0,ledger=0;
            foreach(var p in parties){invoices+=store.Load(session,"form",p.Id).Rows.Count-1;details+=store.Load(session,"details",p.Id).Rows.Count-1;ledger+=store.Load(session,"ledger",p.Id).Rows.Count-1;}
            return(parties.Length==3&&invoices==12&&details==12&&ledger==39,$"Named parties={parties.Length}; invoices={invoices}; details={details}; ledger={ledger}. Metadata excluded. Pre-seed backup exists={Directory.GetFiles(database.Paths.Backups,"*.db").Length>0}.");
        });
        WorksheetController Fresh(string label)
        {var p=store.CreateParty(session,"[AUDIT] "+label);var controller=new WorksheetController(store,session);controller.Load("form",p.Id);return controller;}
        GridRow Invoice(WorksheetController c,string number)
        {var row=c.Add();row["F9"]=number;row["F5"]="15000";row["F6"]="10000";row["F7"]="10000";row["F8"]="5000";row["F17"]="2026-06-17";return row;}
        Check("P02","Duplicate invoice numbers in one party's worksheet",()=>
        {var c=Fresh("Duplicate references");Invoice(c,"AUDIT-DUP-1");Invoice(c,"AUDIT-DUP-1");c.Save();c.Load("form");return(c.Rows.Count==2&&c.Rows.All(r=>r["F9"]=="AUDIT-DUP-1"),$"Saved rows with same invoice number={c.Rows.Count}. No worksheet duplicate guard.");},true);
        Check("P03","Overdue status after all sample receivable components received",()=>
        {var c=Fresh("Settled invoice");var row=Invoice(c,"AUDIT-SETTLED");row["F22"]="10000";row["F25"]="10000";row["F23"]="1200";c.AsOf=new(2026,10,26);c.Save();return(row["F21"]=="41",$"Bank contract=10000; bank received=10000; net difference=1200; difference received=1200; displayed overdue days={row["F21"]}. Worksheet overdue is calendar-based, not settlement-aware.");},true);
        Check("P04","As-of date with later shipment/ledger row",()=>
        {var c=Fresh("Historical cutoff");c.Load("ledger");var row=c.Add();row["B"]="AUDIT future shipment / 2027-01-01";row["C"]="100";row["I"]="2027-01-01";c.AsOf=new(2026,10,1);c.Save();return(row["E"]=="100.00",$"As-of=2026-10-01; BL=2027-01-01; running balance={row["E"]}. No historical filtering in worksheet calculation.");},true);
        Check("P05","Input-register terms affect every invoice",()=>
        {var c=Fresh("Terms scope");var a=Invoice(c,"AUDIT-TERMS-1");var b=Invoice(c,"AUDIT-TERMS-2");c.Terms=60;c.Calculate();return(a["F19"]=="2026-08-16"&&b["F19"]=="2026-08-16",$"Changing worksheet terms to60 changes both due dates to {a["F19"]}; no per-row terms input in the register.");},true);
        Check("P06","Party mismatch blocks UI-controller save",()=>
        {var c=Fresh("Ownership");var row=Invoice(c,"AUDIT-OWNER");row["F1"]="Different party";try{c.Save();return(false,"Mismatched party unexpectedly saved.");}catch(BusinessRuleException){return(true,"Controller rejected a mismatched party name.");}});
        Check("P07","Separate application roots remain independent",()=>
        {using var other=new PortableDatabase(new(Path.Combine(root,"IndependentUSB")));var alternate=new WorkbookStore(other);var op=alternate.Login("operator","Operator@123");return(alternate.GetParties(op).Count==1&&alternate.Load(op,"form").Rows.Count==0,"Second isolated application root contains only Unassigned records and no first-database rows.");});
        Check("P08","Partial receipt fixture retains pending amount",()=>
        {var p=store.GetParties(session).Single(p=>p.Name=="[TEST] Meridian Textile Trading");var c=new WorksheetController(store,session);c.Load("details",p.Id);return(c.Rows[1]["F"]=="30250.50",$"Second invoice pending bank={c.Rows[1]["F"]}; expected30250.50 after20000 received.");});
        File.WriteAllText(Path.Combine(output,"audit-probes.json"),JsonSerializer.Serialize(new{DatasetRoot=root,Findings=findings},new JsonSerializerOptions{WriteIndented=true}));
        foreach(var item in findings)Console.WriteLine($"{item.Id} | {item.Status} | {item.Scenario} | {item.Observation}");
        return findings.Any(f=>f.Status=="FAIL")?1:0;
    }
}
