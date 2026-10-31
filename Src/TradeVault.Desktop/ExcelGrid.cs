using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TradeVault.Desktop;

public sealed class ExcelGrid : UserControl
{
    public DataGrid Table { get; } = new();
    private readonly TextBlock recordCount=Ui.Text("0 records",11,"#64748B");
    private readonly ObservableCollection<GridRow> rows;
    private readonly IReadOnlyList<Field> fields;
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new(8,3,8,3), FontSize=11, Foreground=Ui.Color("#B42318"),Visibility=Visibility.Collapsed };
    private string Feedback{set{feedback.Text=value;feedback.Visibility=string.IsNullOrWhiteSpace(value)?Visibility.Collapsed:Visibility.Visible;}}
    private readonly CollectionViewSource source;
    private Func<GridRow,bool>? rowFilter;
    private readonly ObservableCollection<GridRow> displayRows;
    private readonly HashSet<GridRow> blanks=[];
    private readonly Action<GridRow>? promote;
    private System.Windows.Interop.HwndSource? wheelSource;
    private bool numberingPending;
    private Point? panPosition;
    public event Action? Edited;
    internal event Action? BeforeChange;
    private bool clipboardPaste;
    internal Action<string>? ClipboardWriter{get;set;}
    internal Func<GridRow,bool>? CanEditRecord{get;set;}
    private const string RecordFormat="TradeVault.Record.v1";
    private sealed record RecordClipboard(string[] Fields,string Terms);
    internal GridRow? WholeSelectedRecord
    {
        get{var cells=Table.SelectedCells;var selected=cells.Select(c=>c.Item).Distinct().ToArray();return selected.Length==1&&selected[0] is GridRow row&&rows.Contains(row)&&row["_blank"]!="1"&&Table.Columns.Where(c=>c.Visibility==Visibility.Visible).All(c=>cells.Any(cell=>ReferenceEquals(cell.Item,row)&&cell.Column==c))?row:null;}
    }
    public ExcelGrid(IReadOnlyList<Field> fields, ObservableCollection<GridRow> rows, bool readOnly = false,Action<GridRow>? promote=null,int frozenColumns=2)
    {
        this.fields = fields; this.rows = rows;this.promote=promote;
        displayRows=new(rows);
        if(promote!=null&&!readOnly)
        {
            AddBlankRows();
        }
        System.Collections.Specialized.CollectionChangedEventManager.AddHandler(rows,OnRowsChanged);
        var root = new DockPanel();
        var search = new TextBox { Width=200,MinHeight=28, Padding = new(8,3,8,3), ToolTip = "Filter visible rows across all columns" };
        System.Windows.Automation.AutomationProperties.SetName(search, "Filter rows");
        var tools=new DockPanel {Margin=new(8,4,8,4)};
        recordCount.Margin=new(12,0,0,0);DockPanel.SetDock(recordCount,Dock.Right);tools.Children.Add(recordCount);
        if(readOnly){var legend=new TextBlock {Text="Read-only worksheet",FontSize=11,Foreground=Ui.Color("#64748B"),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(legend,Dock.Right);tools.Children.Add(legend);}
        var filter=new StackPanel {Orientation=Orientation.Horizontal};
        filter.Children.Add(new TextBlock {Text="Search records",VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,10,0),FontSize=12,Foreground=Ui.Color("#64748B")});filter.Children.Add(search);tools.Children.Add(filter);
        DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        DockPanel.SetDock(feedback,Dock.Top); root.Children.Add(feedback);
        source = new CollectionViewSource { Source = displayRows };
        source.Filter += (_,e) => e.Accepted = e.Item is GridRow r && (rowFilter?.Invoke(r)??true) && fields.Any(f => r[f.Key].Contains(search.Text,StringComparison.OrdinalIgnoreCase));
        search.TextChanged += (_,_) => { Table.CommitEdit(); Table.CommitEdit(DataGridEditingUnit.Row,true); source.View.Refresh();RefreshRecordCount(); };
        Table.FontFamily=new("Segoe UI Variable Text, Segoe UI");
        Table.AutoGenerateColumns = false; Table.CanUserAddRows = false; Table.CanUserDeleteRows = false;
        Table.IsReadOnly = readOnly; Table.SelectionUnit = DataGridSelectionUnit.Cell;
        Table.SelectionMode = DataGridSelectionMode.Extended;
        Table.HeadersVisibility = DataGridHeadersVisibility.All; Table.RowHeaderWidth = 42;
        Table.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
        Table.LoadingRow += (_,e) => e.Row.Header = (e.Row.GetIndex()+1).ToString();
        ((System.Collections.Specialized.INotifyCollectionChanged)Table.Items).CollectionChanged+=(_,_)=>QueueRowNumbers();
        Table.ItemContainerGenerator.StatusChanged+=(_,_)=>QueueRowNumbers();
        Table.PreviewMouseLeftButtonDown+=(_,e)=>{
            if(e.OriginalSource is not DependencyObject source)return;
            var header=Ancestor<System.Windows.Controls.Primitives.DataGridRowHeader>(source);
            if(header!=null&&Ancestor<DataGridRow>(header)?.Item is GridRow row){e.Handled=true;SelectRecordRow(row,(Keyboard.Modifiers&ModifierKeys.Control)!=0);}
        };
        ScrollViewer.SetPanningMode(Table,PanningMode.Both);
        Loaded+=(_,_)=>{wheelSource?.RemoveHook(HorizontalWheel);wheelSource=PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;wheelSource?.AddHook(HorizontalWheel);QueueRowNumbers();};
        Unloaded+=(_,_)=>{wheelSource?.RemoveHook(HorizontalWheel);wheelSource=null;panPosition=null;};
        Table.PreviewMouseWheel+=(_,e)=>{if((Keyboard.Modifiers&ModifierKeys.Shift)!=0){e.Handled=ScrollHorizontally(-e.Delta);}};
        foreach (var field in fields)
        {
            var cellStyle=new Style(typeof(DataGridCell),(Style)FindResource(typeof(DataGridCell)));
            var fieldError=new Binding(nameof(GridRow.Errors)){Converter=new FieldErrorConverter(),ConverterParameter=field.Key};
            cellStyle.Setters.Add(new Setter(ToolTipProperty,fieldError));
            var invalid=new DataTrigger{Binding=new Binding(nameof(GridRow.Errors)){Converter=new FieldErrorConverter(),ConverterParameter=field.Key+"?"},Value=true};
            invalid.Setters.Add(new Setter(Border.BorderBrushProperty,Ui.Color("#C2413C")));cellStyle.Triggers.Add(invalid);
            Style? lockedHeader=null;
            if(field.Locked)
            {
                cellStyle.Setters.Add(new Setter(BackgroundProperty,Ui.Color("#EDF1F6")));cellStyle.Setters.Add(new Setter(ForegroundProperty,Ui.Color("#52627A")));
                var selected=new Trigger{Property=DataGridCell.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(BackgroundProperty,Ui.Color("#E7EFFF")));selected.Setters.Add(new Setter(ForegroundProperty,Ui.Color("#18243B")));cellStyle.Triggers.Add(selected);
                lockedHeader=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader),(Style)FindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)));
                lockedHeader.Setters.Add(new Setter(BackgroundProperty,Ui.Color("#E8EFFD")));
            }
            if(field.IsDate&&!field.IsReadOnly&&!readOnly)
            {
                var factory=new FrameworkElementFactory(typeof(DateCellEditor));
                factory.SetBinding(DateCellEditor.ValueProperty,new Binding($"[{field.Key}]"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnNotifyDataErrors=false});
                var template=new DataTemplate{VisualTree=factory};
                Table.Columns.Add(new DataGridTemplateColumn{Header=field.Label,Width=CompactWidth(field),CellTemplate=template,CellEditingTemplate=template,ClipboardContentBinding=new Binding($"[{field.Key}]"){Mode=BindingMode.OneWay,Converter=new DateCellConverter()},SortMemberPath=$"[{field.Key}]",CellStyle=cellStyle});
                continue;
            }
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.PaddingProperty,new Thickness(8,3,8,3)));
            if (field.Calculated){style.Setters.Add(new Setter(TextBlock.BackgroundProperty,Ui.Color("#F0F5FF")));var selected=new DataTrigger{Binding=new Binding("IsSelected"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(DataGridCell),1)},Value=true};selected.Setters.Add(new Setter(TextBlock.BackgroundProperty,Brushes.Transparent));style.Triggers.Add(selected);}
            Table.Columns.Add(new DataGridTextColumn {
                Header = field.Label, Width = field.Width==240?CompactWidth(field):field.Width, IsReadOnly = field.IsReadOnly,
                SortMemberPath = $"[{field.Key}]", ElementStyle = style, CellStyle=cellStyle, HeaderStyle=lockedHeader??(Style)FindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)),
                Binding = new Binding($"[{field.Key}]") { Mode = field.IsReadOnly || readOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                    UpdateSourceTrigger = field.IsDate?UpdateSourceTrigger.LostFocus:UpdateSourceTrigger.PropertyChanged,
                    Converter=field.IsDate?new DateCellConverter():null, ValidatesOnNotifyDataErrors = false },
                EditingElementStyle=EditingStyle()
            });
        }
        Table.FrozenColumnCount = Math.Min(frozenColumns,fields.Count);
        Table.ItemsSource = source.View;
        Table.Sorting+=(_,e)=> {
            e.Handled=true;var direction=e.Column.SortDirection==System.ComponentModel.ListSortDirection.Ascending?System.ComponentModel.ListSortDirection.Descending:System.ComponentModel.ListSortDirection.Ascending;
            foreach(var column in Table.Columns)column.SortDirection=null;e.Column.SortDirection=direction;
            var key=fields[Table.Columns.IndexOf(e.Column)].Key;
            ((ListCollectionView)source.View).CustomSort=new BlankLastComparer(blanks,fields.First(f=>f.Key==key),direction==System.ComponentModel.ListSortDirection.Ascending);
        };
        Table.PreviewKeyDown += (_,e) => {
            if(e.Key==Key.Tab&&!readOnly&&(Keyboard.Modifiers&ModifierKeys.Control)==0){e.Handled=MoveByTab((Keyboard.Modifiers&ModifierKeys.Shift)!=0);return;}
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && !readOnly)
            {
                e.Handled = true;
                try {
                    PasteClipboard();
                } catch (System.Runtime.InteropServices.ExternalException) { Feedback = "Clipboard is busy. Retry paste."; }
            }
        };
        Table.CellEditEnding += (_,e) => {
            if(e.EditAction==DataGridEditAction.Commit&&FindInput(e.EditingElement) is DateCellEditor date&&!date.TryCommitDate()){e.Cancel=true;Feedback="Invalid date. Enter a real calendar date or clear the cell.";Dispatcher.BeginInvoke(new Action(()=>{date.Focus();date.SelectAll();}));return;}
            Dispatcher.BeginInvoke(new Action(() => Edited?.Invoke()));
        };
        root.Children.Add(Table); Content = root;
        RefreshRecordCount();
    }
    internal void RefreshRecordCount(){var known=rows.Where(r=>r["_blank"]!="1").ToHashSet();var total=known.Count;var visible=Table.Items.Cast<GridRow>().Count(known.Contains);recordCount.Text=visible==total?$"{total} record{(total==1?"":"s")}":$"{visible} of {total} records";}
    internal void SetRowFilter(Func<GridRow,bool>? filter){rowFilter=filter;source.View.Refresh();RefreshRecordCount();}
    internal TradeVault.Application.ReportTable PageSnapshot(string name)
    {
        var ordered=Table.Columns.Where(column=>column.Visibility==Visibility.Visible).OrderBy(column=>column.DisplayIndex).Select(column=>fields[Table.Columns.IndexOf(column)]).ToList();
        var columns=ordered.Select(field=>new TradeVault.Application.ReportColumn(field.Key,field.Label,field.DataType??(field.IsDate?"date":field.Key=="F21"||field.Label=="Invoices"?"integer":field.IsNumeric?"number":"text"))).ToList();
        var records=Table.Items.Cast<GridRow>().Where(row=>row["_blank"]!="1").Select(row=>ordered.ToDictionary(field=>field.Key,field=>row[field.Key])).ToList();
        return new(name,columns,records,new());
    }
    private static TextBox? FindInput(DependencyObject root)
    {
        if(root is TextBox input)return input;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(FindInput(VisualTreeHelper.GetChild(root,i)) is {} found)return found;
        return null;
    }
    private static T? Ancestor<T>(DependencyObject? source)where T:DependencyObject
    {
        while(source!=null){if(source is T result)return result;source=source is Visual||source is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}return null;
    }
    private IntPtr HorizontalWheel(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(!Table.IsVisible)return IntPtr.Zero;
        if(message is 0x020E or 0x024F||message==0x020A&&(wParam.ToInt64()&4)!=0)
        {
            var packed=lParam.ToInt64();var position=new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff));
            if(ContainsScreenPoint(position)||CursorOverSheet()){var delta=(short)((wParam.ToInt64()>>16)&0xffff);handled=ScrollHorizontally(message==0x020A?-delta:delta);}
        }
        else if(message==0x0114&&(lParam==IntPtr.Zero||CursorOverSheet()||Table.IsKeyboardFocusWithin)&&Descendant<ScrollViewer>(Table) is {} scroll&&scroll.ScrollableWidth>0)
        {
            var request=(int)(wParam.ToInt64()&0xffff);var position=(int)((wParam.ToInt64()>>16)&0xffff);
            double? offset=request switch{0=>scroll.HorizontalOffset-48,1=>scroll.HorizontalOffset+48,2=>scroll.HorizontalOffset-scroll.ViewportWidth,3=>scroll.HorizontalOffset+scroll.ViewportWidth,4 or 5=>position,6=>0,7=>scroll.ScrollableWidth,_=>null};
            if(offset is {} next){scroll.ScrollToHorizontalOffset(next);handled=true;}
        }
        else if(message==0x0119)
        {
            var gesture=new GestureInfo{Size=(uint)System.Runtime.InteropServices.Marshal.SizeOf<GestureInfo>()};
            if(GetGestureInfo(lParam,ref gesture)&&gesture.Id==4)
            {
                var current=new Point(gesture.Location.X,gesture.Location.Y);
                if((gesture.Flags&1)!=0){panPosition=ContainsScreenPoint(current)?current:null;}
                else if(panPosition is {} previous)
                {
                    panPosition=current;var dx=previous.X-current.X;var dy=previous.Y-current.Y;
                    if(Math.Abs(dx)>Math.Abs(dy)&&Math.Abs(dx)>0){var scale=VisualTreeHelper.GetDpi(Table).DpiScaleX;handled=ScrollPixels(dx/scale);}
                }
                if((gesture.Flags&4)!=0)panPosition=null;
                if(handled)CloseGestureInfoHandle(lParam);
            }
        }
        return IntPtr.Zero;
    }
    private bool ContainsScreenPoint(Point screen)
    {
        if(PresentationSource.FromVisual(Table)==null)return false;
        return new Rect(new Point(),Table.RenderSize).Contains(Table.PointFromScreen(screen));
    }
    private bool CursorOverSheet()=>GetCursorPos(out var cursor)&&ContainsScreenPoint(new(cursor.X,cursor.Y));
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint{public int X,Y;}
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ShortPoint{public short X,Y;}
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct GestureInfo{public uint Size,Flags,Id;public IntPtr Target;public ShortPoint Location;public uint Instance,Sequence;public ulong Arguments;public uint ExtraSize;}
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetGestureInfo(IntPtr handle,ref GestureInfo info);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseGestureInfoHandle(IntPtr handle);
    private void QueueRowNumbers()
    {
        if(numberingPending)return;numberingPending=true;
        Dispatcher.BeginInvoke(new Action(()=>{numberingPending=false;RenumberRows(Table);}),System.Windows.Threading.DispatcherPriority.Loaded);
    }
    private static void RenumberRows(DependencyObject root)
    {
        if(root is DataGridRow row){var number=(row.GetIndex()+1).ToString();if(!Equals(row.Header,number))row.Header=number;return;}
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)RenumberRows(VisualTreeHelper.GetChild(root,i));
    }
    private static T? Descendant<T>(DependencyObject root)where T:DependencyObject
    {
        if(root is T result)return result;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(Descendant<T>(VisualTreeHelper.GetChild(root,i)) is {} found)return found;
        return null;
    }
    internal bool ScrollHorizontally(int delta)
    {
        return ScrollPixels(delta*0.4);
    }
    private bool ScrollPixels(double pixels)
    {
        if(Descendant<ScrollViewer>(Table) is not {} scroll||scroll.ScrollableWidth<=0||pixels==0)return false;
        scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset+pixels);return true;
    }
    internal bool SelectRecordRow(GridRow row,bool toggle=false)
    {
        if(!Table.Items.Contains(row))return false;
        if(!Table.CommitEdit()||!Table.CommitEdit(DataGridEditingUnit.Row,true))return false;
        var columns=Table.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToArray();if(columns.Length==0)return false;
        var selected=toggle?Table.SelectedCells.ToList():[];
        var remove=toggle&&columns.All(c=>selected.Any(cell=>ReferenceEquals(cell.Item,row)&&cell.Column==c));
        selected.RemoveAll(cell=>ReferenceEquals(cell.Item,row));
        if(!remove)foreach(var column in columns)selected.Add(new(row,column));
        Table.CurrentCell=new(row,columns[0]);Table.SelectedCells.Clear();foreach(var cell in selected)Table.SelectedCells.Add(cell);Table.ScrollIntoView(row);Table.Focus();return true;
    }
    internal bool MoveByTab(bool backwards)
    {
        if(Table.IsReadOnly||Table.CurrentItem is not GridRow row||Table.CurrentColumn is not {} column)return false;
        var field=fields[Table.Columns.IndexOf(column)];var content=column.GetCellContent(row);var input=content==null?null:FindInput(content);
        if(field.IsDate&&(!DateCellEditor.IsValidDate(row[field.Key])||(input is DateCellEditor date&&!date.TryCommitDate())))
        {Feedback="Invalid date. Enter a real calendar date (for example 28-10-2026) or clear the cell.";input?.Focus();input?.SelectAll();Edited?.Invoke();return true;}
        if(!Table.CommitEdit()||!Table.CommitEdit(DataGridEditingUnit.Row,true))return true;
        var editable=Table.Columns.Where(c=>!c.IsReadOnly).OrderBy(c=>c.DisplayIndex).ToArray();if(editable.Length==0)return false;
        var target=backwards?editable.LastOrDefault(c=>c.DisplayIndex<column.DisplayIndex):editable.FirstOrDefault(c=>c.DisplayIndex>column.DisplayIndex);
        int index=Table.Items.IndexOf(row);
        if(target==null){index+=backwards?-1:1;if(index<0||index>=Table.Items.Count)return true;target=backwards?editable[^1]:editable[0];}
        var next=Table.Items[index];Table.ScrollIntoView(next,target);Table.CurrentCell=new(next,target);Table.SelectedCells.Clear();Table.SelectedCells.Add(Table.CurrentCell);Table.UpdateLayout();
        DependencyObject? focus=target.GetCellContent(next);while(focus!=null&&focus is not DataGridCell)focus=VisualTreeHelper.GetParent(focus);if(focus is DataGridCell cell)cell.Focus();else Table.Focus();Table.BeginEdit();
        Dispatcher.BeginInvoke(new Action(()=>{if(Table.CurrentColumn!=target||Table.CurrentItem!=next)return;Table.UpdateLayout();var element=target.GetCellContent(next);if(element!=null&&FindInput(element) is {} editor){editor.Focus();Keyboard.Focus(editor);editor.SelectAll();}}),System.Windows.Threading.DispatcherPriority.Input);
        Feedback="";Edited?.Invoke();return true;
    }
    private static int CompactWidth(Field field)=>field.Label.Contains("COUNTRY")?125:field.Label.Contains("DATE")?175:field.Label.Length>36?220:field.Label.Length>24?195:165;
    internal void FilterForCheck(string text)
    {
        if(FindTextBox(this,"Filter rows") is {} input)input.Text=text;
    }
    internal void FocusSearch(){if(FindTextBox(this,"Filter rows") is {} input){input.Focus();Keyboard.Focus(input);input.SelectAll();}}
    private List<DataGridCellInfo> EditableSelection()
    {
        if(Table.IsReadOnly)throw new ArgumentException("This report is read-only. Edit the invoice register instead.");
        var cells=Table.SelectedCells.ToList();if(cells.Count==0&&Table.CurrentCell.IsValid)cells.Add(Table.CurrentCell);
        if(cells.Count==0)throw new ArgumentException("Select an editable cell first.");
        if(cells.Any(c=>c.Column.IsReadOnly))throw new ArgumentException("Invoice identity and calculated cells are locked. Select only editable cells.");
        return cells;
    }
    internal void ClearSelectedCells()
    {
        var cells=EditableSelection();BeforeChange?.Invoke();Table.CancelEdit();Table.CancelEdit(DataGridEditingUnit.Row);
        foreach(var cell in cells)if(cell.Item is GridRow row)row[fields[Table.Columns.IndexOf(cell.Column)].Key]="";
        Feedback="";Edited?.Invoke();
    }
    private static string Tsv(IEnumerable<IEnumerable<string>> rows)=>string.Join("\n",rows.Select(row=>string.Join("\t",row.Select(value=>value.Contains('\t')||value.Contains('\n')||value.Contains('\r')?throw new ArgumentException("Cut ranges cannot contain embedded tabs or line breaks."):value))));
    private void WriteClipboard(string text,RecordClipboard? record=null)
    {
        if(ClipboardWriter!=null){ClipboardWriter(text);return;}
        var data=new DataObject();data.SetText(text,TextDataFormat.UnicodeText);if(record!=null)data.SetData(RecordFormat,System.Text.Json.JsonSerializer.Serialize(record));Clipboard.SetDataObject(data,true);
    }
    internal void CopyRecordForCut(GridRow row)
    {
        var ordered=Table.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).Select(c=>fields[Table.Columns.IndexOf(c)]).ToArray();
        WriteClipboard(Tsv(new[]{ordered.Select(f=>row[f.Key])}),new(ordered.Select(f=>f.Key).ToArray(),row["_terms"]));
    }
    internal void CutSelectedCells()
    {
        var cells=EditableSelection();var items=Table.Items.Cast<GridRow>().Where(r=>cells.Any(c=>ReferenceEquals(c.Item,r))).ToArray();var columns=cells.Select(c=>c.Column).Distinct().OrderBy(c=>c.DisplayIndex).ToArray();
        if(cells.Count!=items.Length*columns.Length||Table.Items.IndexOf(items[^1])-Table.Items.IndexOf(items[0])+1!=items.Length||columns[^1].DisplayIndex-columns[0].DisplayIndex+1!=columns.Length||items.Any(r=>columns.Any(c=>!cells.Any(cell=>ReferenceEquals(cell.Item,r)&&cell.Column==c))))throw new ArgumentException("Select a rectangular range to cut.");
        var text=Tsv(items.Select(r=>columns.Select(c=>r[fields[Table.Columns.IndexOf(c)].Key])));
        WriteClipboard(text);ClearSelectedCells(); // Do not clear anything if clipboard access fails.
    }
    private static TextBox? FindTextBox(DependencyObject root,string name)
    {
        if(root is TextBox input&&System.Windows.Automation.AutomationProperties.GetName(input)==name)return input;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(FindTextBox(VisualTreeHelper.GetChild(root,i),name) is {} result)return result;
        return null;
    }
    private void OnRowsChanged(object? sender,System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if(e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Replace&&e.OldItems is {Count:1}&&e.NewItems is {Count:1})
        {
            var index=displayRows.IndexOf((GridRow)e.OldItems[0]!);
            var sorted=source.View is ListCollectionView {CustomSort:not null};
            var order=sorted?source.View.Cast<GridRow>().ToList():null;
            var position=order?.IndexOf((GridRow)e.OldItems[0]!)??-1;
            if(index>=0)displayRows[index]=(GridRow)e.NewItems[0]!;
            if(position>=0){order![position]=(GridRow)e.NewItems[0]!;((ListCollectionView)source.View).CustomSort=new FixedViewOrder(order,rows);}
            RefreshRecordCount();return;
        }

        if(e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {foreach(var row in displayRows.Where(r=>!blanks.Contains(r)).ToArray())displayRows.Remove(row);}
        if(e.OldItems!=null)foreach(GridRow row in e.OldItems)displayRows.Remove(row);
        if(e.NewItems!=null)foreach(GridRow row in e.NewItems)if(!displayRows.Contains(row))displayRows.Insert(displayRows.Count-blanks.Count,row);
        RefreshRecordCount();
    }
    private sealed class FixedViewOrder(List<GridRow> order,ObservableCollection<GridRow> rows):System.Collections.IComparer
    {
        private readonly Dictionary<int,int> slots=order.Select((row,index)=>(slot:rows.IndexOf(row),index)).Where(pair=>pair.slot>=0).ToDictionary(pair=>pair.slot,pair=>pair.index);
        private readonly Dictionary<GridRow,int> spare=order.Select((row,index)=>(row,index)).Where(pair=>!rows.Contains(pair.row)).ToDictionary(pair=>pair.row,pair=>pair.index);
        private int Rank(object? item){if(item is not GridRow row)return int.MaxValue;var slot=rows.IndexOf(row);return slot>=0?slots.GetValueOrDefault(slot,int.MaxValue):spare.GetValueOrDefault(row,int.MaxValue);}
        public int Compare(object? x,object? y)=>Rank(x).CompareTo(Rank(y));
    }
    private static Style EditingStyle()
    {
        var style=new Style(typeof(TextBox));style.Setters.Add(new Setter(TextBox.MinHeightProperty,0d));
        style.Setters.Add(new Setter(TextBox.PaddingProperty,new Thickness(8,2,8,2)));style.Setters.Add(new Setter(TextBox.VerticalContentAlignmentProperty,VerticalAlignment.Center));return style;
    }
    private void AddBlankRows()
    {
        if(promote==null)return;
        while(blanks.Count<100)
        {
            var row=new GridRow();blanks.Add(row);displayRows.Add(row);
            row.Changed+=changed=> {
                if(!blanks.Contains(changed)||!fields.Any(f=>!f.Calculated&&!string.IsNullOrWhiteSpace(changed[f.Key])))return;
                blanks.Remove(changed);promote(changed);AddBlankRows();
            };
        }
    }
    internal sealed class BlankLastComparer(HashSet<GridRow> blanks,Field field,bool ascending):System.Collections.IComparer
    {
        public int Compare(object? x,object? y)
        {
            if(x is not GridRow left||y is not GridRow right)return 0;
            if(blanks.Contains(left)!=blanks.Contains(right))return blanks.Contains(left)?1:-1;
            var a=left[field.Key];var b=right[field.Key];
            var result=string.Compare(a,b,StringComparison.OrdinalIgnoreCase);
            if(field.IsDate&&DateOnly.TryParse(a,out var da)&&DateOnly.TryParse(b,out var db))result=da.CompareTo(db);
            else if(field.IsNumeric&&decimal.TryParse(a,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var na)&&decimal.TryParse(b,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var nb))result=na.CompareTo(nb);
            return ascending?result:-result;
        }
    }
    public void CopySelection()
    {
        Table.Focus();
        try { if(ApplicationCommands.Copy.CanExecute(null,Table)) ApplicationCommands.Copy.Execute(null,Table); }
        catch(System.Runtime.InteropServices.ExternalException) {Feedback="Clipboard is busy. Retry copy.";}
    }
    public void PasteClipboard()
    {
        if(Table.IsReadOnly)return;
        try {
            if(Clipboard.ContainsText()){
                var target=Table.CurrentItem as GridRow;RecordClipboard? record=null;
                if(Clipboard.GetData(RecordFormat) is string metadata&&metadata.Length<16000){try{record=System.Text.Json.JsonSerializer.Deserialize<RecordClipboard>(metadata);}catch(System.Text.Json.JsonException){}}
                clipboardPaste=true;
                try
                {
                    if(Paste(Clipboard.GetText()))
                    {
                        if(record?.Fields is {} keys&&target!=null&&keys.SequenceEqual(Table.Columns.OrderBy(c=>c.DisplayIndex).Select(c=>fields[Table.Columns.IndexOf(c)].Key))&&keys.Contains("F9")&&int.TryParse(record.Terms,out var days)&&days>=0&&days<=3650)target["_terms"]=record.Terms;
                        Edited?.Invoke();
                    }
                }
                finally{clipboardPaste=false;}
            }else Feedback="Copy a range of cells before pasting.";
        }
        catch(System.Runtime.InteropServices.ExternalException) {Feedback="Clipboard is busy. Retry paste.";}
    }
    public bool Paste(string text)
    {
        if (Table.IsReadOnly) { Feedback = "This service snapshot is read-only."; return false; }
        Table.CommitEdit(); Table.CommitEdit(DataGridEditingUnit.Row,true);
        var visible = source.View.Cast<GridRow>().ToList();
        int row = visible.IndexOf(Table.CurrentItem as GridRow ?? visible.FirstOrDefault()!);
        int col = Table.CurrentColumn?.DisplayIndex ?? 0;
        if (row < 0) { Feedback = "Clear the filter or add a row before pasting."; return false; }
        var ordered = Table.Columns.OrderBy(c => c.DisplayIndex).Select(c => fields[Table.Columns.IndexOf(c)]).ToArray();
        try {
            var changes = PastePlan.Create(text,row,col,visible.Count,ordered);
            if(changes.Any(change=>CanEditRecord?.Invoke(visible[change.Row])==false))throw new ArgumentException("Use New Invoice to create a record before pasting its amounts or shipment details.");
            foreach(var change in changes){var field=ordered.First(f=>f.Key==change.Key);if(field.IsDate&&!DateCellEditor.IsValidDate(change.Value))throw new ArgumentException("Paste contains an invalid date. No cells were changed.");}
            BeforeChange?.Invoke();
            foreach (var change in changes) {
                var field=ordered.First(f=>f.Key==change.Key);
                visible[change.Row][change.Key] = field.IsDate?DateCellConverter.Normalize(change.Value):change.Value;
            }
            Feedback = $"Pasted {changes.Count} input cells — click Save to persist changes.";
            if(!clipboardPaste)Edited?.Invoke();
            return true;
        } catch (ArgumentException ex) { Feedback = ex.Message;return false; }
    }
}

