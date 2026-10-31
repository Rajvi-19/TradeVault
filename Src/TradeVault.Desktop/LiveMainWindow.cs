using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TradeVault.Infrastructure;
using TradeVault.Application;
namespace TradeVault.Desktop;

public sealed partial class MainWindow : Window
{
    private WorksheetController controller;
    private readonly WorkbookStore store;
    private readonly ReportWorkflow workflow;
    private readonly WorksheetHistory history=new();
    private bool generatedView;
    private string activePage="form";
    private readonly StackPanel partyTools;
    private readonly Border optionBar;
    private readonly WrapPanel workflows;
    private readonly WrapPanel options;
    private readonly StackPanel masterActions=new(){Orientation=Orientation.Horizontal};
    private bool busy;
    private readonly ComboBox partySelector=new(){Width=180,MinHeight=32,DisplayMemberPath="Name",IsTextSearchEnabled=true};
    private readonly ComboBox agentSelector=new(){Width=180,MinHeight=32,IsEditable=false,IsTextSearchEnabled=true};
    private readonly StackPanel agentFilterGroup=new(){Orientation=Orientation.Horizontal,Margin=new(14,0,0,0)};
    private static readonly Guid AllPartiesId=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private bool changingAgent,allPartiesView;
    private string selectedAgentId="";
    private readonly Button assignButton;
    private bool changingParty;
    private readonly TextBlock status=Ui.Text("All changes saved",11,"#64748B");
    private readonly TextBlock title=Ui.Text("Invoice register",18,"#18243B",true);
    private readonly TextBlock revision=Ui.Text("",11,"#64748B");
    private readonly TextBox opening=new(){Text="0.00",Width=100,MinHeight=30,Padding=new(8,4,8,4)};
    private readonly TextBox terms=new(){Text="90",Width=54,MinHeight=30,Padding=new(8,4,8,4)};
    private readonly StackPanel openingGroup=new(){Orientation=Orientation.Horizontal};
    private readonly StackPanel termsGroup=new(){Orientation=Orientation.Horizontal};
    private readonly Dictionary<string,Button> nav=new();
    private readonly Button saveButton;
    private readonly Button themeButton;
    private readonly Button backupButton;
    private readonly Button deleteButton;
    private readonly Button newInvoiceButton;
    internal Func<NewRegisterInvoice?>? NewInvoiceInput {get;set;}
    private readonly Border notification;
    private readonly TextBlock message=Ui.Text("",12,"#B42318");
    private ExcelGrid? grid;
    private bool dirty,refreshing;
    private bool calculationPending;
    private readonly Grid sheetArea=new();
    private readonly DockPanel footer;
    private readonly List<(Key Key,ModifierKeys Modifiers,string Gesture,string Description,Action Action)> shortcuts=[];
    internal Func<string,bool>? ConfirmRecordDeletion;

    public MainWindow(WorkbookStore store,string path)
    {
        this.store=store;workflow=new(store);controller=new(store);var startup=Ui.WorkspaceSize(SystemParameters.WorkArea);Width=startup.Width;Height=startup.Height;MinWidth=Math.Min(1000,startup.Width);MinHeight=Math.Min(600,startup.Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
        var root=new DockPanel{Background=Ui.Color("#F4F6FA")};
        var header=new DockPanel{Background=Ui.Color("#111E35"),Height=42};
        var windowControls=Ui.WindowControls(this);backupButton=Ui.LineIconButton("Backups",()=>Run(()=>{RequireSaved();WorkflowWindows.Backups(this,store);}),"backup","QuietButton");backupButton.Foreground=Brushes.White;backupButton.Height=36;backupButton.MinHeight=36;backupButton.Padding=new(12,0,12,0);backupButton.Margin=new(0,0,6,0);System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(backupButton,true);windowControls.Children.Insert(0,backupButton);DockPanel.SetDock(windowControls,Dock.Right);header.Children.Add(windowControls);
        var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(14,0,0,0)};
        var logo=Ui.BrandIcon(26);logo.Margin=new(0,0,9,0);brand.Children.Add(logo);
        brand.Children.Add(Ui.Text("TradeVault",18,"#FFFFFF",true));var divider=Ui.Text("/",16,"#64748B");divider.Margin=new(14,0,12,0);brand.Children.Add(divider);brand.Children.Add(pageTitle);header.Children.Add(brand);
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);

        var tabs=new DockPanel{Background=Ui.Color("#FFFFFF"),Height=40};
        saveButton=Ui.Button("Save changes",()=>Run(Save),"PrimaryButton","\uE74E");saveButton.MinHeight=30;saveButton.MinWidth=124;saveButton.Width=124;ToolTipService.SetShowOnDisabled(saveButton,true);saveButton.Padding=new(12,4,12,4);saveButton.Margin=new(0,4,6,4);
        themeButton=Ui.Button("",()=>Run(AppTheme.Toggle),"SecondaryActionButton");themeButton.Width=32;themeButton.Height=32;themeButton.MinHeight=32;themeButton.Padding=new(0);themeButton.Margin=new(0,4,10,4);
        SetupExport();var saveActions=new StackPanel{Orientation=Orientation.Horizontal};saveActions.Children.Add(exportButton);saveActions.Children.Add(saveButton);saveActions.Children.Add(themeButton);DockPanel.SetDock(saveActions,Dock.Right);tabs.Children.Add(saveActions);
        AppTheme.Changed+=RefreshThemeButton;Closed+=(_,_)=>AppTheme.Changed-=RefreshThemeButton;RefreshThemeButton();
        var navigation=new StackPanel{Orientation=Orientation.Horizontal,Margin=new(6,0,0,0)};
        foreach(var (name,label,icon) in new[]{("parties","Party Master","party"),("agents","Agent Master","party"),("form","Invoice Register","invoice"),("partyReceipt","Party Receipt","ledger"),("agentReceipt","Agent Receipt","ledger"),("reports","Reports","reports")})
        {
            var b=Ui.LineIconButton(label,()=>Run(()=>Navigate(name)),icon);b.Padding=new(8,5,8,5);ToolTipService.SetIsEnabled(b,false);b.MinHeight=32;b.Margin=new(2,3,2,3);nav.Add(name,b);navigation.Children.Add(b);
        }
        tabs.Children.Add(new ScrollViewer{Content=navigation,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});DockPanel.SetDock(tabs,Dock.Top);root.Children.Add(tabs);

