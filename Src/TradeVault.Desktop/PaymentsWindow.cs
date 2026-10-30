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
    private readonly ListBox history=new(){HorizontalContentAlignment=HorizontalAlignment.Stretch,BorderThickness=new(0)};
    private readonly DateCellEditor date=new(){Value=DateTime.Today.ToString("yyyy-MM-dd"),MinHeight=36,Padding=new(10,6,10,6)};
    private readonly TextBox amount=new(){Text="0.00",MinHeight=36,FontSize=15,TextAlignment=TextAlignment.Right,Padding=new(10,6,10,6)};
    private readonly TextBox reference=new(){MinHeight=36,Padding=new(10,6,10,6)};
    private readonly TextBlock mode=Ui.Text("Payment details",16,"#18243B",true),dateLabel=Ui.Text("Payment date",12,"#52627A");
    private readonly TextBlock receivedTotal=Ui.Text("USD 0.00",20,"#18243B",true),allocatedTotal=Ui.Text("USD 0.00",20,"#18243B",true),advanceTotal=Ui.Text("USD 0.00",20,"#2458D3",true);
    private readonly TextBlock feedback=Ui.Text("",12,"#B42318"),balanceNote=Ui.Text("",11,"#64748B"),correctionNote=Ui.Text("",11,"#64748B");
    private readonly ObservableCollection<PaymentRow> rows=[];private readonly DataGrid table;
    private readonly Button save,editPayment,cancelPayment,entryTab,historyTab;
    private readonly Border feedbackBox;
    private readonly Grid pages=new();private readonly DockPanel entryPage=new(),historyPage=new();
    private readonly StackPanel emptyInvoices=new(){HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new(24)};
    private readonly TextBlock emptyHistory=Ui.Text("No payments recorded yet. Save your first payment in New payment.",13,"#64748B");
    private Guid? correcting;private bool refreshing,balancesValid;
    internal PaymentsWindow(Window owner,ReportWorkflow service,Guid party,string? partyName=null)
    {
        this.service=service;this.party=party;if(new System.Windows.Interop.WindowInteropHelper(owner).Handle!=IntPtr.Zero)Owner=owner;
        date.BorderThickness=new(1);date.BorderBrush=Ui.Color("#DCE3ED");date.Background=Ui.Color("#FFFFFF");
        var area=SystemParameters.WorkArea;Width=Math.Min(1180,area.Width);Height=Math.Min(780,area.Height);MinWidth=Math.Min(760,Width);MinHeight=Math.Min(480,Height);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Background=Ui.Color("#F4F6FA")};
        var heading=new DockPanel();var close=Ui.Button("Close",()=>Close(),"QuietButton");close.Padding=new(12,6,12,6);DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
        var identity=new StackPanel();identity.Children.Add(Ui.Text("Receipts",24,"#18243B",true));identity.Children.Add(Ui.Text(partyName??(owner as MainWindow)?.SelectedPartyName??"Customer payments",12,"#64748B"));heading.Children.Add(identity);
        var headingBar=new Border{Background=Ui.Color("#FFFFFF"),BorderBrush=Ui.Color("#E2E8F0"),BorderThickness=new(0,0,0,1),Padding=new(18,12,18,12),Child=heading};DockPanel.SetDock(headingBar,Dock.Top);root.Children.Add(headingBar);
        var navigation=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(18,10,18,10)};
        entryTab=Ui.Button("New payment",()=>SelectPage(false),"QuietButton");historyTab=Ui.Button("Payment history",()=>SelectPage(true),"QuietButton");
        foreach(var tab in new[]{entryTab,historyTab}){tab.Padding=new(14,7,14,7);tab.Margin=new(0,0,6,0);tab.FontWeight=FontWeights.SemiBold;navigation.Children.Add(tab);}
        DockPanel.SetDock(navigation,Dock.Top);root.Children.Add(navigation);
        feedbackBox=new Border{Padding=new(12,8,12,8),CornerRadius=new(6),BorderThickness=new(1),Margin=new(18,0,18,10),Child=feedback};
        var feedbackStyle=new Style(typeof(Border));var none=new DataTrigger{Binding=new Binding(nameof(TextBlock.Text)){Source=feedback},Value=""};none.Setters.Add(new Setter(VisibilityProperty,Visibility.Collapsed));feedbackStyle.Triggers.Add(none);feedbackBox.Style=feedbackStyle;
        DockPanel.SetDock(feedbackBox,Dock.Top);root.Children.Add(feedbackBox);
        pages.Margin=new(18,0,18,14);pages.Children.Add(entryPage);pages.Children.Add(historyPage);root.Children.Add(pages);

        var footer=new StackPanel();var totals=new Grid();for(var i=0;i<3;i++)totals.ColumnDefinitions.Add(new());
        totals.Children.Add(Total("Amount received",receivedTotal,"Customer payment"));var applied=Total("Applied to invoices",allocatedTotal,"Entered in the table");Grid.SetColumn(applied,1);totals.Children.Add(applied);
        var advance=Total("Left as advance",advanceTotal,"Available for a future invoice");Grid.SetColumn(advance,2);totals.Children.Add(advance);footer.Children.Add(totals);
        var actions=new DockPanel{Margin=new(0,12,0,0)};var actionButtons=new StackPanel{Orientation=Orientation.Horizontal};
        var clear=Ui.Button("Clear form",()=>Reset(),"QuietButton");clear.Padding=new(12,8,12,8);clear.Margin=new(0,0,8,0);actionButtons.Children.Add(clear);
        save=Ui.Button("Save payment",()=>Attempt(Save),"PrimaryButton");save.Padding=new(20,8,20,8);save.MinWidth=130;actionButtons.Children.Add(save);DockPanel.SetDock(actionButtons,Dock.Right);actions.Children.Add(actionButtons);
        var hint=Ui.Text("Reports update after saving.",11,"#64748B");hint.Margin=new(0,0,12,0);actions.Children.Add(hint);footer.Children.Add(actions);
        var footerCard=Ui.Card(footer,new Thickness(14));footerCard.Margin=new(0,10,0,0);DockPanel.SetDock(footerCard,Dock.Bottom);entryPage.Children.Add(footerCard);

        var content=new Grid{MinHeight=420};content.RowDefinitions.Add(new(){Height=GridLength.Auto});content.RowDefinitions.Add(new());
        var form=new StackPanel();var formHeading=new DockPanel();var currency=Ui.Badge("USD");DockPanel.SetDock(currency,Dock.Right);formHeading.Children.Add(currency);formHeading.Children.Add(mode);form.Children.Add(formHeading);
        var inputs=new Grid{Margin=new(0,10,0,0)};inputs.ColumnDefinitions.Add(new(){Width=new GridLength(175)});inputs.ColumnDefinitions.Add(new(){Width=new GridLength(190)});inputs.ColumnDefinitions.Add(new());
        var dates=new StackPanel{Margin=new(0,0,12,0)};dateLabel.Margin=new(0,0,0,6);dates.Children.Add(dateLabel);dates.Children.Add(date);inputs.Children.Add(dates);
        var money=new StackPanel{Margin=new(0,0,12,0)};Add(money,"Amount received (USD)",amount);Grid.SetColumn(money,1);inputs.Children.Add(money);
        var description=new StackPanel();Add(description,"Reference / description (optional)",reference);reference.ToolTip="For example: bank transfer ABC123";Grid.SetColumn(description,2);inputs.Children.Add(description);form.Children.Add(inputs);
        correctionNote.Margin=new(0,8,0,0);correctionNote.Visibility=Visibility.Collapsed;form.Children.Add(correctionNote);content.Children.Add(Ui.Card(form,new Thickness(14)));

        var allocation=new DockPanel();var allocationHeader=new StackPanel{Margin=new(14,12,14,10)};
        allocationHeader.Children.Add(Ui.Text("Apply this payment to invoices",16,"#18243B",true));
        var explanation=Ui.Text("Enter amounts in Pay bank and Pay difference. Leave other invoices at zero. All amounts are USD.",12,"#64748B");explanation.Margin=new(0,4,0,6);allocationHeader.Children.Add(explanation);allocationHeader.Children.Add(balanceNote);DockPanel.SetDock(allocationHeader,Dock.Top);allocation.Children.Add(allocationHeader);
        var help=Ui.Text("Bank = Our customs invoice amount still unpaid. Difference = Original minus Our customs, less earlier difference payments. An advance reduces the party account but does not settle an invoice until applied. Future invoices are excluded at the selected payment date.",11,"#64748B");help.Margin=new(10,8,10,10);
        var details=new Expander{Header="How payment allocation works",Content=help,Margin=new(12,6,12,8),FontSize=11};DockPanel.SetDock(details,Dock.Bottom);allocation.Children.Add(details);
        table=new DataGrid{ItemsSource=rows,AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,ColumnHeaderHeight=50,RowHeight=34,FrozenColumnCount=1,SelectionUnit=DataGridSelectionUnit.Cell};
        Column("Invoice","Invoice",180);table.Columns[0].Width=new DataGridLength(1,DataGridLengthUnitType.Star);table.Columns[0].MinWidth=180;
        Column("Due date","Due",105);Column("Bank unpaid","BankUnpaid",110);Column("Difference unpaid","DifferenceUnpaid",115);Column("Total unpaid","TotalUnpaid",115);
        Column("Pay bank","PayBank",110,true);Column("Pay difference","PayDifference",120,true);Column("Balance after payment","Remaining",145);
        table.PreparingCellForEdit+=(_,e)=>{if(e.EditingElement is TextBox input){input.Focus();input.SelectAll();}};
        var allocationGrid=new Grid();allocationGrid.Children.Add(table);
        emptyInvoices.Children.Add(Ui.Text("No invoices at this date",16,"#18243B",true));emptyInvoices.Children.Add(Ui.Text("Choose another date, or save this payment as an advance.",12,"#64748B"));allocationGrid.Children.Add(emptyInvoices);allocation.Children.Add(allocationGrid);
        var allocationCard=Ui.Card(allocation,new Thickness(0));allocationCard.Margin=new(0,10,0,0);Grid.SetRow(allocationCard,1);content.Children.Add(allocationCard);
        var formScroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Stretch};
        content.SetBinding(HeightProperty,new Binding(nameof(ScrollViewer.ViewportHeight)){Source=formScroll});entryPage.Children.Add(formScroll);

        var historyBody=new DockPanel();var historyHeading=new StackPanel{Margin=new(14)};historyHeading.Children.Add(Ui.Text("Recorded payments",17,"#18243B",true));historyHeading.Children.Add(Ui.Text("Select a payment to correct it or cancel its effect. The original always stays in history.",12,"#64748B"));DockPanel.SetDock(historyHeading,Dock.Top);historyBody.Children.Add(historyHeading);
        var historyActions=new DockPanel{Margin=new(14)};var historyButtons=new StackPanel{Orientation=Orientation.Horizontal};
        editPayment=Ui.Button("Edit payment",()=>Attempt(Edit),"SecondaryActionButton");editPayment.Margin=new(0,0,8,0);historyButtons.Children.Add(editPayment);
        cancelPayment=Ui.Button("Cancel payment",()=>Attempt(Cancel),"QuietButton");cancelPayment.Foreground=Ui.Color("#A63E3E");historyButtons.Children.Add(cancelPayment);DockPanel.SetDock(historyButtons,Dock.Right);historyActions.Children.Add(historyButtons);historyActions.Children.Add(Ui.Text("Only active payments can be changed.",11,"#64748B"));DockPanel.SetDock(historyActions,Dock.Bottom);historyBody.Children.Add(historyActions);
        history.ItemTemplate=HistoryTemplate();var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));itemStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));var frame=new FrameworkElementFactory(typeof(Border));frame.Name="SelectionFrame";frame.SetValue(Border.BackgroundProperty,System.Windows.Media.Brushes.Transparent);frame.SetValue(Border.BorderThicknessProperty,new Thickness(1));frame.SetValue(Border.BorderBrushProperty,System.Windows.Media.Brushes.Transparent);
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);frame.AppendChild(presenter);var template=new ControlTemplate(typeof(ListBoxItem)){VisualTree=frame};
        var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Border.BackgroundProperty,Ui.Color("#E8EFFD"),"SelectionFrame"));selected.Setters.Add(new Setter(Border.BorderBrushProperty,Ui.Color("#8FAEEA"),"SelectionFrame"));template.Triggers.Add(selected);itemStyle.Setters.Add(new Setter(Control.TemplateProperty,template));history.ItemContainerStyle=itemStyle;
        var historyGrid=new Grid();historyGrid.Children.Add(history);emptyHistory.Margin=new(24);emptyHistory.HorizontalAlignment=HorizontalAlignment.Center;historyGrid.Children.Add(emptyHistory);historyBody.Children.Add(historyGrid);historyPage.Children.Add(Ui.Card(historyBody,new Thickness(0)));
        history.SelectionChanged+=(_,_)=>{var active=history.SelectedItem is History h&&!h.Cancelled&&h.Receipt.Reverses==null;editPayment.IsEnabled=active;cancelPayment.IsEnabled=active;};
        history.MouseDoubleClick+=(_,_)=>{if(editPayment.IsEnabled)Attempt(Edit);};
        Ui.WindowFrame(this,root,"Receipts");System.Windows.Automation.AutomationProperties.SetName(date,"Payment date");System.Windows.Automation.AutomationProperties.SetName(history,"Recorded payments");
        Loaded+=(_,_)=>{amount.Focus();amount.SelectAll();};amount.GotKeyboardFocus+=(_,_)=>amount.SelectAll();
        amount.TextChanged+=(_,_)=>UpdateSummary();date.TextChanged+=(_,_)=>{if(!refreshing)RefreshBalances();};SelectPage(false);Reload();RefreshBalances();
    }
    private static Border Total(string label,TextBlock value,string hint)
    {
        var panel=new StackPanel{Margin=new(0,0,12,0)};panel.Children.Add(Ui.Text(label,11,"#64748B"));value.Margin=new(0,3,0,3);panel.Children.Add(value);panel.Children.Add(Ui.Text(hint,10,"#64748B"));return new Border{Child=panel};
    }
    private void SelectPage(bool recorded)
    {
        entryPage.Visibility=recorded?Visibility.Collapsed:Visibility.Visible;historyPage.Visibility=recorded?Visibility.Visible:Visibility.Collapsed;
        entryTab.Background=Ui.Color(recorded?"#FFFFFF":"#E8EFFD");entryTab.Foreground=Ui.Color(recorded?"#52627A":"#2458D3");historyTab.Background=Ui.Color(recorded?"#E8EFFD":"#FFFFFF");historyTab.Foreground=Ui.Color(recorded?"#2458D3":"#52627A");
    }
    private void SetFeedback(string text,bool success=false)
    {
        feedback.Text=text;feedback.Foreground=Ui.Color(success?"#247653":"#B42318");feedbackBox.Background=Ui.Color(success?"#F3F8F5":"#FFF2F0");feedbackBox.BorderBrush=Ui.Color(success?"#D4E2D9":"#F3C8C3");
    }
    private static void Add(Panel panel,string label,FrameworkElement field){var text=Ui.Text(label,12,"#52627A");text.Margin=new(0,0,0,6);panel.Children.Add(text);panel.Children.Add(field);System.Windows.Automation.AutomationProperties.SetName(field,label);}
    private static DataTemplate HistoryTemplate()
    {
        var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.BorderBrushProperty,Ui.Color("#E2E8F0"));frame.SetValue(Border.BorderThicknessProperty,new Thickness(0,0,0,1));frame.SetValue(Border.PaddingProperty,new Thickness(14,12,14,12));
        var line=new FrameworkElementFactory(typeof(DockPanel));frame.AppendChild(line);
        void Label(string path,double width,Dock dock,bool bold=false)
        {
            var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(path));text.SetValue(TextBlock.ForegroundProperty,Ui.Color(bold?"#18243B":"#52627A"));text.SetValue(TextBlock.FontSizeProperty,12d);text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);text.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);
            if(width>0){text.SetValue(FrameworkElement.WidthProperty,width);text.SetValue(DockPanel.DockProperty,dock);}if(bold)text.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);line.AppendChild(text);
        }
        Label("Status",135,Dock.Right);Label("Amount",150,Dock.Right,true);Label("Date",105,Dock.Left);Label("Reference",0,Dock.Left,true);return new DataTemplate{VisualTree=frame};
    }
    private void Column(string label,string path,double width,bool edit=false)
    {
        var column=new DataGridTextColumn{Header=label,IsReadOnly=!edit,Width=width,Binding=new Binding(path){UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged}};
        var textStyle=new Style(typeof(TextBlock));textStyle.Setters.Add(new Setter(TextBlock.PaddingProperty,new Thickness(10,6,10,6)));textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,path=="Invoice"?TextAlignment.Left:path=="Due"?TextAlignment.Center:TextAlignment.Right));
        if(edit){textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,Ui.Color("#2458D3")));textStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty,FontWeights.SemiBold));}
        column.ElementStyle=textStyle;
        if(edit)
        {
            var style=new Style(typeof(DataGridCell),(Style)FindResource(typeof(DataGridCell)));style.Setters.Add(new Setter(Control.BackgroundProperty,Ui.Color("#EDF4FF")));column.CellStyle=style;
            var editor=new Style(typeof(TextBox));editor.Setters.Add(new Setter(TextBox.TextAlignmentProperty,TextAlignment.Right));editor.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8,4,8,4)));column.EditingElementStyle=editor;
        }
        table.Columns.Add(column);
    }
    private DateOnly PaymentDate()=>DateOnly.ParseExact(DateCellConverter.Normalize(date.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture);
    private void Reload()
    {
        var items=service.Receipts(party);var cancelled=items.Where(r=>r.Reverses!=null).Select(r=>r.Reverses!.Value).ToHashSet();
        history.ItemsSource=items.Select(r=>new History(r,cancelled.Contains(r.Id))).Reverse().OrderByDescending(h=>h.Receipt.Date).ToArray();
        historyTab.Content="Payment history · "+items.Count;emptyHistory.Visibility=items.Count==0?Visibility.Visible:Visibility.Collapsed;
        editPayment.IsEnabled=false;cancelPayment.IsEnabled=false;
    }
    private void Attempt(Action action){try{action();}catch(Exception ex){SetFeedback(ex.Message);}}
    private void RefreshBalances()
    {
        try
        {
            var old=rows.ToDictionary(r=>r.Balance.Id,r=>(r.PayBank,r.PayDifference));var balances=service.PaymentBalances(party,PaymentDate(),correcting);rows.Clear();
            foreach(var balance in balances){var row=new PaymentRow(balance,UpdateSummary);if(old.TryGetValue(balance.Id,out var draft))row.SetDraft(draft.PayBank,draft.PayDifference);rows.Add(row);}
            balancesValid=true;balanceNote.Text=$"{balances.Count} invoice{(balances.Count==1?"":"s")} · USD {balances.Sum(b=>b.TotalUnpaid):N2} outstanding · at {PaymentDate():dd-MM-yyyy}";emptyInvoices.Visibility=balances.Count==0?Visibility.Visible:Visibility.Collapsed;
        }
        catch(Exception ex){balancesValid=false;emptyInvoices.Visibility=Visibility.Collapsed;SetFeedback(ex.Message);}
        UpdateSummary();
    }
    private void UpdateSummary()
    {
        if(refreshing||save==null)return;
        receivedTotal.Text="—";allocatedTotal.Text="—";advanceTotal.Text="—";
        try
        {
            if(!balancesValid){save.IsEnabled=false;return;}
            var total=Parse(amount.Text);var allocated=rows.Sum(r=>r.BankPayment+r.DifferencePayment);
            var remaining=total-allocated;receivedTotal.Text=$"USD {total:N2}";allocatedTotal.Text=$"USD {allocated:N2}";advanceTotal.Text=$"USD {remaining:N2}";advanceTotal.Foreground=Ui.Color(remaining<0?"#B42318":"#2458D3");
            foreach(var row in rows)row.ValidateDraft();
            if(remaining<0)throw new ArgumentException("You assigned more than the money received. Reduce the amounts entered against invoices.");
            SetFeedback("");save.IsEnabled=total>0;
        }
        catch(Exception ex){SetFeedback(ex.Message);save.IsEnabled=false;}
    }
    private static decimal Parse(string value){if(!decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var result)||result<0||decimal.Round(result,2)!=result)throw new ArgumentException("Enter a non-negative USD amount with at most two decimal places.");return result;}
    private History Selected()=>history.SelectedItem is History h&&h.Receipt.Reverses==null&&!h.Cancelled?h:throw new ArgumentException("Select an active recorded payment from the history first.");
    private void Edit()
    {
        var item=Selected();refreshing=true;correcting=item.Receipt.Id;amount.Text=item.Receipt.Amount.ToString("0.00",CultureInfo.InvariantCulture);reference.Text=item.Receipt.Reference;date.Value=(item.Receipt.Date>DateOnly.FromDateTime(DateTime.Today)?item.Receipt.Date:DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd");rows.Clear();refreshing=false;
        mode.Text="Correct payment";dateLabel.Text="Correction date";correctionNote.Text=$"Original payment: USD {item.Receipt.Amount:N2} · {item.Receipt.Date:dd-MM-yyyy}. Saving keeps the original in history.";correctionNote.Visibility=Visibility.Visible;save.Content="Save correction";System.Windows.Automation.AutomationProperties.SetName(save,"Save correction");SelectPage(false);RefreshBalances();
        foreach(var row in rows)row.SetDraft((item.Receipt.Allocations.SingleOrDefault(a=>a.InvoiceId==row.Balance.Id&&a.Component=="Bank")?.Amount??0).ToString("0.00",CultureInfo.InvariantCulture),(item.Receipt.Allocations.SingleOrDefault(a=>a.InvoiceId==row.Balance.Id&&a.Component=="Difference")?.Amount??0).ToString("0.00",CultureInfo.InvariantCulture));
        UpdateSummary();
    }
    private void Cancel()
    {
        var item=Selected();var dialog=new Window{Owner=this,Width=Math.Min(520,SystemParameters.WorkArea.Width),SizeToContent=SizeToContent.Height,MaxHeight=SystemParameters.WorkArea.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var body=new StackPanel{Margin=new(18)};body.Children.Add(Ui.Text("Cancel this payment?",21,"#18243B",true));
        var detail=Ui.Text($"USD {item.Receipt.Amount:N2} · {item.Receipt.Date:dd-MM-yyyy} · {item.Reference}",13,"#52627A");detail.Margin=new(0,8,0,8);body.Children.Add(detail);
        body.Children.Add(Ui.Text("The original stays in history. This reverses its invoice payments; no bank refund is made.",12,"#64748B"));
        var effective=new DateCellEditor{Value=(item.Receipt.Date>DateOnly.FromDateTime(DateTime.Today)?item.Receipt.Date:DateOnly.FromDateTime(DateTime.Today)).ToString("yyyy-MM-dd"),MinHeight=36};var reason=new TextBox{MinHeight=36};
        var fields=new StackPanel{Margin=new(0,14,0,0)};Add(fields,"Cancellation date",effective);var reasonPanel=new StackPanel{Margin=new(0,12,0,0)};Add(reasonPanel,"Reason for cancellation",reason);fields.Children.Add(reasonPanel);body.Children.Add(fields);
        var error=Ui.Text("",12,"#B42318");error.Visibility=Visibility.Collapsed;body.Children.Add(error);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,14,0,0)};
        var keep=Ui.Button("Keep payment",()=>dialog.DialogResult=false);keep.IsCancel=true;keep.IsDefault=true;keep.Margin=new(0,0,8,0);actions.Children.Add(keep);
        var cancel=Ui.Button("Cancel payment",()=>{try{service.ReverseReceipt(party,item.Receipt.Id,DateOnly.ParseExact(DateCellConverter.Normalize(effective.Text),"yyyy-MM-dd",CultureInfo.InvariantCulture),reason.Text);dialog.DialogResult=true;}catch(Exception ex){error.Text=ex.Message;error.Margin=new(0,10,0,0);error.Visibility=Visibility.Visible;}});cancel.Background=AppTheme.DangerFill;cancel.BorderBrush=AppTheme.DangerFill;cancel.Foreground=System.Windows.Media.Brushes.White;actions.Children.Add(cancel);body.Children.Add(actions);
        Ui.WindowFrame(dialog,new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},"Cancel payment");if(dialog.ShowDialog()==true){Reload();Reset();SetFeedback("Payment cancelled. The original remains in history; invoice balances are updated.",true);}
    }
    private void Save()
    {
        table.CommitEdit();table.CommitEdit(DataGridEditingUnit.Row,true);UpdateSummary();if(!save.IsEnabled)return;
        var splits=rows.SelectMany(r=>new[]{new ReceiptSplit(r.Balance.Id,"Bank",r.BankPayment),new ReceiptSplit(r.Balance.Id,"Difference",r.DifferencePayment)}).Where(a=>a.Amount>0).ToList();
        if(correcting is {} id)service.CorrectReceipt(party,id,PaymentDate(),Parse(amount.Text),reference.Text,splits);else service.RecordReceipt(party,PaymentDate(),Parse(amount.Text),reference.Text,splits);
        Reload();Reset();SetFeedback("Payment saved. Invoice balances and account reports are updated.",true);
    }
    private void Reset(){refreshing=true;correcting=null;rows.Clear();amount.Text="0.00";reference.Clear();date.Value=DateTime.Today.ToString("yyyy-MM-dd");mode.Text="Payment details";dateLabel.Text="Payment date";correctionNote.Visibility=Visibility.Collapsed;save.Content="Save payment";System.Windows.Automation.AutomationProperties.SetName(save,"Save payment");refreshing=false;SelectPage(false);RefreshBalances();}
    internal void PreviewHistory()=>SelectPage(true);
    internal void PreviewDraft()
    {
        amount.Text="5000.00";reference.Text="Bank transfer ABC123";
        if(rows.Count>0)rows[0].SetDraft("3000.00","500.00");if(rows.Count>1)rows[1].SetDraft("1000.00","0.00");
    }
    internal void CheckForAcceptance()
    {
        if(rows.Count==0)throw new InvalidOperationException("Missing invoice balance rows.");
        amount.Text="300";rows[0].PayBank="100";
        if(!advanceTotal.Text.Contains("200.00")||!save.IsEnabled||rows[0].Remaining=="Check payment")throw new InvalidOperationException("Automatic payment summary failed.");
        rows[0].PayBank="400";if(save.IsEnabled)throw new InvalidOperationException("Over-allocation was accepted.");Reset();
        var content=(FrameworkElement)Content;content.Measure(new(1000,600));content.Arrange(new(0,0,1000,600));content.UpdateLayout();var point=save.TranslatePoint(new(0,0),content);
        if(point.Y<0||point.Y+save.ActualHeight>600)throw new InvalidOperationException("Payment save button is outside the viewport.");
        if(table.Columns.Count(c=>!c.IsReadOnly)!=2)throw new InvalidOperationException("Only payment assignment columns should be editable.");
        content.Measure(new(760,480));content.Arrange(new(0,0,760,480));content.UpdateLayout();point=save.TranslatePoint(new(0,0),content);
        if(point.Y<0||point.Y+save.ActualHeight>480)throw new InvalidOperationException("Payment save button is hidden at minimum size.");
        amount.Text="300";SelectPage(true);if(historyPage.Visibility!=Visibility.Visible||entryPage.Visibility!=Visibility.Collapsed)throw new InvalidOperationException("History navigation failed.");
        SelectPage(false);if(amount.Text!="300")throw new InvalidOperationException("Navigation discarded the payment draft.");
        if(editPayment.IsEnabled||cancelPayment.IsEnabled)throw new InvalidOperationException("History actions require a selected active payment.");Reset();
    }
    private sealed record History(PartyReceipt Receipt,bool Cancelled)
    {
        public string Date=>Receipt.Date.ToString("dd-MM-yyyy");public string Amount=>$"USD {Receipt.Amount:N2}";
        public string Reference=>string.IsNullOrWhiteSpace(Receipt.Reference)?"No reference":Receipt.Reference;
        public string Status=>Receipt.Reverses!=null?"Cancellation entry":Cancelled?"Cancelled / replaced":"Recorded";
    }
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
