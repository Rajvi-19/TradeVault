using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TradeVault.Desktop;

internal static class GridInteractionChecks
{
    internal static void CopyDates()
    {
        var fields=ExcelFields.For("form").Where(f=>f.Key!="F1").ToArray();
        var original=new GridRow();var target=new GridRow();
        foreach(var field in fields.Where(f=>f.IsDate&&!f.Calculated))original[field.Key]="2026-10-29";
        original["F9"]="COPY-DATES";original["F19"]="2027-01-27";
        var grid=new ExcelGrid(fields,new ObservableCollection<GridRow>{original,target});
        Layout(grid);
        // This is the same column-level binding path used by WPF's built-in Copy command.
        var clipboard=string.Join('\t',grid.Table.Columns.Select(c=>Convert.ToString(c.OnCopyingCellClipboardContent(original),CultureInfo.InvariantCulture)??""));
        grid.Table.CurrentCell=new(target,grid.Table.Columns[0]);
        if(!grid.Paste(clipboard))throw new InvalidOperationException("Copied record could not be pasted.");
        foreach(var field in fields.Where(f=>f.IsDate&&!f.Calculated))
            if(target[field.Key]!=original[field.Key])throw new InvalidOperationException("Copy lost date: "+field.Key);
        if(target["F19"]!="")throw new InvalidOperationException("Paste overwrote calculated due date.");
    }
    internal static void MultipleRows()
    {
        var rows=new ObservableCollection<GridRow>{new(),new(),new()};
        var grid=new ExcelGrid([new("A","First"),new("B","Locked",true),new("C","Last")],rows);
        grid.SelectRecordRow(rows[0]);grid.SelectRecordRow(rows[2],true);
        if(grid.Table.SelectedCells.Count!=6||grid.WholeSelectedRecord!=null)throw new InvalidOperationException("Ctrl row selection did not preserve both records.");
        grid.SelectRecordRow(rows[0],true);
        if(grid.Table.SelectedCells.Count!=3||grid.WholeSelectedRecord!=rows[2])throw new InvalidOperationException("Ctrl row toggle removed the wrong cells.");
        grid.SelectRecordRow(rows[1]);
        if(grid.Table.SelectedCells.Count!=3||grid.WholeSelectedRecord!=rows[1])throw new InvalidOperationException("Plain row click did not replace selection.");
    }
    internal static void HorizontalScroll()
    {
        var grid=new ExcelGrid(ExcelFields.For("form"),new ObservableCollection<GridRow>{new()});Layout(grid);
        var scroll=Find<ScrollViewer>(grid.Table)??throw new InvalidOperationException("Worksheet scroller missing.");
        if(!grid.ScrollHorizontally(120))throw new InvalidOperationException("Horizontal wheel ignored a wide sheet.");
        Layout(grid);if(scroll.HorizontalOffset<=0)throw new InvalidOperationException("Horizontal wheel did not scroll right.");
        grid.ScrollHorizontally(-120);Layout(grid);
        if(scroll.HorizontalOffset!=0)throw new InvalidOperationException("Horizontal wheel did not scroll left.");
    }
    internal static void NativeHorizontalScroll()
    {
        var grid=new ExcelGrid(ExcelFields.For("form"),new ObservableCollection<GridRow>{new()});
        var window=new Window{Content=grid,Width=900,Height=500,Left=-20000,Top=-20000,ShowInTaskbar=false,ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual};
        try
        {
            window.Show();Layout(grid);
            var hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;
            var point=grid.Table.PointToScreen(new(200,100));
            var position=new IntPtr((ushort)(short)point.X|((long)(ushort)(short)point.Y<<16));
            var scroll=Find<ScrollViewer>(grid.Table)??throw new InvalidOperationException("Worksheet scroller missing.");
            foreach(var message in new[]{0x020E,0x024F,0x020A})
            {
                scroll.ScrollToHorizontalOffset(0);Layout(grid);
                var delta=message==0x020A?-120:120;var flags=message==0x020A?4:0;
                SendMessage(hwnd,message,new IntPtr(((long)(ushort)(short)delta<<16)|(long)(ushort)flags),position);Layout(grid);
                if(scroll.HorizontalOffset<=0)throw new InvalidOperationException($"Native horizontal input 0x{message:X} did not reach the sheet.");
            }
            scroll.ScrollToHorizontalOffset(0);Layout(grid);SendMessage(hwnd,0x0114,new IntPtr(1),IntPtr.Zero);Layout(grid);
            if(scroll.HorizontalOffset<=0)throw new InvalidOperationException("Legacy touchpad line-right scroll failed.");
            SendMessage(hwnd,0x0114,new IntPtr(0),IntPtr.Zero);Layout(grid);
            if(scroll.HorizontalOffset!=0)throw new InvalidOperationException("Legacy touchpad line-left scroll failed.");
        }
        finally{window.Close();}
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);
    internal static void RenumberAfterDelete()
    {
        var rows=new ObservableCollection<GridRow>(Enumerable.Range(1,4).Select(i=>{var row=new GridRow();row["A"]="RECORD-"+i;return row;}));
        var fourth=rows[3];var grid=new ExcelGrid([new("A","Record")],rows,promote:_=>{});Layout(grid);
        var blank=new GridRow();blank["_blank"]="1";rows[2]=blank;Layout(grid);
        if(grid.Table.Items[3]!=fourth||grid.Table.Items[2]!=blank)throw new InvalidOperationException("Clearing row 3 shifted record 4.");
        for(var i=0;i<rows.Count;i++)
            if(grid.Table.ItemContainerGenerator.ContainerFromIndex(i) is not DataGridRow row||row.Header?.ToString()!=(i+1).ToString())throw new InvalidOperationException("Worksheet position headers changed.");
        ((System.Windows.Data.ListCollectionView)grid.Table.ItemsSource).CustomSort=new ReverseRecordOrder();Layout(grid);
        var order=grid.Table.Items.Cast<GridRow>().ToList();var position=order.IndexOf(rows[1]);var sortedBlank=new GridRow();sortedBlank["_blank"]="1";
        rows[1]=sortedBlank;order[position]=sortedBlank;Layout(grid);
        if(!grid.Table.Items.Cast<GridRow>().SequenceEqual(order))throw new InvalidOperationException("Clearing a sorted row shifted other visible records.");
    }
    private sealed class ReverseRecordOrder:System.Collections.IComparer
    {
        public int Compare(object? x,object? y)=>string.Compare((y as GridRow)?["A"],(x as GridRow)?["A"],StringComparison.Ordinal);
    }
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new(900,500));view.Arrange(new(0,0,900,500));view.UpdateLayout();
        view.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);view.UpdateLayout();
    }
    private static T? Find<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T item)return item;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(Find<T>(VisualTreeHelper.GetChild(root,i)) is {} found)return found;
        return null;
    }
}

