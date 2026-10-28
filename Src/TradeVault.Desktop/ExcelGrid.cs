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
    private readonly ObservableCollection<GridRow> rows;
    private readonly IReadOnlyList<Field> fields;
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new(8,3,8,3), FontSize=11, Foreground=Ui.Color("#B42318"),Visibility=Visibility.Collapsed };
    private string Feedback{set{feedback.Text=value;feedback.Visibility=string.IsNullOrWhiteSpace(value)?Visibility.Collapsed:Visibility.Visible;}}
    private readonly CollectionViewSource source;
    private readonly ObservableCollection<GridRow> displayRows;
    private readonly HashSet<GridRow> blanks=[];
    private readonly Action<GridRow>? promote;
    public event Action? Edited;
    public ExcelGrid(IReadOnlyList<Field> fields, ObservableCollection<GridRow> rows, bool readOnly = false,Action<GridRow>? promote=null)
    {
        this.fields = fields; this.rows = rows;this.promote=promote;
        displayRows=new(rows);
        if(promote!=null&&!readOnly)
        {
            AddBlankRows();
            System.Collections.Specialized.CollectionChangedEventManager.AddHandler(rows,OnRowsChanged);
        }
        var root = new DockPanel();
        var search = new TextBox { Width=200,MinHeight=28, Padding = new(8,3,8,3), ToolTip = "Filter visible rows across all columns" };
        System.Windows.Automation.AutomationProperties.SetName(search, "Filter rows");
        var tools=new DockPanel {Margin=new(8,4,8,4)};
        var legend=new TextBlock {Text=readOnly?"Read-only worksheet":"Shaded columns are calculated",FontSize=11,Foreground=Ui.Color("#64748B"),VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(legend,Dock.Right);tools.Children.Add(legend);
        var filter=new StackPanel {Orientation=Orientation.Horizontal};
        filter.Children.Add(new TextBlock {Text="Search records",VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,10,0),FontSize=12,Foreground=Ui.Color("#64748B")});filter.Children.Add(search);tools.Children.Add(filter);
        DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        var formula=new DockPanel {Height=28,Margin=new(0,0,0,1),Background=Ui.Color("#FAFBFD")};
        var address=new TextBlock {Text="A1",Width=76,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(address,Dock.Left);formula.Children.Add(address);
        var fx=new TextBlock {Text="│   fx",Foreground=Brushes.Gray,Width=50,VerticalAlignment=VerticalAlignment.Center,FontStyle=FontStyles.Italic};
        DockPanel.SetDock(fx,Dock.Left);formula.Children.Add(fx);
        var value=new TextBox {IsReadOnly=true,MinHeight=0,Padding=new(6,2,6,2),BorderThickness=new(0),ToolTip="Selected cell value. Edit inputs directly in the worksheet."};
        System.Windows.Automation.AutomationProperties.SetName(value,"Selected cell value");formula.Children.Add(value);
        DockPanel.SetDock(formula,Dock.Top);root.Children.Add(formula);
        Table.CurrentCellChanged+=(_,_)=> {
            if(Table.CurrentItem is GridRow row && Table.CurrentColumn is DataGridColumn column) {
                int index=column.DisplayIndex;string letters="";for(int n=index+1;n>0;n=(n-1)/26)letters=(char)('A'+(n-1)%26)+letters;
                address.Text=$"{letters}{Table.Items.IndexOf(row)+1}";
                value.SetBinding(TextBox.TextProperty,new Binding($"[{fields[Table.Columns.IndexOf(column)].Key}]"){Source=row,Mode=BindingMode.OneWay,ValidatesOnNotifyDataErrors=false});
            }
        };
        DockPanel.SetDock(feedback,Dock.Top); root.Children.Add(feedback);
        source = new CollectionViewSource { Source = displayRows };
        source.Filter += (_,e) => e.Accepted = e.Item is GridRow r && fields.Any(f => r[f.Key].Contains(search.Text,StringComparison.OrdinalIgnoreCase));
        search.TextChanged += (_,_) => { Table.CommitEdit(); Table.CommitEdit(DataGridEditingUnit.Row,true); source.View.Refresh(); };
        Table.AutoGenerateColumns = false; Table.CanUserAddRows = false; Table.CanUserDeleteRows = false;
        Table.IsReadOnly = readOnly; Table.SelectionUnit = DataGridSelectionUnit.Cell;
        Table.SelectionMode = DataGridSelectionMode.Extended;
        Table.HeadersVisibility = DataGridHeadersVisibility.All; Table.RowHeaderWidth = 42;
        Table.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
        Table.LoadingRow += (_,e) => e.Row.Header = (e.Row.GetIndex()+1).ToString();
        foreach (var field in fields)
        {
            var cellStyle=new Style(typeof(DataGridCell),(Style)FindResource(typeof(DataGridCell)));
            var fieldError=new Binding(nameof(GridRow.Errors)){Converter=new FieldErrorConverter(),ConverterParameter=field.Key};
            cellStyle.Setters.Add(new Setter(ToolTipProperty,fieldError));
            var invalid=new DataTrigger{Binding=new Binding(nameof(GridRow.Errors)){Converter=new FieldErrorConverter(),ConverterParameter=field.Key+"?"},Value=true};
            invalid.Setters.Add(new Setter(Border.BorderBrushProperty,Ui.Color("#C2413C")));cellStyle.Triggers.Add(invalid);
            if(field.IsDate&&!field.Calculated&&!readOnly)
            {
                var factory=new FrameworkElementFactory(typeof(DateCellEditor));
                factory.SetBinding(DateCellEditor.ValueProperty,new Binding($"[{field.Key}]"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged,ValidatesOnNotifyDataErrors=false});
                var template=new DataTemplate{VisualTree=factory};
                Table.Columns.Add(new DataGridTemplateColumn{Header=field.Label,Width=CompactWidth(field),CellTemplate=template,CellEditingTemplate=template,SortMemberPath=$"[{field.Key}]",CellStyle=cellStyle});
                continue;
            }
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.PaddingProperty,new Thickness(8,3,8,3)));
            if (field.Calculated) style.Setters.Add(new Setter(TextBlock.BackgroundProperty,Ui.Color("#F0F5FF")));
            Table.Columns.Add(new DataGridTextColumn {
                Header = field.Label, Width = field.Width==240?CompactWidth(field):field.Width, IsReadOnly = field.Calculated,
                SortMemberPath = $"[{field.Key}]", ElementStyle = style, CellStyle=cellStyle,
                Binding = new Binding($"[{field.Key}]") { Mode = field.Calculated || readOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                    UpdateSourceTrigger = field.IsDate?UpdateSourceTrigger.LostFocus:UpdateSourceTrigger.PropertyChanged,
                    Converter=field.IsDate?new DateCellConverter():null, ValidatesOnNotifyDataErrors = false },
                EditingElementStyle=EditingStyle()
            });
        }
        Table.FrozenColumnCount = Math.Min(2,fields.Count);
        Table.ItemsSource = source.View;
        if(promote!=null)Table.Sorting+=(_,e)=> {
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
    }
    private static TextBox? FindInput(DependencyObject root)
    {
        if(root is TextBox input)return input;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(FindInput(VisualTreeHelper.GetChild(root,i)) is {} found)return found;
        return null;
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
    private static TextBox? FindTextBox(DependencyObject root,string name)
    {
        if(root is TextBox input&&System.Windows.Automation.AutomationProperties.GetName(input)==name)return input;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(FindTextBox(VisualTreeHelper.GetChild(root,i),name) is {} result)return result;
        return null;
    }
    private void OnRowsChanged(object? sender,System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if(e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {foreach(var row in displayRows.Where(r=>!blanks.Contains(r)).ToArray())displayRows.Remove(row);}
        if(e.OldItems!=null)foreach(GridRow row in e.OldItems)displayRows.Remove(row);
        if(e.NewItems!=null)foreach(GridRow row in e.NewItems)if(!displayRows.Contains(row))displayRows.Insert(displayRows.Count-blanks.Count,row);
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
        try {if(Clipboard.ContainsText())Paste(Clipboard.GetText());else Feedback="Copy a range of cells before pasting.";}
        catch(System.Runtime.InteropServices.ExternalException) {Feedback="Clipboard is busy. Retry paste.";}
    }
    public void Paste(string text)
    {
        if (Table.IsReadOnly) { Feedback = "This service snapshot is read-only."; return; }
        Table.CommitEdit(); Table.CommitEdit(DataGridEditingUnit.Row,true);
        var visible = source.View.Cast<GridRow>().ToList();
        int row = visible.IndexOf(Table.CurrentItem as GridRow ?? visible.FirstOrDefault()!);
        int col = Table.CurrentColumn?.DisplayIndex ?? 0;
        if (row < 0) { Feedback = "Clear the filter or add a row before pasting."; return; }
        var ordered = Table.Columns.OrderBy(c => c.DisplayIndex).Select(c => fields[Table.Columns.IndexOf(c)]).ToArray();
        try {
            var changes = PastePlan.Create(text,row,col,visible.Count,ordered);
            foreach(var change in changes){var field=ordered.First(f=>f.Key==change.Key);if(field.IsDate&&!DateCellEditor.IsValidDate(change.Value))throw new ArgumentException("Paste contains an invalid date. No cells were changed.");}
            foreach (var change in changes) {
                var field=ordered.First(f=>f.Key==change.Key);
                visible[change.Row][change.Key] = field.IsDate?DateCellConverter.Normalize(change.Value):change.Value;
            }
            Feedback = $"Pasted {changes.Count} input cells — click Save to persist changes.";
            Edited?.Invoke();
        } catch (ArgumentException ex) { Feedback = ex.Message; }
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
            if (field.Calculated && startColumn==0 && cells[r].Length==fields.Count) continue;
            if (field.Calculated) throw new ArgumentException("Paste touches a locked calculated column. Select input columns only; no cells were changed.");
            result.Add(new(startRow+r,field.Key,cells[r][c]));
        }
        return result;
    }
}
