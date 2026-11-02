using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using TradeVault.Infrastructure;

namespace TradeVault.Desktop;

public sealed partial class MainWindow
{
    private string lastReceiptPage="partyReceipt";
    private void LoadTransferReceiptPage(string page)
    {
        var party=page=="partyReceipt";
        var rows=new ObservableCollection<GridRow>();
        foreach(var entry in page=="expenseReceipt"?Array.Empty<TransferReceipt>():store.GetTransferReceipts(party?TransferReceiptKind.Party:TransferReceiptKind.Agent))
        {
            var row=new GridRow();row["_id"]=entry.Id.ToString();row["A"]=entry.Name;row["B"]=entry.PaymentDate.ToString("yyyy-MM-dd");row["C"]=entry.UsdAmount.ToString("0.00",CultureInfo.InvariantCulture);row["D"]=entry.Description;row["E"]=store.GetAgents().FirstOrDefault(a=>a.Id==entry.ReceivingAgentId)?.Name??"Select receiving agent";row["R"]=entry.UsdValueDisplay;row["T"]=entry.BankTransferCharge?.ToString("0.00",CultureInfo.InvariantCulture)??"";rows.Add(row);
        }
        Field[] fields=party?[new("A","Party name",Width:230),new("E","Receiving agent",Width:230),new("B","Payment date",Width:140),new("C","Receipt amount / USD",Width:170),new("T","Bank transfer charge / USD",Width:190,DataType:"number"),new("R","USD rate",Width:230,DataType:"text"),new("D","Reference / description",Width:340)]:[new("A","Agent name",Width:250),new("B","Payment date",Width:160),new("C","Receipt amount (USD)",Width:200),new("R","USD rate",Width:230,DataType:"text"),new("D","Reference / description",Width:440)];
        if(page=="expenseReceipt")
        {
            var parties=store.GetParties().ToDictionary(p=>p.Id,p=>p.Name);var agents=store.GetAgents().ToDictionary(a=>a.Id,a=>a.Name);
            foreach(var expense in store.GetExpenseReceipts())
            {
                var row=new GridRow();row["A"]=parties.GetValueOrDefault(expense.PartyId,"Missing party");row["I"]=store.GetExpenseInvoices(expense.PartyId).FirstOrDefault(i=>i.Id==expense.InvoiceId)?.Number??"Missing invoice";row["G"]=agents.GetValueOrDefault(expense.AgentId,"Missing agent");row["B"]=expense.Date.ToString("yyyy-MM-dd");row["C"]=expense.Amount.ToString("0.00",CultureInfo.InvariantCulture);row["D"]=expense.Description;rows.Add(row);
            }
            fields=[new("A","Party name",Width:230),new("I","Proforma invoice",Width:200),new("G","Agent name",Width:220),new("B","Expense date",Width:140,DataType:"date"),new("C","Expense amount / USD",Width:180,DataType:"number"),new("D","Description",Width:340)];
        }
        grid=new ExcelGrid(fields,rows,true,frozenColumns:1);
        grid.Table.Columns[^1].Width=new System.Windows.Controls.DataGridLength(1,System.Windows.Controls.DataGridLengthUnitType.Star);
        grid.Table.Columns[^1].MinWidth=party?340:440;
        var root=new System.Windows.Controls.DockPanel{Margin=new(0,8,0,0)};
        var tabs=new System.Windows.Controls.StackPanel{Orientation=System.Windows.Controls.Orientation.Horizontal,Margin=new(12,8,12,8)};
        foreach(var (key,label) in new[]{("partyReceipt","Party Receipt"),("agentReceipt","Agent Receipt"),("expenseReceipt","Expense Receipt")})
        {
            var button=Ui.Button(label,()=>Run(()=>Navigate(key)),"QuietButton");button.Padding=new(14,7,14,7);button.Margin=new(0,0,6,0);button.FontWeight=FontWeights.SemiBold;button.Background=Ui.Color(page==key?"#E8EFFD":"#FFFFFF");button.Foreground=Ui.Color(page==key?"#2458D3":"#52627A");tabs.Children.Add(button);
        }
        var actions=new System.Windows.Controls.Grid();actions.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});actions.ColumnDefinitions.Add(new(){Width=GridLength.Auto});actions.Children.Add(tabs);
        var add=CompactButton("Add New Receipt",()=>Run(CreateTransferReceipt),"\uE710");add.Margin=new(8,8,12,8);add.VerticalAlignment=VerticalAlignment.Center;System.Windows.Controls.Grid.SetColumn(add,1);actions.Children.Add(add);
        var toolbar=new System.Windows.Controls.Border{Background=Ui.Color("#FFFFFF"),BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(0,0,0,1),Margin=new(0,0,0,8),Child=actions};
        System.Windows.Controls.DockPanel.SetDock(toolbar,System.Windows.Controls.Dock.Top);root.Children.Add(toolbar);
        root.Children.Add(new System.Windows.Controls.Border{Background=Ui.Color("#FFFFFF"),BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(1),Child=grid});sheetArea.Children.Add(root);
    }
    private void CreateTransferReceipt()
    {
        if(activePage=="expenseReceipt"){var expense=new ExpenseReceiptDialog(store){Owner=this};if(expense.ShowDialog()==true){OpenPage("expenseReceipt");status.Text="Expense saved";}return;}
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
            Check(dialog.DefaultBankCharge=="0.00","New receipt bank charge must default to 0.00.");
            Check(dialog.AmountIsLeftAligned,"USD input must be left aligned in both receipt forms.");
            Exception? failure=null;
            dialog.Loaded+=(_,_)=>
            {
                try
                {
                    dialog.FillForCheck("unknown","2026-10-15","1250.50","Wire transfer");dialog.SubmitForCheck();Check(dialog.HasError&&dialog.Result==null,"Unknown receipt master must be rejected.");
                    dialog.FillForCheck(id,"54-04-2026","1250.50","Wire transfer");dialog.SubmitForCheck();Check(dialog.HasError&&dialog.Result==null,"Invalid calendar dates must be rejected.");
                    dialog.FillForCheck(id,"2026-10-15","-1","Wire transfer");dialog.SubmitForCheck();Check(dialog.HasError&&dialog.Result==null,"Negative amounts must be rejected.");
                    dialog.FillForCheck(id,"2026-10-15","1250.50","Wire transfer",agent.Id,"280 PKR",kind==TransferReceiptKind.Party?"10.00":"0.00");Check(dialog.SelectedNameForCheck()==(kind==TransferReceiptKind.Party?party.Name:agent.Name),"Receipt dropdown must render only the master name.");dialog.SubmitForCheck();
                }
                catch(Exception ex){failure=ex;dialog.Close();}
            };
            var result=dialog.ShowDialog();if(failure!=null)throw failure;
            Check(result==true&&store.GetTransferReceipts(kind).Count==count+1,"Receipt submit must persist one entry.");
            var cancel=new TransferReceiptDialog(store,kind){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};cancel.Loaded+=(_,_)=>cancel.Close();cancel.ShowDialog();Check(store.GetTransferReceipts(kind).Count==count+1,"Cancel must not save a receipt.");
            var page=kind==TransferReceiptKind.Party?"partyReceipt":"agentReceipt";OpenPage(page);
            Check(grid!=null&&grid.Table.IsReadOnly&&grid.Table.Items.Cast<GridRow>().Any(row=>row["A"]==(kind==TransferReceiptKind.Party?party.Name:agent.Name)&&row["C"]=="1250.50"),"Saved receipt must appear in the correct table.");
            ReloadSaved();Check(grid!.Table.Items.Count==count+1,"Reload must retain receipts.");
            Check(dialog.Result!.UsdValue=="280 PKR"&&grid.Table.Items.Cast<GridRow>().Any(r=>r["R"]=="280 PKR")&&BuildPageExport().Tables[0].Columns.Single(c=>c.Key=="R").Type=="text","Both receipt pages must retain USD rate as text after save/reload and in exports.");
            Check(grid.Table.Columns.Count(c=>c.Header?.ToString()=="USD rate")==1,"Each receipt table must show exactly one separate USD rate column.");
            if(kind==TransferReceiptKind.Party)Check(!grid.Table.Columns.Any(c=>c.Header?.ToString()?.Contains("Allocated",StringComparison.OrdinalIgnoreCase)==true||c.Header?.ToString()?.Contains("Unallocated",StringComparison.OrdinalIgnoreCase)==true),"Party receipt must show stored USD rate instead of allocation columns.");
            if(kind==TransferReceiptKind.Party)Check(dialog.Result!.BankTransferCharge==10&&grid.Table.Items.Cast<GridRow>().Any(r=>r["T"]=="10.00")&&BuildPageExport().Tables[0].Columns.Single(c=>c.Key=="T").Type=="number","Party bank charge must persist and export numerically.");
            var export=BuildPageExport();Check(export.Tables.Single().Rows.Count==count+1&&export.Tables[0].Columns.Single(c=>c.Key=="C").Type=="number","Receipt export must retain numeric USD rates.");
        }
        Navigate("agentReceipt");Navigate("parties");Navigate("receipts");
        Check(activePage=="agentReceipt","Receipt tab must remember the selected sub-tab.");
        var host=(System.Windows.Controls.DockPanel)sheetArea.Children[0];var subtabs=(System.Windows.Controls.StackPanel)((System.Windows.Controls.Grid)((System.Windows.Controls.Border)host.Children[0]).Child).Children[0];
        ((System.Windows.Controls.Button)subtabs.Children[0]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(activePage=="partyReceipt"&&grid!.Table.Columns.Count==7,"Party Receipt sub-tab must open the party table.");
        host=(System.Windows.Controls.DockPanel)sheetArea.Children[0];subtabs=(System.Windows.Controls.StackPanel)((System.Windows.Controls.Grid)((System.Windows.Controls.Border)host.Children[0]).Child).Children[0];
        ((System.Windows.Controls.Button)subtabs.Children[1]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(activePage=="agentReceipt"&&grid!.Table.Columns.Count==5,"Agent Receipt sub-tab must open the agent table.");
        store.CreateRegisterInvoice(party.Id,store.Load("form",party.Id).Revision,new("Pakistan",agent.Name,"SYNTHETIC-EXPENSE-PI",new(2026,1,1),agent.Id));
        var expenseInvoice=store.GetExpenseInvoices(party.Id).Single();
        var expenseDialog=new ExpenseReceiptDialog(store){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false};
        expenseDialog.Loaded+=(_,_)=>{expenseDialog.FillForCheck(party.Id,expenseInvoice.Id);expenseDialog.SubmitForCheck();};
        Check(expenseDialog.ShowDialog()==true&&expenseDialog.Result?.Amount==25.50m,"Expense form must save a receipt for the selected party invoice.");
        host=(System.Windows.Controls.DockPanel)sheetArea.Children[0];subtabs=(System.Windows.Controls.StackPanel)((System.Windows.Controls.Grid)((System.Windows.Controls.Border)host.Children[0]).Child).Children[0];
        ((System.Windows.Controls.Button)subtabs.Children[2]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Check(activePage=="expenseReceipt"&&grid!.Table.Columns.Count==6&&grid.Table.Items.Cast<GridRow>().Any(r=>r["C"]=="25.50"),"Expense sub-tab must show the saved expense.");
        Check(BuildPageExport().Tables[0].Name=="Expense Receipt"&&BuildPageExport().Tables[0].Columns.Single(c=>c.Key=="C").Type=="number","Expense exports must retain name and numeric amount.");
        ReloadSaved();Check(grid!.Table.Items.Cast<GridRow>().Any(r=>r["C"]=="25.50"),"Expense must survive reload.");
        Navigate("form");
    }
}









