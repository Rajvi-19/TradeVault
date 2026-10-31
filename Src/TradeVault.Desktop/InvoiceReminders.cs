using System.Windows;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal sealed record InvoiceReminder(string Party,string Invoice,DateOnly? Due,decimal? Outstanding,int? OverdueDays,string Status);

public sealed partial class MainWindow
{
    private List<InvoiceReminder> CurrentReminders()
    {
        var visible=grid?.Table.Items.Cast<GridRow>().Where(row=>row["_blank"]!="1").Select(row=>row["_id"]).ToHashSet()??[];
        var result=new List<InvoiceReminder>();var date=DateOnly.FromDateTime(DateTime.Today);
        foreach(var party in store.GetParties().Where(party=>allPartiesView||party.Id==controller.PartyId))
        {
            var invoices=workflow.Invoices(party.Id).Where(row=>visible.Contains(ReportWorkflow.InvoiceKey(party.Id,row))).ToList();
            if(invoices.Count==0)continue;
            try
            {
                foreach(var item in workflow.Reminders(party.Id,date))
                {
                    var row=invoices.FirstOrDefault(row=>row.GetValueOrDefault("F9")==item.Invoice);if(row==null)continue;
                    var partyName=string.IsNullOrWhiteSpace(row.GetValueOrDefault("F1"))?party.Name:row["F1"];var invoice=string.IsNullOrWhiteSpace(row.GetValueOrDefault("F3"))?item.Invoice:row["F3"];
                    result.Add(new(partyName,invoice,item.Due,item.Outstanding,item.OverdueDays,item.Status));
                }
            }
            catch(TradeVault.Domain.BusinessRuleException)
            {
                foreach(var row in invoices)result.Add(new(party.Name,row.GetValueOrDefault("F9",row.GetValueOrDefault("F3","")),null,null,null,"Needs validation"));
            }
        }
        return result.OrderBy(row=>row.Party,StringComparer.OrdinalIgnoreCase).ThenBy(row=>row.Invoice,StringComparer.OrdinalIgnoreCase).ToList();
    }
    private void ShowFilteredReminders()
    {
        RequireSaved();WorkflowWindows.Reminders(this,CurrentReminders(),DateOnly.FromDateTime(DateTime.Today));
    }
}
