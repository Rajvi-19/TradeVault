using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

public sealed partial class MainWindow
{
    private void LoadTransferReceiptPage(string page)
    {
        var party=page=="partyReceipt";
        var rows=new ObservableCollection<GridRow>();
        foreach(var entry in store.GetTransferReceipts(party?TransferReceiptKind.Party:TransferReceiptKind.Agent))
        {
            var row=new GridRow();row["_id"]=entry.Id.ToString();row["A"]=entry.Name;row["B"]=entry.PaymentDate.ToString("yyyy-MM-dd");row["C"]=entry.UsdAmount.ToString("0.00",CultureInfo.InvariantCulture);row["D"]=entry.Description;row["E"]=store.GetAgents().FirstOrDefault(a=>a.Id==entry.ReceivingAgentId)?.Name??"Select receiving agent";row["R"]=entry.UsdValueDisplay;rows.Add(row);
        }
        Field[] fields=party?[new("A","Party name",Width:230),new("E","Receiving agent",Width:230),new("B","Payment date",Width:140),new("C","Receipt amount / USD",Width:170),new("R","USD value",Width:230,DataType:"text"),new("D","Reference / description",Width:340)]:[new("A","Agent name",Width:250),new("B","Payment date",Width:160),new("C","Receipt amount (USD)",Width:200),new("R","USD value",Width:230,DataType:"text"),new("D","Reference / description",Width:440)];
        grid=new ExcelGrid(fields,rows,true,frozenColumns:1);
        grid.Table.Columns[^1].Width=new System.Windows.Controls.DataGridLength(1,System.Windows.Controls.DataGridLengthUnitType.Star);
        grid.Table.Columns[^1].MinWidth=party?340:440;
        sheetArea.Children.Add(grid);
    }
    private void CreateTransferReceipt()
    {
        if(activePage is not("partyReceipt" or "agentReceipt"))return;
        var page=activePage;var dialog=new TransferReceiptDialog(store,page=="partyReceipt"?TransferReceiptKind.Party:TransferReceiptKind.Agent){Owner=this};
        if(dialog.ShowDialog()!=true)return;
        OpenPage(page);status.Text="Receipt saved";
    }
    internal void CheckTransferReceiptPages()
    {
        void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        var party=store.CreateParty("SYNTHETIC receipt buyer");var agent=store.CreateAgent("SYNTHETIC receipt intermediary");
        foreach(var kind in new[]{TransferReceiptKind.Party,TransferReceiptKind.Agent})
        {
            var id=kind==TransferReceiptKind.Party?party.Id.ToString():agent.Id;
            var count=store.GetTransferReceipts(kind).Count;
            var dialog=new TransferReceiptDialog(store,kind){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};
            Check(dialog.DefaultDate==DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),"Receipt default date must be today.");
            Check(dialog.AmountIsLeftAligned,"USD input must be left aligned in both receipt forms.");
            Exception? failure=null;
            dialog.Loaded+=(_,_)=>
            {
                try
                {
                    dialog.FillForCheck("unknown","2026-10-15","1250.50","Wire transfer");dialog.SubmitForCheck();Check(dialog.HasError&&dialog.Result==null,"Unknown receipt master must be rejected.");
                    dialog.FillForCheck(id,"54-04-2026","1250.50","Wire transfer");dialog.SubmitForCheck();Check(dialog.HasError&&dialog.Result==null,"Invalid calendar dates must be rejected.");
                    dialog.FillForCheck(id,"2026-10-15","-1","Wire transfer");dialog.SubmitForCheck();Check(dialog.HasError&&dialog.Result==null,"Negative amounts must be rejected.");
                    dialog.FillForCheck(id,"2026-10-15","1250.50","Wire transfer",agent.Id,"280 PKR");Check(dialog.SelectedNameForCheck()==(kind==TransferReceiptKind.Party?party.Name:agent.Name),"Receipt dropdown must render only the master name.");dialog.SubmitForCheck();
                }
                catch(Exception ex){failure=ex;dialog.Close();}
            };
            var result=dialog.ShowDialog();if(failure!=null)throw failure;
            Check(result==true&&store.GetTransferReceipts(kind).Count==count+1,"Receipt submit must persist one entry.");
            var cancel=new TransferReceiptDialog(store,kind){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};cancel.Loaded+=(_,_)=>cancel.Close();cancel.ShowDialog();Check(store.GetTransferReceipts(kind).Count==count+1,"Cancel must not save a receipt.");
            var page=kind==TransferReceiptKind.Party?"partyReceipt":"agentReceipt";OpenPage(page);
            Check(grid!=null&&grid.Table.IsReadOnly&&grid.Table.Items.Cast<GridRow>().Any(row=>row["A"]==(kind==TransferReceiptKind.Party?party.Name:agent.Name)&&row["C"]=="1250.50"),"Saved receipt must appear in the correct table.");
            ReloadSaved();Check(grid!.Table.Items.Count==count+1,"Reload must retain receipts.");
            Check(dialog.Result!.UsdValue=="280 PKR"&&grid.Table.Items.Cast<GridRow>().Any(r=>r["R"]=="280 PKR")&&BuildPageExport().Tables[0].Columns.Single(c=>c.Key=="R").Type=="text","Both receipt pages must retain USD value as text after save/reload and in exports.");
            Check(grid.Table.Columns.Count(c=>c.Header?.ToString()=="USD value")==1,"Each receipt table must show exactly one separate USD value column.");
            if(kind==TransferReceiptKind.Party)Check(!grid.Table.Columns.Any(c=>c.Header?.ToString()?.Contains("Allocated",StringComparison.OrdinalIgnoreCase)==true||c.Header?.ToString()?.Contains("Unallocated",StringComparison.OrdinalIgnoreCase)==true),"Party receipt must show stored USD value instead of allocation columns.");
            var export=BuildPageExport();Check(export.Tables.Single().Rows.Count==count+1&&export.Tables[0].Columns.Single(c=>c.Key=="C").Type=="number","Receipt export must retain numeric USD values.");
        }
        Navigate("form");
    }
}
