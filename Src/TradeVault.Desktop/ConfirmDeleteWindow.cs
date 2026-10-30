using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace TradeVault.Desktop;

internal sealed class ConfirmDeleteWindow:Window
{
    internal ConfirmDeleteWindow(Window owner,string reference,string party)
    {
        if(new System.Windows.Interop.WindowInteropHelper(owner).Handle!=IntPtr.Zero)Owner=owner;
        Width=Math.Min(560,SystemParameters.WorkArea.Width);SizeToContent=SizeToContent.Height;MaxHeight=SystemParameters.WorkArea.Height;MinWidth=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;
        var root=new DockPanel{Margin=new(18)};
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new(0,12,0,0)};
        var keep=Ui.Button("Keep record",()=>DialogResult=false,"PrimaryButton");keep.IsDefault=true;keep.IsCancel=true;keep.Margin=new(0,0,10,0);keep.Padding=new(16,8,16,8);actions.Children.Add(keep);
        var remove=Ui.Button("Delete record",()=>DialogResult=true);remove.Background=AppTheme.DangerFill;remove.BorderBrush=AppTheme.DangerFill;remove.Foreground=Brushes.White;remove.Padding=new(16,8,16,8);actions.Children.Add(remove);DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var content=new StackPanel();var heading=new DockPanel{Margin=new(0,0,0,12)};var icon=new Border{Background=Ui.Color("#FEECE9"),CornerRadius=new(8),Padding=new(12),Margin=new(0,0,12,0),Child=Ui.Icon("\uE74D","#B42318",22)};DockPanel.SetDock(icon,Dock.Left);heading.Children.Add(icon);
        var title=new StackPanel();title.Children.Add(Ui.Text("Delete this record?",21,"#18243B",true));title.Children.Add(Ui.Text("Please confirm the selected entry below.",12,"#64748B"));heading.Children.Add(title);content.Children.Add(heading);
        var details=new StackPanel();details.Children.Add(Ui.Text("RECORD",10,"#64748B",true));details.Children.Add(Ui.Text(reference,15,"#18243B",true));var partyText=Ui.Text("Party: "+party,12,"#52627A");partyText.Margin=new(0,8,0,0);details.Children.Add(partyText);content.Children.Add(Ui.Card(details,new Thickness(12)));
        var note=Ui.Text("This record will be cleared and saved immediately. Its row stays blank; lower records stay in place. Use Ctrl+Z to undo it in this worksheet. Other unsaved edits stay unsaved.",12,"#64748B");note.Margin=new(0,10,0,0);content.Children.Add(note);
        root.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Ui.WindowFrame(this,root,"Confirm deletion");ShowInTaskbar=false;Loaded+=(_,_)=>keep.Focus();
    }
    internal static bool Confirm(Window owner,string reference,string party)=>new ConfirmDeleteWindow(owner,reference,party).ShowDialog()==true;
}