internal sealed class FieldErrorConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)
    {
        var key=parameter.ToString()!;var boolean=key.EndsWith('?');if(boolean)key=key[..^1];
        var error=value is IReadOnlyDictionary<string,string> errors?errors.GetValueOrDefault(key,""):"";
        return boolean?(object)(error.Length>0):error;
    }
    public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture)=>throw new NotSupportedException();
}

public record CellChange(int Row,string Key,string Value);
public static class PastePlan
{
    public static IReadOnlyList<CellChange> Create(string text,int startRow,int startColumn,int rowCount,IReadOnlyList<Field> fields)
    {
        var lines = text.Replace("\r\n","\n").Replace('\r','\n').TrimEnd('\n').Split('\n');
        if (lines.Length > 1000 || text.Length > 1_000_000) throw new ArgumentException("Paste is limited to 1,000 rows and 1 MB.");
        var cells = lines.Select(x => x.Split('\t')).ToArray();
        if (cells.Any(x => x.Length != cells[0].Length)) throw new ArgumentException("Paste must be a rectangular tab-separated range.");
        if (startRow < 0 || startColumn < 0 || startRow + cells.Length > rowCount || startColumn + cells[0].Length > fields.Count)
            throw new ArgumentException("Paste exceeds available rows or columns. Add rows first; no cells were changed.");
        List<CellChange> result = [];
        for (int r=0;r<cells.Length;r++) for (int c=0;c<cells[r].Length;c++) {
            var field = fields[startColumn+c];
            if (field.IsReadOnly && startColumn==0 && cells[r].Length==fields.Count) continue;
            if (field.IsReadOnly) throw new ArgumentException("Paste touches a locked column. Select input columns only; no cells were changed.");
            result.Add(new(startRow+r,field.Key,cells[r][c]));
        }
        return result;
    }
}
