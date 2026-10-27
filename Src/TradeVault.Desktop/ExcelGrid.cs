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
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new(20,8,20,8), FontSize=11, Foreground=Brushes.DimGray };
    private readonly CollectionViewSource source;
    public event Action? Edited;
    public ExcelGrid(IReadOnlyList<Field> fields, ObservableCollection<GridRow> rows, bool readOnly = false)
    {
        this.fields = fields; this.rows = rows;
        var root = new DockPanel();
        var search = new TextBox { Width=240, Padding = new(8,5,8,5), ToolTip = "Filter visible rows across all columns" };
        System.Windows.Automation.AutomationProperties.SetName(search, "Filter rows");
        var tools=new DockPanel {Margin=new(18,0,18,14)};
        var legend=new TextBlock {Text=readOnly?"Read-only worksheet":"Shaded columns are calculated",FontSize=11,Foreground=Ui.Color("#64748B"),VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(legend,Dock.Right);tools.Children.Add(legend);
        var filter=new StackPanel {Orientation=Orientation.Horizontal};
        filter.Children.Add(new TextBlock {Text="Search records",VerticalAlignment=VerticalAlignment.Center,Margin=new(0,0,10,0),FontSize=12,Foreground=Ui.Color("#64748B")});filter.Children.Add(search);tools.Children.Add(filter);
        DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
        var formula=new DockPanel {Height=36,Margin=new(0,0,0,1),Background=Ui.Color("#FAFBFD")};
        var address=new TextBlock {Text="A1",Width=76,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        DockPanel.SetDock(address,Dock.Left);formula.Children.Add(address);
        var fx=new TextBlock {Text="│   fx",Foreground=Brushes.Gray,Width=50,VerticalAlignment=VerticalAlignment.Center,FontStyle=FontStyles.Italic};
        DockPanel.SetDock(fx,Dock.Left);formula.Children.Add(fx);
        var value=new TextBox {IsReadOnly=true,BorderThickness=new(0),ToolTip="Selected cell value. Edit inputs directly in the worksheet."};
        System.Windows.Automation.AutomationProperties.SetName(value,"Selected cell value");formula.Children.Add(value);
        DockPanel.SetDock(formula,Dock.Top);root.Children.Add(formula);
        Table.CurrentCellChanged+=(_,_)=> {
            if(Table.CurrentItem is GridRow row && Table.CurrentColumn is DataGridColumn column) {
                int index=column.DisplayIndex;string letters="";for(int n=index+1;n>0;n=(n-1)/26)letters=(char)('A'+(n-1)%26)+letters;
                address.Text=$"{letters}{Table.Items.IndexOf(row)+1}";
                value.SetBinding(TextBox.TextProperty,new Binding($"[{fields[Table.Columns.IndexOf(column)].Key}]"){Source=row,Mode=BindingMode.OneWay});
            }
        };
        feedback.Text = readOnly ? "Read-only · Sorting changes the view, not the account order." : "Enter / Tab to navigate · Ctrl+C to copy · Ctrl+V to paste · Dates: yyyy-MM-dd";
        DockPanel.SetDock(feedback,Dock.Bottom); root.Children.Add(feedback);
        source = new CollectionViewSource { Source = rows };
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
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.PaddingProperty,new Thickness(12,5,12,5)));
            if (field.Calculated) style.Setters.Add(new Setter(TextBlock.BackgroundProperty,Ui.Color("#F0F5FF")));
            Table.Columns.Add(new DataGridTextColumn {
                Header = field.Label, Width = field.Width, IsReadOnly = field.Calculated,
                SortMemberPath = $"[{field.Key}]", ElementStyle = style,
                Binding = new Binding($"[{field.Key}]") { Mode = field.Calculated || readOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, ValidatesOnNotifyDataErrors = true }
            });
        }
        Table.FrozenColumnCount = Math.Min(2,fields.Count);
        Table.ItemsSource = source.View;
        Table.PreviewKeyDown += (_,e) => {
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && !readOnly)
            {
                e.Handled = true;
                try {
                    PasteClipboard();
                } catch (System.Runtime.InteropServices.ExternalException) { feedback.Text = "Clipboard is busy. Retry paste."; }
            }
        };
        Table.CellEditEnding += (_,_) => Dispatcher.BeginInvoke(new Action(() => Edited?.Invoke()));
        root.Children.Add(Table); Content = root;
    }
    public void CopySelection()
    {
        Table.Focus();
        try { if(ApplicationCommands.Copy.CanExecute(null,Table)) ApplicationCommands.Copy.Execute(null,Table); }
        catch(System.Runtime.InteropServices.ExternalException) {feedback.Text="Clipboard is busy. Retry copy.";}
    }
    public void PasteClipboard()
    {
        if(Table.IsReadOnly)return;
        try {if(Clipboard.ContainsText())Paste(Clipboard.GetText());else feedback.Text="Copy a range of cells before pasting.";}
        catch(System.Runtime.InteropServices.ExternalException) {feedback.Text="Clipboard is busy. Retry paste.";}
    }
    public void Paste(string text)
    {
        if (Table.IsReadOnly) { feedback.Text = "This service snapshot is read-only."; return; }
        Table.CommitEdit(); Table.CommitEdit(DataGridEditingUnit.Row,true);
        var visible = source.View.Cast<GridRow>().ToList();
        int row = visible.IndexOf(Table.CurrentItem as GridRow ?? visible.FirstOrDefault()!);
        int col = Table.CurrentColumn?.DisplayIndex ?? 0;
        if (row < 0) { feedback.Text = "Clear the filter or add a row before pasting."; return; }
        var ordered = Table.Columns.OrderBy(c => c.DisplayIndex).Select(c => fields[Table.Columns.IndexOf(c)]).ToArray();
        try {
            var changes = PastePlan.Create(text,row,col,visible.Count,ordered);
            foreach (var change in changes) visible[change.Row][change.Key] = change.Value;
            feedback.Text = $"Pasted {changes.Count} input cells — click Save to persist changes.";
            Edited?.Invoke();
        } catch (ArgumentException ex) { feedback.Text = ex.Message; }
    }
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
            if (field.Calculated) throw new ArgumentException("Paste touches a locked calculated column. Select input columns only; no cells were changed.");
            result.Add(new(startRow+r,field.Key,cells[r][c]));
        }
        return result;
    }
}
