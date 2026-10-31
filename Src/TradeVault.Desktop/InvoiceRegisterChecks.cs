using System.Windows.Controls;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

internal static class InvoiceRegisterChecks
{
    internal static void Run(WorkbookStore store)
    {
        static void Check(bool pass,string message){if(!pass)throw new InvalidOperationException(message);}
        store.CreateAgent("Agent");store.CreateAgent("Synthetic agent");
        var modalParty=store.CreateParty("SYNTHETIC modal checks");
        var dialog=new NewInvoiceDialog(store,modalParty.Id,0){WindowStartupLocation=System.Windows.WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};
        Exception? modalFailure=null;
        dialog.Loaded+=(_,_)=>
        {
            try
            {
                dialog.FillForCheck(new("","Agent","PI-MODAL",new(2026,5,1)));dialog.SubmitForCheck();
                Check(dialog.HasValidationError&&dialog.CreatedId==null&&store.Load("form",modalParty.Id).Rows.Count==0,"Invalid modal submission must remain open without storing a record.");
                dialog.FillForCheck(new("India","Unknown agent","PI-MODAL",new(2026,5,1)));dialog.SubmitForCheck();Check(dialog.HasValidationError&&dialog.CreatedId==null,"An unlisted agent must not be accepted by the dropdown.");
                dialog.FillForCheck(new("India","Agent","PI-MODAL",new(2026,5,1)));Check(dialog.SelectedAgentTextForCheck()=="Agent","Selected agent must render its name without record IDs or metadata.");dialog.SubmitForCheck();
            }
            catch(Exception ex){modalFailure=ex;dialog.Close();}
        };
        var modalResult=dialog.ShowDialog();if(modalFailure!=null)throw modalFailure;
        Check(modalResult==true&&store.Load("form",modalParty.Id).Rows.Count==1,"Add record must create exactly one invoice and close its modal.");
        var cancel=new NewInvoiceDialog(store,modalParty.Id,1){WindowStartupLocation=System.Windows.WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};
        cancel.Loaded+=(_,_)=>cancel.Close();cancel.ShowDialog();Check(store.Load("form",modalParty.Id).Rows.Count==1,"Closing the modal must not create another invoice.");
        var otherParty=store.CreateParty("SYNTHETIC modal other party");
        var chooseParty=new NewInvoiceDialog(store,modalParty.Id,1){WindowStartupLocation=System.Windows.WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};
        chooseParty.Loaded+=(_,_)=>{chooseParty.SelectPartyForCheck(otherParty.Id);chooseParty.FillForCheck(new("India","Agent","OTHER-PI",new(2026,5,1)));chooseParty.SubmitForCheck();};
        Check(chooseParty.ShowDialog()==true&&chooseParty.CreatedPartyId==otherParty.Id&&store.Load("form",otherParty.Id).Rows.Single()["F1"]==otherParty.Name,"Party selected in New Invoice must determine ownership and the locked Party name value.");
        var party=store.CreateParty("SYNTHETIC invoice identity checks");
        store.CreateRegisterInvoice(party.Id,0,new("India","Synthetic agent","SYNTHETIC-PI",new(2026,5,1)));
        var controller=new WorksheetController(store);controller.Load("form",party.Id);var row=controller.Rows.Single();
        var fields=controller.Fields.ToArray();
        Check(fields.Select(f=>f.Key).SequenceEqual(RegisterInvoiceSchema.VisibleKeys),"Register columns are in the requested 21-column order.");
        var grid=new ExcelGrid(fields,controller.Rows,frozenColumns:5);
        Check(grid.Table.Columns.Count==21&&grid.Table.FrozenColumnCount==5&&grid.Table.Columns.Take(5).All(c=>c.IsReadOnly),"First five identity columns must be locked and frozen.");
        Check(grid.Table.Columns.Take(5).All(c=>c.HeaderStyle!=null&&c.CellStyle.Setters.OfType<System.Windows.Setter>().Any(setter=>setter.Property==Control.BackgroundProperty)),"Locked identity columns must have distinct header and cell backgrounds.");
        Check(!grid.Table.Columns[18].IsReadOnly&&grid.Table.Columns[19].IsReadOnly&&grid.Table.Columns[20].IsReadOnly,"Due date is editable; reminder and overdue are calculated.");
        grid.Table.CurrentCell=new(row,grid.Table.Columns[0]);Check(!grid.Paste("Changed country"),"Pasting into identity must be rejected.");
        Check(row["F2"]=="India","Identity was modified by rejected paste.");
        row["F9"]="SYNTHETIC-INV";row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F7"]="9000";row["F17"]="2026-06-17";
        controller.Calculate();Check(row["F19"]=="2026-09-15","Default deadline must be BL plus 90.");
        row["F19"]="2026-10-01";controller.Save();controller.Load("form",party.Id);row=controller.Rows.Single();
        Check(row["F19"]=="2026-10-01"&&row["F20"]=="2026-09-01","Custom deadline and reminder must survive reload.");
        var state=controller.CaptureEditState();row["F19"]="2026-10-15";controller.Calculate();controller.RestoreEditState(state,controller.Revision);row=controller.Rows.Single();
        Check(row["F19"]=="2026-10-01","Undo state must preserve an explicit deadline.");
        row["F17"]="2026-07-01";controller.Calculate();Check(row["F19"]=="2026-10-01","BL edits must preserve a custom deadline.");
        row["F19"]="";controller.Save();controller.Load("form",party.Id);row=controller.Rows.Single();
        Check(row["F19"]=="2026-09-29"&&row["_dueOverride"]=="","Clearing the override must restore the automatic deadline.");
    }
}
