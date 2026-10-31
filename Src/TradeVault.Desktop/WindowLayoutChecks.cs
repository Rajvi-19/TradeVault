using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace TradeVault.Desktop;

public sealed partial class MainWindow
{
    internal void CheckWindowViewport()
    {
        static IEnumerable<T> Descendants<T>(DependencyObject node)where T:DependencyObject
        {if(node is T found)yield return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Descendants<T>(VisualTreeHelper.GetChild(node,i)))yield return child;}
        void Flush(){UpdateLayout();Dispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);UpdateLayout();}
        void CheckFooter()
        {
            Flush();var work=WindowBounds.WorkArea(new WindowInteropHelper(this).Handle);var bottom=footer.PointToScreen(new Point(0,footer.ActualHeight));
            if(footer.ActualHeight<20||bottom.Y>work.Bottom+1)throw new InvalidOperationException("Footer extends below the monitor work area.");
            if(grid==null)return;
            var bar=Descendants<ScrollBar>(grid.Table).FirstOrDefault(scroll=>scroll.Orientation==Orientation.Horizontal&&scroll.IsVisible);
            if(bar==null||bar.PointToScreen(new Point(0,bar.ActualHeight)).Y>work.Bottom+1)throw new InvalidOperationException("Worksheet horizontal scrollbar is clipped or unavailable.");
        }
        PreviewSheet("form");WindowStartupLocation=WindowStartupLocation.Manual;Left=20;Top=20;ShowActivated=false;ShowInTaskbar=false;Width=Math.Min(1100,SystemParameters.WorkArea.Width-40);Height=Math.Min(720,SystemParameters.WorkArea.Height-40);
        Show();CheckFooter();WindowState=WindowState.Maximized;CheckFooter();WindowState=WindowState.Normal;CheckFooter();Hide();
    }
}
