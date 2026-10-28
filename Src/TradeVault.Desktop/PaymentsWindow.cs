using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TradeVault.Application;
using TradeVault.Infrastructure;
namespace TradeVault.Desktop;

internal sealed class PaymentsWindow:Window
{
    private readonly ReportWorkflow service;private readonly Guid party;
    private readonly ListBox history=new(){Height=110,DisplayMemberPath="Label"};
    private readonly DateCellEditor date=new(){Value=DateTime.Today.ToString("yyyy-MM-dd"),MinHeight=32};
    private readonly TextBox amount=new(){Text="0.00"};private readonly TextBox reference=new();
    private readonly TextBlock mode=Ui.Text("New customer payment",17,"#18243B",true),dateLabel=Ui.Text("Date money was received",12,"#52627A");
    private readonly TextBlock summary=Ui.Text("",13,"#2458D3",true),feedback=Ui.Text("",12,"#B42318"),balanceNote=Ui.Text("",12,"#52627A");
    private readonly ObservableCollection<PaymentRow> rows=[];private readonly DataGrid table;
    private readonly Button save;private Guid? correcting;private bool refreshing,balancesValid;
    internal PaymentsWindow(Window owner,ReportWorkflow service,Guid party)
    {
        this.service=service;this.party=party;if(new System.Windows.Interop.WindowInteropHelper(owner).Handle!=IntPtr.Zero)Owner=owner;var area=SystemParameters.WorkArea;Width=Math.Min(1180,area.Width);Height=Math.Min(780,area.Height);MinWidth=Math.Min(760,Width);MinHeight=Math.Min(480,Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Background=Ui.Color("#F4F6FA")};
        var footer=new StackPanel{Margin=new(16,10,16,12)};footer.Children.Add(summary);footer.Children.Add(feedback);
        var actions=new WrapPanel{Margin=new(0,8,0,0)};save=Ui.Button("Save new payment",()=>Attempt(Save),"PrimaryButton");actions.Children.Add(save);
        actions.Children.Add(Ui.Button("Start new payment / clear form",()=>Reset()));actions.Children.Add(Ui.Button("Close",()=>Close()));footer.Children.Add(actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var form=new StackPanel{Margin=new(16)};form.Children.Add(Ui.Text("Customer payments and unpaid invoices",21,"#18243B",true));
        form.Children.Add(Ui.Text("Record money received, then enter how much pays each invoice. All amounts are USD. Reports update after saving.",12,"#52627A"));
        var historyBody=new StackPanel();Add(historyBody,"Select an active payment to edit or cancel",history);
        var historyActions=new WrapPanel();historyActions.Children.Add(Ui.Button("Edit selected payment / assign its advance",()=>Attempt(Edit)));historyActions.Children.Add(Ui.Button("Cancel recorded payment",()=>Attempt(Cancel)));historyBody.Children.Add(historyActions);
        form.Children.Add(new Expander{Header="Recorded payments — open history to edit or cancel",Content=historyBody,Margin=new(0,10,0,0)});
        mode.Margin=new(0,12,0,0);form.Children.Add(mode);
        var inputs=new Grid();inputs.ColumnDefinitions.Add(new(){Width=new GridLength(230)});inputs.ColumnDefinitions.Add(new(){Width=new GridLength(220)});inputs.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var dates=new StackPanel{Margin=new(0,0,10,0)};dateLabel.Margin=new(0,12,0,5);dates.Children.Add(dateLabel);dates.Children.Add(date);inputs.Children.Add(dates);
        var moneyFields=new StackPanel{Margin=new(0,0,10,0)};Add(moneyFields,"Total money received (USD)",amount);Grid.SetColumn(moneyFields,1);inputs.Children.Add(moneyFields);
        var notes=new StackPanel();Add(notes,"Payment description / bank transfer reference",reference);Grid.SetColumn(notes,2);inputs.Children.Add(notes);form.Children.Add(inputs);
        form.Children.Add(Ui.Text("Example: Transfer ABC123 received in Malaysia account. This is a note, not a bank-account selector.",11,"#64748B"));
        form.Children.Add(Ui.Text("Choose which invoices this payment pays",17,"#18243B",true));
        form.Children.Add(Ui.Text("Bank unpaid = contract amount still owed. Difference unpaid = the remaining difference after any approved shipping deduction and earlier payments. Enter payment amounts in the two blue columns; leave other invoices at zero.",12,"#52627A"));form.Children.Add(balanceNote);
        table=new DataGrid{ItemsSource=rows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,Height=250,ColumnHeaderHeight=58,FrozenColumnCount=1};
        Column("Invoice","Invoice",180);Column("Payment due date","Due",110);Column("Bank unpaid / USD","BankUnpaid",110);Column("Difference unpaid / USD","DifferenceUnpaid",125);Column("Total unpaid / USD","TotalUnpaid",115);
        Column("Pay bank portion / USD","PayBank",125,true);Column("Pay difference portion / USD","PayDifference",135,true);Column("Unpaid after this payment / USD","Remaining",145);
        form.Children.Add(table);form.Children.Add(Ui.Text("Balances are shown at the payment/correction date. Future invoices are excluded. An advance reduces the customer account balance but does not settle an invoice until assigned. Cancelling a payment changes records only; it does not send a bank refund.",12,"#64748B"));
        root.Children.Add(new ScrollViewer{Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        Ui.WindowFrame(this,root,"Customer payments");
        amount.TextChanged+=(_,_)=>UpdateSummary();date.TextChanged+=(_,_)=>{if(!refreshing)RefreshBalances();};Reload();RefreshBalances();
    }
    private static void Add(Panel panel,string label,FrameworkElement field){var text=Ui.Text(label,12,"#52627A");text.Margin=new(0,12,0,5);panel.Children.Add(text);panel.Children.Add(field);System.Windows.Automation.AutomationProperties.SetName(field,label);}
    private void Column(string label,string path,double width,bool edit=false)
    {
        var column=new DataGridTextColumn{Header=label,IsReadOnly=!edit,Width=width,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged}};
        if(edit){var style=new Style(typeof(DataGridCell));style.Setters.Add(new Setter(Control.BackgroundProperty,Ui.Color("#EDF4FF")));column.CellStyle=style;}
        table.Columns.Add(column);
    }
    private DateOnly PaymentDate()=>DateOnly.ParseExact(DateCellConverter.Normalize(date.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture);
    private void Reload(){var items=service.Receipts(party);var cancelled=items.Where(r=>r.Reverses!=null).Select(r=>r.Reverses!.Value).ToHashSet();history.ItemsSource=items.Select(r=>new History(r,cancelled.Contains(r.Id))).ToArray();}
    private void Attempt(Action action){try{action();}catch(Exception ex){feedback.Text=ex.Message;}}
    private void RefreshBalances()
    {
        try
        {
            var old=rows.ToDictionary(r=>r.Balance.Id,r=>(r.PayBank,r.PayDifference));var balances=service.PaymentBalances(party,PaymentDate(),correcting);rows.Clear();
            foreach(var balance in balances){var row=new PaymentRow(balance,UpdateSummary);if(old.TryGetValue(balance.Id,out var draft))row.SetDraft(draft.PayBank,draft.PayDifference);rows.Add(row);}
            balancesValid=true;balanceNote.Text=$"{balances.Count} invoices · unpaid invoice total USD {balances.Sum(b=>b.TotalUnpaid):N2} · at {PaymentDate():dd-MM-yyyy}";
        }
        catch(Exception ex){balancesValid=false;feedback.Text=ex.Message;}
        UpdateSummary();
    }
    private void UpdateSummary()
    {
        if(refreshing||save==null)return;
        try
        {
            feedback.Foreground=Ui.Color("#B42318");
            if(!balancesValid){save.IsEnabled=false;return;}
            var total=Parse(amount.Text);var allocated=rows.Sum(r=>r.BankPayment+r.DifferencePayment);foreach(var row in rows)row.ValidateDraft();
            var remaining=total-allocated;summary.Text=$"Money received: USD {total:N2}    Assigned to invoices: USD {allocated:N2}    Unassigned advance: USD {remaining:N2}";
            if(remaining<0)throw new ArgumentException("You assigned more than the money received. Reduce the amounts entered against invoices.");
            feedback.Text="";save.IsEnabled=total>0;
        }
        catch(Exception ex){feedback.Text=ex.Message;summary.Text="Check the entered payment amounts. Nothing has been saved.";save.IsEnabled=false;}
    }
    private static decimal Parse(string value){if(!decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var result)||result<0||decimal.Round(result,2)!=result)throw new ArgumentException("Enter a non-negative USD amount with at most two decimal places.");return result;}
    private History Selected()=>history.SelectedItem is History h&&h.Receipt.Reverses==null&&!h.Cancelled?h:throw new ArgumentException("Select an active recorded payment from the history first.");
    private void Edit()
    {
        var item=Selected();refreshing=true;correcting=item.Receipt.Id;amount.Text=item.Receipt.Amount.ToString("0.00",CultureInfo.InvariantCulture);reference.Text=item.Receipt.Reference;date.Value=DateTime.Today.ToString("yyyy-MM-dd");rows.Clear();refreshing=false;
        mode.Text="Editing payment: "+item.Receipt.Reference;dateLabel.Text="Date this correction takes effect (on/after original payment)";save.Content="Save payment correction";RefreshBalances();
        foreach(var row in rows)row.SetDraft((item.Receipt.Allocations.SingleOrDefault(a=>a.InvoiceId==row.Balance.Id&&a.Component=="Bank")?.Amount??0).ToString("0.00",CultureInfo.InvariantCulture),(item.Receipt.Allocations.SingleOrDefault(a=>a.InvoiceId==row.Balance.Id&&a.Component=="Difference")?.Amount??0).ToString("0.00",CultureInfo.InvariantCulture));
        UpdateSummary();
    }
    private void Cancel()
    {
        var item=Selected();var dialog=new Window{Owner=this,Width=540,Height=340,WindowStartupLocation=WindowStartupLocation.CenterOwner};var body=new StackPanel{Margin=new(18)};
        body.Children.Add(Ui.Text($"Cancel recorded payment of USD {item.Receipt.Amount:N2}?",18,"#18243B",true));body.Children.Add(Ui.Text("The original remains in history. Its invoice payments are undone from the date below. No bank refund is made.",12,"#52627A"));
        var effective=new DateCellEditor{Value=DateTime.Today.ToString("yyyy-MM-dd")};var reason=new TextBox();Add(body,"Date cancellation takes effect",effective);Add(body,"Reason for cancellation",reason);
        body.Children.Add(Ui.Button("Confirm cancellation",()=>{try{service.ReverseReceipt(party,item.Receipt.Id,DateOnly.ParseExact(DateCellConverter.Normalize(effective.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture),reason.Text);dialog.DialogResult=true;}catch(Exception ex){MessageBox.Show(dialog,ex.Message);}},"PrimaryButton"));
        Ui.WindowFrame(dialog,body,"Cancel recorded payment");if(dialog.ShowDialog()==true){Reload();Reset();feedback.Text="Payment cancelled. Invoice balances and reports are updated.";}
    }
    private void Save()
    {
        table.CommitEdit();table.CommitEdit(DataGridEditingUnit.Row,true);UpdateSummary();if(!save.IsEnabled)return;
        var splits=rows.SelectMany(r=>new[]{new ReceiptSplit(r.Balance.Id,"Bank",r.BankPayment),new ReceiptSplit(r.Balance.Id,"Difference",r.DifferencePayment)}).Where(a=>a.Amount>0).ToList();
        if(correcting is {} id)service.CorrectReceipt(party,id,PaymentDate(),Parse(amount.Text),reference.Text,splits);else service.RecordReceipt(party,PaymentDate(),Parse(amount.Text),reference.Text,splits);
        Reload();Reset();feedback.Foreground=Ui.Color("#247653");feedback.Text="Payment saved. Invoice balances refreshed; both report tabs use these saved payments.";
    }
    private void Reset(){refreshing=true;correcting=null;rows.Clear();amount.Text="0.00";reference.Clear();date.Value=DateTime.Today.ToString("yyyy-MM-dd");mode.Text="New customer payment";dateLabel.Text="Date money was received";save.Content="Save new payment";refreshing=false;RefreshBalances();}
    internal void CheckForAcceptance()
    {
        if(rows.Count==0)throw new InvalidOperationException("Missing invoice balance rows.");
        amount.Text="300";rows[0].PayBank="100";
        if(!summary.Text.Contains("200.00")||!save.IsEnabled||rows[0].Remaining=="Check payment")throw new InvalidOperationException("Automatic payment summary failed.");
        rows[0].PayBank="400";if(save.IsEnabled)throw new InvalidOperationException("Over-allocation was accepted.");Reset();
        var content=(FrameworkElement)Content;content.Measure(new(1000,600));content.Arrange(new(0,0,1000,600));content.UpdateLayout();var point=save.TranslatePoint(new(0,0),content);
        if(point.Y<0||point.Y+save.ActualHeight>600)throw new InvalidOperationException("Payment save button is outside the viewport.");
        if(table.Columns.Count(c=>!c.IsReadOnly)!=2)throw new InvalidOperationException("Only payment assignment columns should be editable.");
    }
    private sealed record History(PartyReceipt Receipt,bool Cancelled){public string Label=>$"{Receipt.Date:dd-MM-yyyy} · USD {Receipt.Amount:N2} · {Receipt.Reference}"+(Receipt.Reverses!=null?" · CANCELLATION":Cancelled?" · CANCELLED / REPLACED":"");}
    private sealed class PaymentRow(InvoicePaymentBalance balance,Action changed):INotifyPropertyChanged
    {
        public InvoicePaymentBalance Balance{get;}=balance;public string Invoice=>Balance.Invoice;public string Due=>Balance.Due?.ToString("dd-MM-yyyy")??"Awaiting BL date";
        public string BankUnpaid=>Balance.BankUnpaid.ToString("N2");public string DifferenceUnpaid=>Balance.DifferenceUnpaid.ToString("N2");public string TotalUnpaid=>Balance.TotalUnpaid.ToString("N2");
        private string bank="0.00",difference="0.00";public string PayBank{get=>bank;set{bank=value;Notify();}}public string PayDifference{get=>difference;set{difference=value;Notify();}}
        public decimal BankPayment=>Parse(bank);public decimal DifferencePayment=>Parse(difference);
        public string Remaining{get{try{return ReportWorkflow.RemainingAfterPayment(Balance,BankPayment,DifferencePayment).ToString("N2");}catch{return "Check payment";}}}
        public void ValidateDraft()=>ReportWorkflow.RemainingAfterPayment(Balance,BankPayment,DifferencePayment);
        public void SetDraft(string b,string d){bank=b;difference=d;Notify();}
        private void Notify(){PropertyChanged?.Invoke(this,new(null));changed();}public event PropertyChangedEventHandler? PropertyChanged;
    }
}