        footer=new DockPanel{Height=24,Margin=new(12,0,12,0)};var build=Ui.Text(Ui.BuildLabel,10,"#64748B");build.Margin=new(16,0,0,0);DockPanel.SetDock(build,Dock.Right);footer.Children.Add(build);DockPanel.SetDock(revision,Dock.Right);footer.Children.Add(revision);footer.Children.Add(status);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        var heading=new DockPanel{Margin=new(12,4,12,4)};pageHeading=heading;
        partyTools=new StackPanel{Orientation=Orientation.Horizontal};
        partySelector.SetResourceReference(StyleProperty,"FilterSelectorStyle");
        System.Windows.Controls.TextSearch.SetTextPath(partySelector,"Name");
        var partyGroup=new StackPanel{Orientation=Orientation.Horizontal};var partyLabel=Ui.Text("Party",11,"#64748B",true);partyLabel.Margin=new(0,0,7,0);partyGroup.Children.Add(partyLabel);partyGroup.Children.Add(partySelector);partyTools.Children.Add(partyGroup);
        agentSelector.SetResourceReference(StyleProperty,"FilterSelectorStyle");System.Windows.Controls.TextSearch.SetTextPath(agentSelector,"Name");
        System.Windows.Automation.AutomationProperties.SetName(agentSelector,"Filter by agent");
        agentFilterGroup.Orientation=Orientation.Horizontal;var agentLabel=Ui.Text("Agent",11,"#64748B",true);agentLabel.Margin=new(0,0,7,0);agentFilterGroup.Children.Add(agentLabel);agentFilterGroup.Children.Add(agentSelector);partyTools.Children.Add(agentFilterGroup);
        agentSelector.SelectionChanged+=(_,_)=>ChangeAgentFilter();
        assignButton=CompactButton("Assign worksheet",()=>Run(AssignWorksheet),"\uE8B5");
        DockPanel.SetDock(partyTools,Dock.Right);heading.Children.Add(partyTools);title.FontFamily=new("Segoe UI Variable Display, Segoe UI");title.FontSize=17;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;heading.Children.Add(title);DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);

        options=new WrapPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};
        termsGroup.Margin=new(0,0,14,0);var termLabel=Ui.Text("New terms",12,"#52627A");termLabel.Margin=new(0,0,6,0);termsGroup.Children.Add(termLabel);termsGroup.Children.Add(terms);termsGroup.Children.Add(Ui.Text(" days",11,"#64748B"));options.Children.Add(termsGroup);
        openingGroup.Margin=new(0,0,14,0);var openLabel=Ui.Text("Opening USD",12,"#52627A");openLabel.Margin=new(0,0,6,0);openingGroup.Children.Add(openLabel);openingGroup.Children.Add(opening);options.Children.Add(openingGroup);
        var reload=CompactButton("Reload",ReloadSaved,"\uE72C");reload.ToolTip="Reload saved records (F5)";options.Children.Add(reload);
        deleteButton=CompactButton("Delete",()=>Run(Delete),"\uE74D");deleteButton.ToolTip="Delete clears cells; select a whole row to delete a record with confirmation.";deleteButton.Foreground=Ui.Color("#A63E3E");options.Children.Add(deleteButton);
        var toolbar=new Grid();toolbar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});toolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});Grid.SetColumn(options,1);toolbar.Children.Add(options);
        optionBar=new Border{Background=Ui.Color("#FFFFFF"),BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(1),Padding=new(10,5,10,5),Margin=new(10,0,10,4),Child=toolbar};DockPanel.SetDock(optionBar,Dock.Top);root.Children.Add(optionBar);
        workflows=new WrapPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,12,0)};
        newInvoiceButton=CompactButton("New Invoice",()=>Run(CreateInvoice),"\uE710");newInvoiceButton.ToolTip="Create invoice (Ctrl+N)";workflows.Children.Add(newInvoiceButton);
        workflows.Children.Add(CompactButton("Reminders",()=>Run(ShowFilteredReminders),"\uE787"));
        toolbar.Children.Add(workflows);
        masterActions.Children.Add(CompactButton("Add New Receipt",()=>Run(CreateTransferReceipt),"\uE710"));masterActions.Children.Add(CompactButton("New Party",()=>Run(CreateParty),"\uE710"));masterActions.Children.Add(CompactButton("New Agent",()=>Run(CreateAgent),"\uE710"));toolbar.Children.Add(masterActions);masterActions.Visibility=Visibility.Collapsed;
        notification=new Border{Background=Ui.Color("#FFF2F0"),BorderBrush=Ui.Color("#F3C8C3"),BorderThickness=new(1),Padding=new(10,6,10,6),Margin=new(10,0,10,4),Child=message,Visibility=Visibility.Collapsed};DockPanel.SetDock(notification,Dock.Top);root.Children.Add(notification);
        var sheet=new Border{Background=Ui.Color("#FFFFFF"),BorderBrush=Ui.Color("#DCE3ED"),BorderThickness=new(1),Margin=new(10,0,10,4),Child=sheetArea};root.Children.Add(sheet);
        opening.TextChanged+=(_,_)=>{if(!refreshing&&!generatedView)MarkDirty();};terms.TextChanged+=(_,_)=>{if(!refreshing&&!generatedView)MarkDirty();};
        System.Windows.Automation.AutomationProperties.SetName(opening,"Opening balance in USD");System.Windows.Automation.AutomationProperties.SetName(terms,"Payment terms in calendar days");
        Ui.WindowFrame(this,root,"TradeVault",embeddedControls:true);
        ToolTipService.SetToolTip(revision,"Storage: "+path);Closing+=(_,e)=>{if(busy||(!App.AutomatedRun&&!Discard()))e.Cancel=true;};
        RegisterShortcuts();
        PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Z or Key.Y&&Keyboard.Modifiers==ModifierKeys.Control&&Keyboard.FocusedElement is TextBox&&grid?.Table.IsKeyboardFocusWithin!=true)return;if((e.Key==Key.Delete||(e.Key==Key.X&&Keyboard.Modifiers==ModifierKeys.Control))&&(grid==null||!grid.Table.IsKeyboardFocusWithin||Keyboard.FocusedElement is TextBox))return;if(e.IsRepeat&&shortcuts.Any(s=>s.Key==e.Key&&s.Modifiers==Keyboard.Modifiers)){e.Handled=true;return;}if(TryShortcut(e.Key,Keyboard.Modifiers))e.Handled=true;};
        System.Windows.Automation.AutomationProperties.SetName(partySelector,"Select party");
        RefreshParties(AllPartiesId);
        partySelector.SelectionChanged+=(_,_)=>SwitchParty();
        Open("form",AllPartiesId);
        Loaded+=(_,_)=>Run(ShowOpeningReminders);
    }
    internal void CheckMasterPages()
    {
        void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        Check(nav.Keys.SequenceEqual(new[]{"parties","agents","form","partyReceipt","agentReceipt","reports"}),"Six workspace tabs must appear in order.");
        Check(backupButton.Parent is StackPanel controls&&controls.Children.Count==4&&ReferenceEquals(controls.Children[0],backupButton)&&System.Windows.Automation.AutomationProperties.GetName((Button)controls.Children[1])=="Minimize"&&System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(backupButton),"Backups must be clickable immediately to the left of Minimize.");
        Open("form");Check(title.ToolTip==null&&!ToolTipService.GetIsEnabled(nav["form"]),"Invoice Register must not display a hover tooltip.");
        Check(masterActions.Visibility==Visibility.Collapsed&&!partyTools.Children.OfType<Button>().Any(),"Invoice Register must not offer party or agent creation.");
        foreach(var name in new[]{"parties","agents"})
        {
            Navigate(name);Check(grid!=null&&grid.Table.IsReadOnly&&grid.Table.Columns.Count==5,"Master page requires a read-only five-column grid.");
            CheckPageExport();Check(grid!.Table.Columns[0].Header?.ToString()==(name=="parties"?"Party Name":"Agent Name")&&grid.Table.Columns[1].Header?.ToString()=="Invoices","Master headers differ.");
            var expected=name=="parties"?store.GetPartyMaster().Select(e=>(e.Name,e.Invoices)):store.GetAgents().Select(e=>(e.Name,e.Invoices));
            Check(grid.Table.Items.Cast<GridRow>().Select(row=>(row["A"],int.Parse(row["B"]))).SequenceEqual(expected),"Master invoice counts are stale or incorrect.");
            Check(masterActions.Children[name=="parties"?1:2].Visibility==Visibility.Visible&&partyTools.Visibility==Visibility.Collapsed,"Creation must stay in the appropriate master page.");
        }
        foreach(var name in new[]{"partyReceipt","agentReceipt"})
        {
            Navigate(name);Check(grid!=null&&grid.Table.IsReadOnly&&grid.Table.Columns.Count==(name=="partyReceipt"?6:5)&&optionBar.Visibility==Visibility.Visible,"Receipt pages require saved read-only tables.");
        }
        Navigate("reports");Check(sheetArea.Children.Count>0&&optionBar.Visibility==Visibility.Collapsed,"Reports must show its three report options.");
        Navigate("form");CheckNavigationLayout();
    }
    internal void CheckInvoiceFilters()
    {
        void Check(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        var first=store.CreateParty("SYNTHETIC filter party one");var second=store.CreateParty("SYNTHETIC filter party two");
        var agentA=store.CreateAgent("SYNTHETIC filter agent A");var agentB=store.CreateAgent("SYNTHETIC filter agent B");
        store.CreateRegisterInvoice(first.Id,0,new("India",agentA.Name,"FILTER-ONE-A",new(2026,5,1),agentA.Id));
        store.CreateRegisterInvoice(first.Id,1,new("India",agentB.Name,"FILTER-ONE-B",new(2026,5,1),agentB.Id));
        store.CreateRegisterInvoice(second.Id,0,new("India",agentA.Name,"FILTER-TWO-A",new(2026,5,1),agentA.Id));
        foreach(var party in new[]{first,second})
        {
            var snapshot=store.Load("form",party.Id);foreach(var row in snapshot.Rows){row["F9"]=row["F3"];row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F7"]="9000";row["F17"]="2026-06-17";}store.Save("form",snapshot.Revision,snapshot.Rows,party.Id);
        }
        void Agent(string id)=>agentSelector.SelectedItem=agentSelector.Items.Cast<AgentMasterEntry>().Single(agent=>agent.Id==id);
        PreviewParty("form",first.Id);Agent(agentA.Id);
        CheckPageExport();Check(grid!.Table.Items.Count==1&&controller.Records.Count()==2&&!dirty,"Party and agent filters must intersect without changing records.");
        grid.Measure(new(1100,600));grid.Arrange(new(0,0,1100,600));grid.UpdateLayout();
        grid.FilterForCheck("NO SUCH INVOICE");Check(grid.Table.Items.Count==0,"Search must combine with the agent filter.");grid.FilterForCheck("");Check(grid.Table.Items.Count==1,"Clearing search must preserve the agent filter.");
        var visible=(GridRow)grid.Table.Items[0];visible["F11"]="FILTERED EDIT";Calculate();Save();
        var saved=store.Load("form",first.Id);Check(saved.Rows.Count(row=>!row.ContainsKey("_metadata"))==2&&saved.Rows.Any(row=>row.GetValueOrDefault("F3")=="FILTER-ONE-B"),"Saving a filtered register must preserve hidden invoices.");
        Agent(agentB.Id);Check(grid.Table.Items.Count==1,"Second agent filter returned wrong records.");
        partySelector.SelectedItem=partySelector.Items.Cast<WorksheetParty>().Single(party=>party.Id==second.Id);
        Check(grid!.Table.Items.Count==0,"Party/agent combination with no matches must be empty.");Agent("");Check(grid.Table.Items.Count==1,"All agents must restore this party's invoices.");
        partySelector.SelectedItem=partySelector.Items.Cast<WorksheetParty>().Single(party=>party.Id==AllPartiesId);Agent(agentA.Id);
        Check(allPartiesView&&grid!.Table.IsReadOnly&&grid.Table.Items.Count==2,"Agent-only filter must include matching invoices across parties.");
        if(CurrentReminders().Count!=2||workflows.Children.OfType<Button>().Any(button=>!button.IsEnabled))throw new InvalidOperationException("All-party reminders must remain enabled and include matching invoices across parties.");
        ReloadSaved();Check(allPartiesView&&grid!.Table.Items.Count==2&&selectedAgentId==agentA.Id,"Reload must retain the all-party and agent filters.");
        partySelector.SelectedItem=partySelector.Items.Cast<WorksheetParty>().Single(party=>party.Id==first.Id);
        Check(!allPartiesView&&!grid!.Table.IsReadOnly&&grid.Table.Items.Count==1,"Selecting a party must restore editable intersected results.");
        var previousInput=NewInvoiceInput;
        try{NewInvoiceInput=()=>new("India",agentB.Name,"FILTER-NEW-B",new(2026,5,1),agentB.Id);CreateInvoice();Check(selectedAgentId==agentB.Id&&grid!.Table.Items.Cast<GridRow>().Any(row=>row["F3"]=="FILTER-NEW-B"),"A newly created invoice must remain visible when it uses a different agent.");}
        finally{NewInvoiceInput=previousInput;}
        Agent("");PreviewParty("form",Guid.Empty);
    }
    internal void CheckNavigationLayout()
    {
        var content=(FrameworkElement)Content;content.Measure(new(1366,728));content.Arrange(new(0,0,1366,728));content.UpdateLayout();
        foreach(var name in new[]{"parties","agents","form","partyReceipt","agentReceipt","reports"})
        {
            var button=nav[name];var point=button.TranslatePoint(new(0,0),content);
            if(button.ActualWidth<=0||point.Y<0||point.Y+button.ActualHeight>728||point.X<0||point.X+button.ActualWidth>1366)throw new InvalidOperationException("Worksheet navigation is outside the visible workspace: "+name);
        }
    }
    private static Button CompactButton(string text,Action action,string glyph){var b=Ui.Button(text,action,"QuietButton",glyph);b.FontFamily=new("Segoe UI Variable Text, Segoe UI");b.FontSize=12;b.FontWeight=FontWeights.Medium;b.MinHeight=30;b.Padding=new(9,4,9,4);b.Margin=new(4,0,0,0);b.ToolTip=text switch{"Export Excel"=>"Export Excel reports (Ctrl+E)","Preview / Print"=>"Preview or print reports (Ctrl+P)","Receipts"=>"Customer payments (Ctrl+Shift+R)",_=>text};return b;}
    private void ReloadSaved(){if(activePage!="form"){Navigate(activePage);return;}if(allPartiesView){OpenAllInvoices();return;}if(Discard())Open(controller.Name);}
    private void RegisterShortcuts()
    {
        void AddKey(Key key,ModifierKeys modifiers,string gesture,string description,Action action)=>shortcuts.Add((key,modifiers,gesture,description,action));
        AddKey(Key.S,ModifierKeys.Control,"Ctrl+S","Save changes",()=>{if(grid!=null&&(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true)))throw new ArgumentException("Correct invalid cells before saving.");if(dirty)Save();});
        AddKey(Key.N,ModifierKeys.Control,"Ctrl+N","New invoice / receipt",()=>{if(activePage is "partyReceipt" or "agentReceipt")CreateTransferReceipt();else Add();});
        AddKey(Key.N,ModifierKeys.Control|ModifierKeys.Shift,"Ctrl+Shift+N","Create a party",CreateParty);
        AddKey(Key.Delete,ModifierKeys.None,"Delete","Clear cells / delete whole row with confirmation",Delete);
        AddKey(Key.X,ModifierKeys.Control,"Ctrl+X","Cut selected cells / whole record without confirmation",CutSelection);
        AddKey(Key.Z,ModifierKeys.Control,"Ctrl+Z","Undo worksheet edit, cut or deletion",()=>ReplayHistory(false));
        AddKey(Key.Y,ModifierKeys.Control,"Ctrl+Y","Redo worksheet edit, cut or deletion",()=>ReplayHistory(true));
        AddKey(Key.F,ModifierKeys.Control,"Ctrl+F","Search the current worksheet",()=>grid?.FocusSearch());
        AddKey(Key.F5,ModifierKeys.None,"F5","Reload saved records",ReloadSaved);
        AddKey(Key.L,ModifierKeys.Control,"Ctrl+L","Choose a party",()=>{partySelector.Focus();partySelector.IsDropDownOpen=true;});
        AddKey(Key.D1,ModifierKeys.Control,"Ctrl+1","Open Party Master",()=>Navigate("parties"));
        AddKey(Key.D2,ModifierKeys.Control,"Ctrl+2","Open Agent Master",()=>Navigate("agents"));
        AddKey(Key.D3,ModifierKeys.Control,"Ctrl+3","Open Invoice Register",()=>Navigate("form"));
        AddKey(Key.D4,ModifierKeys.Control,"Ctrl+4","Open Party Receipt",()=>Navigate("partyReceipt"));
        AddKey(Key.D5,ModifierKeys.Control,"Ctrl+5","Open Agent Receipt",()=>Navigate("agentReceipt"));
        AddKey(Key.D6,ModifierKeys.Control,"Ctrl+6","Open Reports",()=>Navigate("reports"));
        AddKey(Key.E,ModifierKeys.Control,"Ctrl+E","Export current page to Excel",ExportReports);
        AddKey(Key.P,ModifierKeys.Control,"Ctrl+P","Export current page to PDF",()=>ExportCurrentPage(true));
        AddKey(Key.F1,ModifierKeys.None,"F1","Show keyboard shortcuts",ShowShortcuts);
    }
    internal bool TryShortcut(Key key,ModifierKeys modifiers)
    {
        if(activePage!="form"&&(key is Key.Delete or Key.X or Key.Z or Key.Y  or Key.R or Key.L))return false;
        var item=shortcuts.FirstOrDefault(s=>s.Key==key&&s.Modifiers==modifiers);if(item.Action==null)return false;
        if(!busy)Run(item.Action);return true;
    }
    private void ShowShortcuts()
    {
        var dialog=new Window{Owner=this,Width=650,Height=Math.Min(610,SystemParameters.WorkArea.Height),WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new StackPanel{Margin=new(18)};panel.Children.Add(Ui.Text("Keyboard shortcuts",20,"#18243B",true));
        foreach(var item in shortcuts){var line=new DockPanel{Margin=new(0,10,0,0)};var gesture=Ui.Text(item.Gesture,13,"#2458D3",true);gesture.Width=180;DockPanel.SetDock(gesture,Dock.Left);line.Children.Add(gesture);line.Children.Add(Ui.Text(item.Description));panel.Children.Add(line);}
        panel.Children.Add(Ui.Text("In grids: Tab / Shift+Tab move between inputs. Ctrl+C / Ctrl+V copy and paste. Click a row number to select a whole record. Delete clears cells; whole-row deletion asks for confirmation. Ctrl+X cuts cells or a whole record without confirmation. Whole-record Cut/Delete leave that row blank and keep lower records in place. Ctrl+Z undoes and Ctrl+Y redoes worksheet changes. History is kept for the active worksheet until you reload, change party or switch tabs. Undoing a cut/deletion restores the stored record immediately; ordinary cell edits still use Save changes. While editing text, Delete and Ctrl+X use normal text-editing behavior.",12,"#64748B"));
        var close=Ui.Button("Close",()=>dialog.Close());close.IsCancel=true;close.Margin=new(0,14,0,0);panel.Children.Add(close);
        Ui.WindowFrame(dialog,new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},"Keyboard shortcuts");dialog.ShowDialog();
    }
    private void RequireSaved(){if(dirty)throw new ArgumentException("Save or reload your current changes first.");}
    private void Navigate(string name)
    {
        if(grid!=null&&(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true)))throw new ArgumentException("Correct the highlighted invoice cells before opening another tab.");
        if(dirty)Save();
        if(name is "parties" or "agents" or "partyReceipt" or "agentReceipt" or "reports")OpenPage(name);else Open(name,(partySelector.SelectedItem as WorksheetParty)?.Id);
    }
    private void ShowOpeningReminders()
    {
        int overdue=0,soon=0,unavailable=0,configured=0;
        foreach(var party in store.GetParties().Where(p=>!p.IsUnassigned))
        {
            configured++;try{var reminders=workflow.Reminders(party.Id,DateOnly.FromDateTime(DateTime.Today));overdue+=reminders.Count(r=>r.Status=="Overdue");soon+=reminders.Count(r=>r.Status=="Due within 30 days");}
            catch(TradeVault.Domain.BusinessRuleException){unavailable++;}
        }
        if(configured>0)status.Text=$"{overdue} overdue · {soon} due soon · {unavailable} party report(s) need validation · open Reminders for the selected party";
    }
    private void ExportReports()=>ExportCurrentPage(false);
    private void RequireParty(){if(controller.PartyId==Guid.Empty)throw new ArgumentException("Select a named party first.");}
    private void ChangeSelectedTerms()
    {
        if(controller.Name!="form"||grid?.Table.CurrentItem is not GridRow row||!controller.Rows.Contains(row))throw new ArgumentException("Select an invoice row first.");
        var dialog=new Window{Owner=this,Width=420,Height=230,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel{Margin=new(18)};
        panel.Children.Add(Ui.Text("Terms for "+row["F9"],18,"#18243B",true));var input=new TextBox{Text=row["_terms"],Margin=new(0,12,0,12)};panel.Children.Add(input);
        panel.Children.Add(Ui.Button("Apply to this invoice",()=>Run(()=>{if(!int.TryParse(input.Text,out var days)||days<0||days>3650)throw new ArgumentException("Terms must be 0–3650 days.");row["_terms"]=days.ToString();MarkDirty();Calculate();dialog.Close();}),"PrimaryButton"));Ui.WindowFrame(dialog,panel,"Invoice terms");dialog.ShowDialog();
    }
    private void Run(Action action){try{action();}catch(Exception ex){Error(ex.Message);}}
    private void Error(string text){message.Text=text;notification.Visibility=Visibility.Visible;status.Text="Action needs attention";status.Foreground=Ui.Color("#B42318");}
    private void SetSaveState(bool pending)
    {
        saveButton.IsEnabled=pending;saveButton.SetResourceReference(StyleProperty,"SaveActionButton");
        var color=pending?"#FFFFFF":"#407057";saveButton.Background=pending?AppTheme.AccentFill:Ui.Color("#F3F8F5");saveButton.BorderBrush=pending?AppTheme.AccentFill:Ui.Color("#D4E2D9");saveButton.Foreground=pending?Brushes.White:Ui.Color(color);
        var content=new StackPanel{Orientation=Orientation.Horizontal};var icon=Ui.Icon(pending?"\uE74E":"\uE73E",color,14);icon.Margin=new(0,0,7,0);content.Children.Add(icon);content.Children.Add(Ui.Text(pending?"Save changes":generatedView?"Read-only":"Saved",12,color,true));saveButton.Content=content;
        saveButton.ToolTip=pending?"Save the current worksheet (Ctrl+S)":"All changes are saved. No action is needed.";
        System.Windows.Automation.AutomationProperties.SetName(saveButton,pending?"Save changes, Control S":generatedView?"Read-only report":"All changes saved");
    }
    private void RefreshThemeButton()
    {
        var label=AppTheme.IsDark?"Switch to light theme":"Switch to dark theme";
        themeButton.ToolTip=label;System.Windows.Automation.AutomationProperties.SetName(themeButton,label);
        var geometry=AppTheme.IsDark?"M8,4 A4,4 0 1 0 8,12 A4,4 0 1 0 8,4 M8,0 V2 M8,14 V16 M0,8 H2 M14,8 H16 M2.3,2.3 L3.7,3.7 M12.3,12.3 L13.7,13.7 M13.7,2.3 L12.3,3.7 M3.7,12.3 L2.3,13.7":"M11.5,1.5 A7,7 0 1 0 14.5,11 A6,6 0 0 1 11.5,1.5 Z";
        var icon=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),Width=16,Height=16,Stretch=Stretch.Uniform,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){Source=themeButton});themeButton.Content=icon;
    }
    private void MarkDirty(){dirty=true;SetSaveState(true);status.Text="● Unsaved changes";status.Foreground=Ui.Color("#9A6700");}
    private void Watch(GridRow row)=>row.Changed+=_=>{if(!refreshing){refreshing=true;try{controller.ActivateBlank(row);}finally{refreshing=false;}MarkDirty();UpdateRecords();if(!calculationPending){calculationPending=true;Dispatcher.BeginInvoke(new Action(()=>{calculationPending=false;if(dirty&&!generatedView)Calculate();}),System.Windows.Threading.DispatcherPriority.Background);}}};
    private bool Discard(){grid?.Table.CommitEdit();grid?.Table.CommitEdit(DataGridEditingUnit.Row,true);return !dirty||MessageBox.Show(this,"You have unsaved changes. Discard them and continue?","Unsaved changes",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
    private void Open(string name,Guid? partyId=null)
    {
        if(name=="form"&&partyId==AllPartiesId){OpenAllInvoices();return;}
        allPartiesView=false;workflows.IsEnabled=true;foreach(UIElement action in workflows.Children)action.IsEnabled=true;agentFilterGroup.Visibility=name=="form"?Visibility.Visible:Visibility.Collapsed;
        activePage=name;partyTools.Visibility=Visibility.Visible;optionBar.Visibility=Visibility.Visible;workflows.Visibility=Visibility.Visible;options.Visibility=Visibility.Visible;masterActions.Visibility=Visibility.Collapsed;
        var selected=partyId??controller.PartyId;var next=new WorksheetController(store);next.Load(name,selected);
        if((partySelector.SelectedItem as WorksheetParty)?.Id!=selected)RefreshParties(selected);
        var nextGenerated=name!="form"&&selected!=Guid.Empty;
        next.AsOf=DateOnly.FromDateTime(DateTime.Today);next.Calculate();
        var nextReport=nextGenerated?workflow.BuildCurrent(selected):null;
        controller=next;refreshing=true;opening.Text=controller.Opening.ToString("0.00",CultureInfo.InvariantCulture);terms.Text=controller.Terms.ToString();refreshing=false;
        foreach(var row in controller.Rows)Watch(row);
        title.Text=(name switch{"form"=>"Invoice register","details"=>"Account details",_=>"Debit & credit"})+" · "+controller.PartyName;
        title.ToolTip=null;
        partySelector.ToolTip=controller.PartyName;
        foreach(var item in nav){item.Value.Background=Ui.Color(item.Key==name?"#E8EFFD":"#FFFFFF");item.Value.Foreground=Ui.Color(item.Key==name?"#2458D3":"#52627A");}
        openingGroup.Visibility=Visibility.Collapsed;termsGroup.Visibility=Visibility.Collapsed;newInvoiceButton.Visibility=name=="form"?Visibility.Visible:Visibility.Collapsed;
        generatedView=nextGenerated;
        if(generatedView){grid=WorkflowWindows.ReportGrid(nextReport!.Tables[name=="details"?0:1]);title.Text+=" · generated";}
        else grid=new ExcelGrid(controller.Fields.ToArray(),controller.Rows,promote:name=="form"?null:row=>{controller.Add(row);Watch(row);MarkDirty();UpdateRecords();},frozenColumns:name=="form"?3:2);
        if(name=="form")
        {
            grid.CanEditRecord=row=>row["_blank"]!="1";
            foreach(var column in grid.Table.Columns.Take(3))column.CanUserReorder=false;
            grid.Table.BeginningEdit+=(_,e)=>{if(e.Row.Item is GridRow row&&row["_blank"]=="1")e.Cancel=true;};
        }
        grid.Table.RowHeight=28;grid.BeforeChange+=CaptureHistory;grid.Edited+=Calculate;
        if(name=="form"){RefreshAgents();ApplyAgentFilter();}
        grid.Table.SelectedCellsChanged+=(_,_)=>UpdateRecords();
        sheetArea.Children.Clear();sheetArea.Children.Add(grid);UpdateRecords();notification.Visibility=Visibility.Collapsed;dirty=false;SetSaveState(false);openingGroup.IsEnabled=!generatedView;
        status.Text="All changes saved";status.Foreground=Ui.Color("#64748B");revision.Text=$"Revision {controller.Revision} · Local storage";
        assignButton.Visibility=controller.PartyId==Guid.Empty?Visibility.Visible:Visibility.Collapsed;
        assignButton.IsEnabled=controller.Records.Any();
        history.Reset(controller.CaptureEditState());RefreshPageHeading();
    }
    private void UpdateRecords(){grid?.RefreshRecordCount();deleteButton.IsEnabled=!generatedView&&grid?.Table.CurrentItem is GridRow row&&controller.Rows.Contains(row)&&row["_blank"]!="1";assignButton.IsEnabled=controller.PartyId==Guid.Empty&&controller.Records.Any()&&!dirty;}
    private void Add(){if(activePage!="form")return;if(controller.Name=="form"){CreateInvoice();return;}if(grid==null||generatedView)return;var row=controller.Add();if(controller.Name=="form"&&string.IsNullOrWhiteSpace(row["F1"]))row["F1"]=controller.PartyName;Watch(row);MarkDirty();UpdateRecords();grid.Table.ScrollIntoView(row);grid.Table.CurrentCell=new DataGridCellInfo(row,grid.Table.Columns.First(c=>!c.IsReadOnly));grid.Table.Focus();grid.Table.BeginEdit();CaptureHistory();}
    private void CreateInvoice()
    {
        if(activePage!="form")return;
        if(grid!=null&&(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true)))throw new ArgumentException("Correct invalid cells before creating an invoice.");
        if(dirty)Save();
        string? id;var targetParty=controller.PartyId;
        if(NewInvoiceInput!=null){var input=NewInvoiceInput();if(input==null)return;id=store.CreateRegisterInvoice(controller.PartyId,controller.Revision,input);}
        else {var dialog=new NewInvoiceDialog(store,allPartiesView?Guid.Empty:controller.PartyId,controller.Revision,selectedAgentId){Owner=this};if(dialog.ShowDialog()!=true)return;id=dialog.CreatedId;targetParty=dialog.CreatedPartyId;}
        RefreshParties(targetParty);Open("form",targetParty);var row=controller.Rows.Single(r=>r["_id"]==id);if(!grid!.Table.Items.Contains(row)){selectedAgentId=row["_agentId"];RefreshAgents();ApplyAgentFilter();}grid.Table.ScrollIntoView(row);grid.Table.CurrentCell=new(row,grid.Table.Columns.Single(c=>c.SortMemberPath=="[F5]"));grid.Table.Focus();status.Text="Invoice created · enter amounts and shipment details";
    }
    private void Delete()
    {
        if(grid==null)return;if(grid.Table.CurrentItem is GridRow blank&&blank["_blank"]=="1"&&grid.Table.SelectedCells.All(c=>ReferenceEquals(c.Item,blank)))return;if(grid.WholeSelectedRecord==null){grid.ClearSelectedCells();return;}DeleteRecord(false);
    }
    private void CutSelection()
    {
        if(grid==null)return;if(grid.Table.CurrentItem is GridRow blank&&blank["_blank"]=="1"&&grid.Table.SelectedCells.All(c=>ReferenceEquals(c.Item,blank)))return;if(grid.WholeSelectedRecord==null){grid.CutSelectedCells();return;}DeleteRecord(true);
    }
    private void DeleteRecord(bool cut)
    {
        if(generatedView)throw new ArgumentException("This report is read-only. Select an invoice in Invoice register to delete a record.");
        if(grid?.WholeSelectedRecord is not GridRow row||!controller.Rows.Contains(row))throw new ArgumentException("Select a record in the worksheet first.");
        if(controller.Name=="form")workflow.EnsureInvoiceCanDelete(controller.PartyId,row.Snapshot());
        var reference=controller.Name=="form"?row["F9"]:row["B"];if(string.IsNullOrWhiteSpace(reference))reference="the selected draft row";
        var text=$"Delete {reference} for {controller.PartyName}?\n\nThis record will be deleted and saved immediately. Other unsaved edits will not be saved.";
        var confirmed=cut||(ConfirmRecordDeletion?.Invoke(text)??ConfirmDeleteWindow.Confirm(this,reference,controller.PartyName));
        if(!confirmed)return;
        CaptureHistory();var index=controller.Rows.IndexOf(row);var snapshot=store.Load(controller.Name,controller.PartyId);
        var records=snapshot.Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
        var stored=controller.Name=="form"?records.SingleOrDefault(r=>ReportWorkflow.InvoiceKey(controller.PartyId,r)==ReportWorkflow.InvoiceKey(controller.PartyId,row.Snapshot())):records.ElementAtOrDefault(index);
        if(cut)grid.CopyRecordForCut(row);grid.Table.CancelEdit();grid.Table.CancelEdit(DataGridEditingUnit.Row);
        refreshing=true;try{dirty=controller.DeleteImmediately(row);Watch(controller.Rows[index]);}finally{refreshing=false;}
        history.Capture(controller.CaptureEditState(),new(row.Snapshot(),index){StoredData=stored==null?null:new(stored)});
        SetSaveState(dirty);UpdateRecords();revision.Text=$"Revision {controller.Revision} · Local storage";notification.Visibility=Visibility.Collapsed;status.Text=(cut?"Record cut to clipboard; row left blank":"Record cleared and saved; row left blank")+(dirty?" · other edits remain unsaved":"");status.Foreground=Ui.Color("#247653");
    }
    private void CaptureHistory(){if(grid!=null&&!generatedView&&!refreshing)history.Capture(controller.CaptureEditState());}
    private void ReplayHistory(bool redo)
    {
        if(grid==null||generatedView)return;
        CaptureHistory();var step=redo?history.RedoStep:history.UndoStep;if(step==null)return;
        var nextRevision=controller.Revision;
        if(step.Removed is {} removed)
        {
            var id=controller.Name=="form"?ReportWorkflow.InvoiceKey(controller.PartyId,removed.Data):null;
            if(redo)
            {
                var records=store.Load(controller.Name,controller.PartyId).Rows.Where(r=>!r.ContainsKey("_metadata")).ToList();
                var stored=controller.Name=="form"?records.SingleOrDefault(r=>ReportWorkflow.InvoiceKey(controller.PartyId,r)==id):records.ElementAtOrDefault(removed.Index);
                nextRevision=store.DeleteWorksheetRecord(controller.Name,controller.PartyId,controller.Revision,removed.Index,id);
                removed.StoredData=stored==null?null:new(stored);
            }
            else if(removed.StoredData is {} data)nextRevision=store.RestoreWorksheetRecord(controller.Name,controller.PartyId,controller.Revision,removed.Index,data);
        }
        grid.Table.CancelEdit();grid.Table.CancelEdit(DataGridEditingUnit.Row);refreshing=true;
        try
        {
            controller.RestoreEditState(redo?step.After:step.Before,nextRevision);
            foreach(var row in controller.Rows)Watch(row);
            opening.Text=controller.Opening.ToString("0.00",CultureInfo.InvariantCulture);terms.Text=controller.Terms.ToString();
        }
        finally{refreshing=false;}
        if(redo)history.CompleteRedo();else history.CompleteUndo();
        dirty=controller.HasSavedDifferences();SetSaveState(dirty);UpdateRecords();notification.Visibility=Visibility.Collapsed;
        revision.Text=$"Revision {controller.Revision} · Local storage";status.Text=(redo?"Change redone":"Change undone")+(dirty?" · unsaved changes":"");status.Foreground=Ui.Color("#247653");
        var target=grid.Table.Items.Cast<GridRow>().FirstOrDefault();var column=grid.Table.Columns.FirstOrDefault(c=>!c.IsReadOnly);
        if(target!=null&&column!=null){grid.Table.CurrentCell=new(target,column);grid.Table.SelectedCells.Clear();grid.Table.SelectedCells.Add(grid.Table.CurrentCell);}
        grid.Table.Focus();
    }
    private void Options(){if(!decimal.TryParse(opening.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||decimal.Round(value,2)!=value)throw new ArgumentException("Opening balance: enter USD with at most two decimals.");if(!int.TryParse(terms.Text,out var days)||days<0||days>3650)throw new ArgumentException("Payment terms must be 0–3650 days.");controller.Opening=value;controller.Terms=days;controller.AsOf=DateOnly.FromDateTime(DateTime.Today); }
    private void Calculate(){if(grid==null)return;try{Options();if(generatedView){var report=workflow.BuildCurrent(controller.PartyId);grid=WorkflowWindows.ReportGrid(report.Tables[controller.Name=="details"?0:1]);sheetArea.Children.Clear();sheetArea.Children.Add(grid);status.Text="Generated report refreshed · "+controller.AsOf.ToString("dd-MM-yyyy");return;}refreshing=true;controller.Calculate();controller.Validate(false);notification.Visibility=Visibility.Collapsed;status.Text=dirty?"● Unsaved changes":"All changes saved";status.Foreground=Ui.Color(dirty?"#9A6700":"#64748B");}catch(Exception ex){Error(ex.Message);}finally{refreshing=false;CaptureHistory();}}
    private void Save()
    {
        if(grid==null||generatedView)return;
        if(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true))throw new ArgumentException("Correct invalid cells before saving.");
        Options();CaptureHistory();refreshing=true;
        try
        {
            controller.Save();dirty=false;SetSaveState(false);notification.Visibility=Visibility.Collapsed;
            status.Foreground=Ui.Color("#247653");status.Text="Changes saved";revision.Text=$"Revision {controller.Revision} · Local storage";UpdateRecords();
        }
        finally{refreshing=false;}
    }
    internal void PreviewSheet(string name){if(name is "parties" or "agents" or "partyReceipt" or "agentReceipt" or "reports")OpenPage(name);else Open(name);}
    internal void PreviewParty(string name,Guid party){RefreshParties(party);Open(name,party);}
    internal void CheckWorkflowInteractions()
    {
        var party=store.CreateParty("Workflow UI test");var input=new WorksheetController(store);input.Load("form",party.Id);var row=input.Add();row["F9"]="WF-UI-1";row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";row["F7"]="9000";row["F17"]="2026-06-17";input.Save();

        PreviewParty("details",party.Id);
        if(!generatedView||grid?.Table.IsReadOnly!=true||((GridRow)grid.Table.Items[0])["F"]!="10000.00")throw new InvalidOperationException("Generated report UI did not use register source.");
        Save();if(store.Load("details",party.Id).Rows.Count!=0)throw new InvalidOperationException("Generated view overwrote legacy worksheet.");
        PreviewParty("form",party.Id);controller.Rows[0]["F5"]="16000";Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        if(controller.Rows[0]["F8"]!="7000.00")throw new InvalidOperationException("Automatic calculation did not refresh without a Recalculate button.");Navigate("details");
        if(((GridRow)grid!.Table.Items[0])["C"]!="16000.00")throw new InvalidOperationException("Invoice edits did not save when opening Account details.");
        Navigate("ledger");if(grid!.Table.Items.Cast<GridRow>().Last()["E"]!="16000.00")throw new InvalidOperationException("Invoice edit did not reach debit/credit.");
        Navigate("form");controller.Rows[0]["F5"]="15000";Navigate("details");
        workflow.RecordReceipt(party.Id,new(2026,7,1),5000,"UI workflow test",[new(ReportWorkflow.InvoiceKey(party.Id,row.Snapshot()),"Bank",5000)]);
        PreviewParty("details",party.Id);if(((GridRow)grid.Table.Items[0])["F"]!="5000.00")throw new InvalidOperationException("Generated UI did not reflect dated receipt.");
        PreviewParty("form",party.Id);var future=controller.Add();Watch(future);future["F9"]="WF-FUTURE";future["F10"]="2026-11-20";future["F5"]="15000";future["F6"]="10000";Save();
        Navigate("details");if(grid!.Table.Items.Count!=2)throw new InvalidOperationException("All-records Account Details excluded a saved future invoice.");
        Navigate("ledger");if(!grid!.Table.Items.Cast<GridRow>().Any(r=>r["B"].Contains("WF-FUTURE")))throw new InvalidOperationException("All-records ledger excluded a saved invoice.");
        Navigate("form");if(grid!.Table.Columns.Count!=controller.Fields.Count||grid.Table.Columns[0].SortMemberPath!="[F1]"||!grid.Table.Columns[0].IsReadOnly||!title.Text.Contains(party.Name))throw new InvalidOperationException("Party heading/hidden party column failed.");
        PreviewParty("form",Guid.Empty);
    }
    private void RefreshParties(Guid selected)
    {
        changingParty=true;
        try{var parties=new[]{new WorksheetParty(AllPartiesId,"All parties",false)}.Concat(store.GetParties().Where(p=>!p.IsUnassigned)).ToList();partySelector.ItemsSource=parties;partySelector.SelectedItem=parties.FirstOrDefault(p=>p.Id==selected)??parties[0];}
        finally{changingParty=false;}
    }
    private void SwitchParty()
    {
        if(changingParty||partySelector.SelectedItem is not WorksheetParty party)return;
        if(party.Id==(allPartiesView?AllPartiesId:controller.PartyId))return;
        try{if(Discard()){Open(controller.Name,party.Id);return;}}
        catch(Exception ex){Error(ex.Message);}
        RefreshParties(allPartiesView?AllPartiesId:controller.PartyId);
    }
    private void RefreshAgents()
    {
        changingAgent=true;
        try{var agents=new[]{new AgentMasterEntry("","All agents",0)}.Concat(store.GetAgents()).ToList();agentSelector.ItemsSource=agents;agentSelector.SelectedItem=agents.FirstOrDefault(agent=>agent.Id==selectedAgentId)??agents[0];selectedAgentId=((AgentMasterEntry)agentSelector.SelectedItem).Id;}
        finally{changingAgent=false;}
    }
    private void ChangeAgentFilter()
    {
        if(changingAgent||agentSelector.SelectedItem is not AgentMasterEntry selected)return;
        if(grid!=null&&(!grid.Table.CommitEdit()||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true)))
        {
            changingAgent=true;try{agentSelector.SelectedItem=agentSelector.Items.Cast<AgentMasterEntry>().First(agent=>agent.Id==selectedAgentId);}finally{changingAgent=false;}Error("Correct invalid cells before changing the agent filter.");return;
        }
        selectedAgentId=selected.Id;ApplyAgentFilter();
    }
    private void ApplyAgentFilter()
    {
        if(activePage!="form"||grid==null)return;
        var agent=agentSelector.SelectedItem as AgentMasterEntry;
        grid.SetRowFilter(agent==null||agent.Id.Length==0?null:row=>row["_blank"]!="1"&&(row["_agentId"].Length>0?row["_agentId"]==agent.Id:string.Equals(row["F27"].Trim(),agent.Name.Trim(),StringComparison.OrdinalIgnoreCase)));
        UpdateRecords();RefreshPageHeading();
    }
    private void OpenAllInvoices()
    {
        var rows=new System.Collections.ObjectModel.ObservableCollection<GridRow>();
        foreach(var party in store.GetParties())
        {
            var sheet=new WorksheetController(store);sheet.Load("form",party.Id);
            foreach(var row in sheet.Records)rows.Add(row);
        }
        activePage="form";allPartiesView=true;generatedView=true;dirty=false;
        partyTools.Visibility=Visibility.Visible;agentFilterGroup.Visibility=Visibility.Visible;optionBar.Visibility=Visibility.Visible;workflows.Visibility=Visibility.Visible;workflows.IsEnabled=true;foreach(UIElement action in workflows.Children)action.IsEnabled=true;options.Visibility=Visibility.Collapsed;masterActions.Visibility=Visibility.Collapsed;
        title.Text="Invoice register · All parties";title.ToolTip=null;partySelector.ToolTip=null;
        foreach(var item in nav){item.Value.Background=Ui.Color(item.Key=="form"?"#E8EFFD":"#FFFFFF");item.Value.Foreground=Ui.Color(item.Key=="form"?"#2458D3":"#52627A");}
        grid=new ExcelGrid(ExcelFields.For("form").ToArray(),rows,true,frozenColumns:3);grid.Table.RowHeight=28;foreach(var column in grid.Table.Columns.Take(3))column.CanUserReorder=false;
        sheetArea.Children.Clear();sheetArea.Children.Add(grid);RefreshAgents();ApplyAgentFilter();notification.Visibility=Visibility.Collapsed;SetSaveState(false);status.Text="All parties · select a specific party to edit invoices or use invoice actions";revision.Text="Local storage";RefreshPageHeading();
    }
    private void CreateParty()
    {
        if(dirty)Save();
        if(activePage!="parties")OpenPage("parties");
        var dialog=new PartyDialog(store){Owner=this};
        if(dialog.ShowDialog()!=true)return;
        RefreshParties(dialog.Result!.Id);OpenPage("parties");
    }
    private void CreateAgent()
    {
        if(dirty)Save();if(activePage!="agents")OpenPage("agents");
        var dialog=new AgentDialog(store){Owner=this};if(dialog.ShowDialog()==true)OpenPage("agents");
    }
    private void OpenPage(string name)
    {
        activePage=name;generatedView=true;dirty=false;grid=null;
        partyTools.Visibility=Visibility.Collapsed;workflows.Visibility=Visibility.Collapsed;options.Visibility=Visibility.Collapsed;
        var master=name is "parties" or "agents";var receipt=name is "partyReceipt" or "agentReceipt";optionBar.Visibility=master||receipt?Visibility.Visible:Visibility.Collapsed;masterActions.Visibility=master||receipt?Visibility.Visible:Visibility.Collapsed;
        masterActions.Children[0].Visibility=receipt?Visibility.Visible:Visibility.Collapsed;masterActions.Children[1].Visibility=name=="parties"?Visibility.Visible:Visibility.Collapsed;masterActions.Children[2].Visibility=name=="agents"?Visibility.Visible:Visibility.Collapsed;
        title.Text=name switch{"parties"=>"Party Master","agents"=>"Agent Master","partyReceipt"=>"Party Receipt","agentReceipt"=>"Agent Receipt",_=>"Reports"};title.ToolTip=null;
        foreach(var item in nav){item.Value.Background=Ui.Color(item.Key==name?"#E8EFFD":"#FFFFFF");item.Value.Foreground=Ui.Color(item.Key==name?"#2458D3":"#52627A");}
        sheetArea.Children.Clear();notification.Visibility=Visibility.Collapsed;
        if(master)
        {
            var rows=new System.Collections.ObjectModel.ObservableCollection<GridRow>();
            if(name=="parties")foreach(var entry in store.GetPartyMaster()){var row=new GridRow();row["A"]=entry.Name;row["B"]=entry.Invoices.ToString(CultureInfo.InvariantCulture);var amounts=new CompanyReports(store).MasterAmounts(name=="agents",entry.Id.ToString());row["E"]=amounts.OriginalOrPartyReceipts?.ToString("0.00",CultureInfo.InvariantCulture)??"";row["C"]=amounts.Received.ToString("0.00",CultureInfo.InvariantCulture);row["D"]=amounts.Pending?.ToString("0.00",CultureInfo.InvariantCulture)??"";rows.Add(row);}
            else foreach(var entry in store.GetAgents()){var row=new GridRow();row["A"]=entry.Name;row["B"]=entry.Invoices.ToString(CultureInfo.InvariantCulture);var amounts=new CompanyReports(store).MasterAmounts(name=="agents",entry.Id.ToString());row["E"]=amounts.OriginalOrPartyReceipts?.ToString("0.00",CultureInfo.InvariantCulture)??"";row["C"]=amounts.Received.ToString("0.00",CultureInfo.InvariantCulture);row["D"]=amounts.Pending?.ToString("0.00",CultureInfo.InvariantCulture)??"";rows.Add(row);}
            grid=new ExcelGrid([new("A",name=="parties"?"Party Name":"Agent Name",Width:310),new("B","Invoices",Width:90),new("E",name=="parties"?"Original invoice total / USD":"Received from parties / USD",Width:230,DataType:"number"),new("C",name=="parties"?"Paid by party to agent / USD":"Sent to company / USD",Width:230,DataType:"number"),new("D",name=="parties"?"Party still owes agent / USD":"Agent still owes company / USD",Width:230,DataType:"number")],rows,true);sheetArea.Children.Add(grid);
        }
        if(receipt)LoadTransferReceiptPage(name);
        if(name=="reports")LoadCompanyReportPage();
        SetSaveState(false);status.Text="All changes saved";revision.Text="Local storage";RefreshPageHeading();
    }
    private void AssignWorksheet()
    {
        if(controller.PartyId!=Guid.Empty||controller.Rows.Count==0)return;
        if(dirty)throw new ArgumentException("Save or reload your unassigned changes before assigning this worksheet.");
        var dialog=new PartyDialog(store,true){Owner=this};
        if(dialog.ShowDialog()!=true)return;
        if(MessageBox.Show(this,$"Move this entire worksheet and its opening balance to {dialog.Result!.Name}? Only do this if every row belongs to that party.","Assign worksheet",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        store.AssignUnassigned(controller.Name,dialog.Result.Id,controller.Revision);
        RefreshParties(dialog.Result.Id);Open(controller.Name,dialog.Result.Id);
    }
    internal void CheckInteractions()
    {
        Open("form");if(!store.GetAgents().Any(a=>a.Name=="TEST agent"))store.CreateAgent("TEST agent");NewInvoiceInput=()=>new("TEST country","TEST agent","TEST PI-"+Guid.NewGuid(),new(2026,5,1));var before=controller.Rows.Count;Add();
        var row=controller.Rows.Last();row["F1"]=controller.PartyName;row["F9"]="UI-TEST";
        if(!saveButton.IsEnabled)throw new InvalidOperationException("Editing must enable Save.");
        Save();Open("form");
        if(controller.Rows.Count!=before+1||saveButton.IsEnabled)throw new InvalidOperationException("UI save/reload failed.");
        foreach(var name in new[]{"details","ledger","form"}){Open(name);Measure(new Size(1000,600));Arrange(new Rect(0,0,1000,600));UpdateLayout();}
    }
    internal void CheckShortcutInteractions()
    {
        var party=store.CreateParty("Shortcut UI TEST");PreviewParty("form",party.Id);
        if(shortcuts.Count!=19||shortcuts.Select(s=>(s.Key,s.Modifiers)).Distinct().Count()!=shortcuts.Count)throw new InvalidOperationException("Shortcut registration is missing or duplicated.");
        if(!store.GetAgents().Any(a=>a.Name=="TEST agent"))store.CreateAgent("TEST agent");NewInvoiceInput=()=>new("TEST country","TEST agent","TEST PI-"+Guid.NewGuid(),new(2026,5,1));TryShortcut(Key.N,ModifierKeys.Control);var row=controller.Records.ToList().Last();row["F9"]="SHORTCUT-TEST";row["F10"]="2026-06-01";row["F5"]="1000";row["F6"]="800";TryShortcut(Key.S,ModifierKeys.Control);
        if(dirty||saveButton.IsEnabled||System.Windows.Automation.AutomationProperties.GetName(saveButton)!="All changes saved")throw new InvalidOperationException("Ctrl+S did not save and update button state.");
        int cellConfirmations=0;ConfirmRecordDeletion=_=>{cellConfirmations++;return false;};grid!.Table.SelectedCells.Clear();grid.Table.CurrentCell=new(row,grid.Table.Columns.Single(c=>c.SortMemberPath=="[F11]"));grid.Table.SelectedCells.Add(grid.Table.CurrentCell);row["F11"]="CLEAR ME";TryShortcut(Key.Delete,ModifierKeys.None);
        if(cellConfirmations!=0||row["F11"]!=""||controller.Records.ToList().Count!=1)throw new InvalidOperationException("Cell Delete requested row confirmation or removed a record.");
        string clipboard="";grid.ClipboardWriter=text=>clipboard=text;row["F11"]="CELL CUT";TryShortcut(Key.X,ModifierKeys.Control);if(clipboard!="CELL CUT"||row["F11"]!=""||cellConfirmations!=0)throw new InvalidOperationException($"Ctrl+X cell cut failed: clipboard={clipboard}; value={row["F11"]}; confirmations={cellConfirmations}; error={message.Text}");TryShortcut(Key.S,ModifierKeys.Control);
        int confirmations=0;ConfirmRecordDeletion=_=>{confirmations++;return false;};grid!.SelectRecordRow(row);TryShortcut(Key.Delete,ModifierKeys.None);
        if(confirmations!=1||controller.Records.ToList().Count!=1||dirty)throw new InvalidOperationException("Cancelled deletion changed records.");
        ConfirmRecordDeletion=_=>{confirmations++;return true;};TryShortcut(Key.Delete,ModifierKeys.None);
        if(confirmations!=2||controller.Records.ToList().Count!=0||dirty||saveButton.IsEnabled||store.Load("form",party.Id).Rows.Any(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"))throw new InvalidOperationException("Confirmed deletion was not saved immediately.");
        ReloadSaved();if(controller.Records.ToList().Count!=0)throw new InvalidOperationException("Reload restored a deleted record.");
        var first=controller.Add();Watch(first);first["F9"]="DELETE-ONLY";first["F10"]="2026-06-01";first["F5"]="1000";first["F6"]="800";
        var second=controller.Add();Watch(second);second["F9"]="KEEP-DRAFT";second["F10"]="2026-06-01";second["F5"]="1000";second["F6"]="800";Save();
        second["F5"]="1200";grid!.SelectRecordRow(first);TryShortcut(Key.Delete,ModifierKeys.None);
        var persisted=store.Load("form",party.Id).Rows.Where(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1").ToList();
        if(persisted.Count!=1||persisted[0]["F9"]!="KEEP-DRAFT"||persisted[0]["F5"]!="1000"||second["F5"]!="1200"||!dirty)throw new InvalidOperationException("Deleting a record saved or discarded unrelated draft edits.");
        Save();grid.SelectRecordRow(second);TryShortcut(Key.Delete,ModifierKeys.None);ReloadSaved();
        TryShortcut(Key.S,ModifierKeys.Control);if(store.Load("form",party.Id).Rows.Any(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"))throw new InvalidOperationException("Deletion was not saved.");
        var cut=controller.Add();Watch(cut);cut["F9"]="CUT-RECORD";cut["F10"]="2026-06-01";cut["F5"]="1000";cut["F6"]="800";Save();grid!.ClipboardWriter=text=>clipboard=text;grid.SelectRecordRow(cut);
        ConfirmRecordDeletion=_=>throw new InvalidOperationException("Cut requested confirmation.");TryShortcut(Key.X,ModifierKeys.Control);
        if(!clipboard.Contains("CUT-RECORD")||controller.Records.ToList().Count!=0||store.Load("form",party.Id).Rows.Any(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"))throw new InvalidOperationException("Whole-record cut was not copied and persisted.");
        TryShortcut(Key.Z,ModifierKeys.Control);
        if(controller.Records.ToList().Count!=1||!store.Load("form",party.Id).Rows.Any(r=>r.GetValueOrDefault("F9")=="CUT-RECORD"))throw new InvalidOperationException("Undo did not restore the stored cut record.");
        TryShortcut(Key.Y,ModifierKeys.Control);
        if(controller.Records.ToList().Count!=0||store.Load("form",party.Id).Rows.Any(r=>!r.ContainsKey("_metadata")&&r.GetValueOrDefault("_blank")!="1"))throw new InvalidOperationException("Redo did not remove the stored cut record.");
        grid.ClipboardWriter=null;ConfirmRecordDeletion=null;TryShortcut(Key.D2,ModifierKeys.Control);if(activePage!="agents")throw new InvalidOperationException("Ctrl+2 navigation failed.");TryShortcut(Key.D3,ModifierKeys.Control);if(activePage!="form")throw new InvalidOperationException("Ctrl+3 navigation failed.");TryShortcut(Key.D1,ModifierKeys.Control);if(activePage!="parties")throw new InvalidOperationException("Ctrl+1 navigation failed.");
        PreviewParty("form",Guid.Empty);
    }
    internal void CheckUndoRedoInteractions()
    {
        var party=store.CreateParty("Undo redo UI test");PreviewParty("form",party.Id);
        var first=controller.Add();Watch(first);first["F9"]="UNDO-ONE";first["F10"]="2026-06-01";first["F5"]="15000";first["F6"]="10000";first["F7"]="9000";Save();
        controller.Records.ToList()[0]["F5"]="16000";Calculate();TryShortcut(Key.Z,ModifierKeys.Control);
        if(controller.Records.ToList()[0]["F5"]!="15000"||controller.Records.ToList()[0]["F8"]!="6000.00"||dirty)throw new InvalidOperationException("Undo did not restore input/formula/saved state.");
        TryShortcut(Key.Y,ModifierKeys.Control);
        if(controller.Records.ToList()[0]["F5"]!="16000"||controller.Records.ToList()[0]["F8"]!="7000.00"||!dirty)throw new InvalidOperationException("Redo did not restore input/formula/draft state.");
        TryShortcut(Key.Z,ModifierKeys.Control);controller.Records.ToList()[0]["F5"]="17000";Calculate();TryShortcut(Key.Y,ModifierKeys.Control);
        if(controller.Records.ToList()[0]["F5"]!="17000")throw new InvalidOperationException("New edit failed to clear redo history.");
        Save();var row=controller.Records.ToList()[0];grid!.Table.CurrentCell=new(row,grid.Table.Columns.Single(c=>c.SortMemberPath=="[F11]"));
        grid.Paste("Pasted country\tPasted proforma\t02-06-2026");TryShortcut(Key.Z,ModifierKeys.Control);
        if(controller.Records.ToList()[0]["F11"]!=""||controller.Records.ToList()[0]["F12"]!=""||controller.Records.ToList()[0]["F13"]!="")throw new InvalidOperationException("Paste did not undo atomically.");
        TryShortcut(Key.Y,ModifierKeys.Control);
        if(controller.Records.ToList()[0]["F13"]!="2026-06-02"||controller.Records.ToList()[0]["F11"]!="Pasted country")throw new InvalidOperationException("Paste redo lost date data.");
        Save();row=controller.Records.ToList()[0];grid.Table.CurrentCell=new(row,grid.Table.Columns.Single(c=>c.SortMemberPath=="[F11]"));grid.Table.SelectedCells.Clear();grid.Table.SelectedCells.Add(grid.Table.CurrentCell);
        grid.ClipboardWriter=_=>{};TryShortcut(Key.X,ModifierKeys.Control);
        if(controller.Records.ToList()[0]["F11"]!="")throw new InvalidOperationException("Cell cut did not clear value.");
        TryShortcut(Key.Z,ModifierKeys.Control);if(controller.Records.ToList()[0]["F11"]!="Pasted country")throw new InvalidOperationException("Cell cut undo failed.");
        TryShortcut(Key.Y,ModifierKeys.Control);if(controller.Records.ToList()[0]["F11"]!="")throw new InvalidOperationException("Cell cut redo failed.");Save();
        var other=controller.Add();Watch(other);other["F9"]="UNDO-TWO";other["F10"]="2026-06-02";other["F5"]="1000";other["F6"]="800";Save();
        other["F5"]="1200";Calculate();grid.SelectRecordRow(controller.Records.ToList()[0]);ConfirmRecordDeletion=_=>true;TryShortcut(Key.Delete,ModifierKeys.None);
        TryShortcut(Key.Z,ModifierKeys.Control);
        if(controller.Records.ToList().Count!=2||controller.Records.ToList()[1]["F5"]!="1200"||store.Load("form",party.Id).Rows.Single(r=>r.GetValueOrDefault("F9")=="UNDO-TWO")["F5"]!="1000")throw new InvalidOperationException("Deletion undo saved unrelated draft edits.");
        TryShortcut(Key.Y,ModifierKeys.Control);
        if(controller.Records.ToList().Count!=1||controller.Records.ToList()[0]["F5"]!="1200")throw new InvalidOperationException("Deletion redo lost unrelated drafts.");
        ConfirmRecordDeletion=null;Save();Open("form",party.Id);TryShortcut(Key.Z,ModifierKeys.Control);
        if(controller.Records.ToList().Count!=1||controller.Records.ToList()[0]["F9"]!="UNDO-TWO")throw new InvalidOperationException("Reload did not clear worksheet history.");
        var draft=controller.Add();Watch(draft);draft["F9"]="UNDO-DRAFT";draft["F10"]="2026-06-03";draft["F5"]="1000";draft["F6"]="800";
        grid!.SelectRecordRow(draft);grid.ClipboardWriter=_=>{};ConfirmRecordDeletion=_=>throw new InvalidOperationException("Draft cut requested confirmation.");TryShortcut(Key.X,ModifierKeys.Control);
        TryShortcut(Key.Z,ModifierKeys.Control);Save();TryShortcut(Key.Y,ModifierKeys.Control);
        if(store.Load("form",party.Id).Rows.Any(r=>r.GetValueOrDefault("F9")=="UNDO-DRAFT"))throw new InvalidOperationException("Redo of a cut saved draft was not persisted.");
        TryShortcut(Key.Z,ModifierKeys.Control);
        if(!store.Load("form",party.Id).Rows.Any(r=>r.GetValueOrDefault("F9")=="UNDO-DRAFT"))throw new InvalidOperationException("Undo after saving a restored draft lost the stored record.");
        ConfirmRecordDeletion=null;
        PreviewParty("form",Guid.Empty);
    }
    internal void CheckBlankRowInteractions()
    {
        var party=store.CreateParty("Fixed worksheet positions UI test");PreviewParty("form",party.Id);
        for(var i=1;i<=4;i++){var row=controller.Add();Watch(row);row["F9"]="POSITION-"+i;row["F10"]="2026-06-01";row["F5"]="15000";row["F6"]="10000";}
        Save();var oldThird=controller.Rows[2]["_id"];var fourth=controller.Rows[3]["_id"];
        grid!.SelectRecordRow(controller.Rows[2]);ConfirmRecordDeletion=_=>true;TryShortcut(Key.Delete,ModifierKeys.None);
        CheckGap();TryShortcut(Key.Z,ModifierKeys.Control);
        if(controller.Rows[2]["F9"]!="POSITION-3"||controller.Rows[3]["_id"]!=fourth)throw new InvalidOperationException("Undo shifted worksheet positions.");
        TryShortcut(Key.Y,ModifierKeys.Control);CheckGap();Open("form",party.Id);CheckGap();
        grid!.ClipboardWriter=_=>{};ConfirmRecordDeletion=_=>throw new InvalidOperationException("Cut requested confirmation.");grid.SelectRecordRow(controller.Rows[3]);TryShortcut(Key.X,ModifierKeys.Control);
        if(controller.Rows.Count!=4||controller.Rows[3]["_blank"]!="1"||controller.Records.Count()!=2)throw new InvalidOperationException("Cut did not leave its row blank.");
        TryShortcut(Key.Z,ModifierKeys.Control);
        if(controller.Rows[3]["F9"]!="POSITION-4"||controller.Rows[2]["_blank"]!="1")throw new InvalidOperationException("Cut undo restored the wrong row.");
        var blank=controller.Rows[2];blank["F9"]="POSITION-NEW";blank["F10"]="2026-06-02";blank["F5"]="12000";blank["F6"]="8000";Save();
        if(blank["_blank"]=="1"||blank["_id"]==oldThird||blank["_id"].Length==0||controller.Rows[3]["_id"]!=fourth)throw new InvalidOperationException("Reusing blank row kept the deleted identity or shifted another record.");
        ConfirmRecordDeletion=null;PreviewParty("form",Guid.Empty);
        void CheckGap()
        {
            if(controller.Rows.Count!=4||controller.Rows[2]["_blank"]!="1"||controller.Rows[3]["_id"]!=fourth||controller.Fields.Any(f=>controller.Rows[2][f.Key]!=""))throw new InvalidOperationException("Record deletion shifted rows or left visible data behind.");
            if(workflow.BuildCurrent(party.Id).Tables[0].Rows.Count!=3)throw new InvalidOperationException("Reports counted the blank worksheet row as an invoice.");
        }
    }
    internal void CheckThemeInteractions()
    {
        var before=controller.CaptureEditState().Signature;var beforeRevision=controller.Revision;var undo=history.UndoStep;
        try
        {
            AppTheme.SetDark(false);ThemeChecks.Flush();
            var content=(FrameworkElement)Content;content.Measure(new(1100,720));content.Arrange(new(0,0,1100,720));content.UpdateLayout();
            var save=saveButton.TranslatePoint(new(0,0),content);var toggle=themeButton.TranslatePoint(new(0,0),content);
            if(themeButton.ActualWidth!=32||themeButton.ActualHeight!=32||toggle.X<save.X+saveButton.ActualWidth||Math.Abs(toggle.Y-save.Y)>1)throw new InvalidOperationException("Theme button is not a compact square after Save.");
            themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ThemeChecks.Flush();
            if(!AppTheme.IsDark||themeButton.ToolTip?.ToString()!="Switch to light theme"||((SolidColorBrush)grid!.Table.Background).Color!=System.Windows.Media.Color.FromRgb(23,34,52))throw new InvalidOperationException("Dark theme did not update the existing worksheet.");
            if(((SolidColorBrush)grid.Table.Foreground).Color!=System.Windows.Media.Color.FromRgb(229,236,246))throw new InvalidOperationException("Dark grid text is unreadable.");
            themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ThemeChecks.Flush();
            if(AppTheme.IsDark||((SolidColorBrush)grid.Table.Background).Color!=Colors.White)throw new InvalidOperationException("Second click did not restore light theme.");
            if(before!=controller.CaptureEditState().Signature||beforeRevision!=controller.Revision||history.UndoStep!=undo)throw new InvalidOperationException("Theme toggle changed worksheet data/history.");
        }
        finally{AppTheme.SetDark(false);}
    }
    internal string SelectedPartyName=>controller.PartyName;
    internal double SheetTop=>grid?.TranslatePoint(new Point(0,0),(UIElement)Content).Y??999;
    internal void CheckPartyInteractions()
    {
        var a=store.CreateParty("UI Party A");var b=store.CreateParty("UI Party B");
        var legacy=new WorksheetController(store);legacy.Load("ledger",a.Id);legacy.Opening=100;
        var old=legacy.Add();old["B"]="Preserved legacy row";old["C"]="50";legacy.Save();
        RefreshParties(a.Id);Open("ledger",a.Id);
        if(!generatedView||grid!.Table.Items.Count!=0)throw new InvalidOperationException("New party did not open generated reports without setup.");
        if(store.Load("ledger",a.Id).Rows.Count!=2)throw new InvalidOperationException("Automatic reports erased legacy data.");
        Open("form",a.Id);var row=controller.Add();
        if(row["F1"]!=a.Name)throw new InvalidOperationException("Selected party was not defaulted.");
        row["F9"]="UI-PARTY-A";row["F10"]="2026-06-01";row["F5"]="100";row["F6"]="70";terms.Text="60";Save();
        Open("details",a.Id);if(!generatedView||grid!.Table.Items.Count!=1)throw new InvalidOperationException("Automatic report did not use saved invoice.");
        RefreshParties(b.Id);Open("details",b.Id);if(grid!.Table.Items.Count!=0)throw new InvalidOperationException("Party B inherited Party A invoices.");
        RefreshParties(b.Id);partySelector.SelectedItem=partySelector.Items.Cast<WorksheetParty>().Single(p=>p.Id==a.Id);
        if(controller.PartyId!=a.Id||grid!.Table.Items.Count!=1)throw new InvalidOperationException("Party switching failed to restore isolated reports.");
        Open("form",b.Id);if(controller.Rows.Count!=0)throw new InvalidOperationException("Invoice register leaked across parties.");
        if(terms.Text!="90")throw new InvalidOperationException("Party terms leaked.");
        var reopenedController=new WorksheetController(store);reopenedController.Load("form",a.Id);
        if(reopenedController.Terms!=60||reopenedController.AsOf!=DateOnly.FromDateTime(DateTime.Today))throw new InvalidOperationException("Party settings were not persisted.");
        RefreshParties(Guid.Empty);Open("form",Guid.Empty);
    }
    private sealed class ActionCommand(Action execute):ICommand{public event EventHandler? CanExecuteChanged{add{}remove{}}public bool CanExecute(object? parameter)=>true;public void Execute(object? parameter)=>execute();}
}


