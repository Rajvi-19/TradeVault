using System.Globalization;
using System.IO;
using System.Text.Json;
using TradeVault.Application;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;

/// <summary>Explicit creation of a separate workbook-derived test database only.</summary>
internal static class ExcelDemoSeeder
{
    internal sealed record Entry(int SourceRow,string Reference,string InvoiceDate,bool AssumedInvoiceDate,string BlDate,string Original,string Contract,string Bank,string Shipping,string DifferenceReceived,string Terms);
    internal static void Run(WorkbookStore store,string fixture)
    {
        var entries=JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(fixture))??throw new InvalidOperationException("Missing fixture.");
        if(entries.Count!=14)throw new InvalidOperationException("Expected 14 workbook invoice rows.");

        {
            if(store.GetParties().Count!=1||store.Load("form").Rows.Count!=0)throw new InvalidOperationException("Only a new empty test database can be seeded.");
            var workflow=new ReportWorkflow(store);
            foreach(var lab in new[]{false,true})
            {
                var party=store.CreateParty(lab?"[TEST] Receipt allocation practice":"[TEST] Account Details workbook sample");
                workflow.Configure(party.Id,new("F6","F22",true,new(2026,1,1)));
                var sheet=new WorksheetController(store);sheet.Load("form",party.Id);sheet.AsOf=new(2026,10,27);
                foreach(var entry in lab?entries.Skip(6).Take(2):entries)
                {
                    var row=sheet.Add();row["F2"]="Malaysia / Pakistan (TEST)";row["F3"]="DEMO-PI-"+entry.SourceRow;
                    row["F5"]=entry.Original;row["F6"]=entry.Contract; // Workbook D explicitly supplies the configured contract. F7 remains unknown.
                    row["F9"]=entry.Reference;row["F10"]=entry.InvoiceDate;row["F17"]=entry.BlDate;row["F18"]=entry.Shipping;row["_terms"]=entry.Terms;
                    row["F22"]=lab?"0.00":entry.Bank;row["F23"]=lab?"0.00":entry.DifferenceReceived;
                    var assumed=DateOnly.ParseExact(entry.InvoiceDate,"yyyy-MM-dd",CultureInfo.InvariantCulture);
                    if(DateOnly.TryParseExact(entry.BlDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var bl)&&bl>assumed)assumed=bl;
                    row["F24"]=assumed.AddDays(1).ToString("yyyy-MM-dd"); // Synthetic payment date, not supplied by workbook.
                }
                sheet.Save();
                if(lab)
                {
                    var invoices=workflow.Invoices(party.Id);var first=ReportWorkflow.InvoiceKey(party.Id,invoices[0]);var second=ReportWorkflow.InvoiceKey(party.Id,invoices[1]);
                    workflow.RecordReceipt(party.Id,new(2026,10,20),1500,"SYNTHETIC shared receipt + advance",[new(first,"Bank",800),new(second,"Bank",400),new(first,"Difference",50)]);
                    workflow.RecordReceipt(party.Id,new(2026,10,22),500,"SYNTHETIC second partial receipt",[new(first,"Bank",500)]);
                }
                else
                {
                    sheet.Load("details",party.Id);sheet.AsOf=new(2026,10,27);
                    foreach(var entry in entries)
                    {
                        var row=sheet.Add();row["B"]=entry.Reference+" / source row "+entry.SourceRow;row["C"]=entry.Original;row["D"]=entry.Contract;row["E"]=entry.Bank;row["H"]=entry.Shipping;row["J"]=entry.DifferenceReceived;row["L"]=entry.BlDate;row["M"]=entry.Terms;
                        if(entry.SourceRow==7){var rounding=sheet.Add();rounding["B"]="SOURCE round-off row 8 (legacy only)";rounding["C"]="0";rounding["D"]="0";rounding["H"]="0";rounding["J"]="0.25";}
                    }
                    sheet.Save();
                }
                var report=workflow.Build(party.Id,new(2026,10,27),true);
                if(report.Tables[0].Rows.Count!=(lab?2:14))throw new InvalidOperationException("Demo report count mismatch.");
                workflow.Export(party.Id,new(2026,10,27),true);
            }
            Console.WriteLine("PASS: 14 workbook-derived invoices + 2 practice invoices; generated reports, 2 split receipts and preserved legacy round-off. Dates identified as synthetic in DEMO-READ-ME.md.");
        }
    }
}
