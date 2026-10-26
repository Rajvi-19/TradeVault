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
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0,8,0,8) };
    private readonly CollectionViewSource source;
    public event Action? Edited;
    public ExcelGrid(IReadOnlyList<Field> fields, ObservableCollection<GridRow> rows, bool readOnly = false)
    {
        this.fields = fields; this.rows = rows;
        var root = new DockPanel();
        var search = new TextBox { Margin = new(0,0,0,8), Padding = new(8), ToolTip = "Filter visible rows across all columns" };
        System.Windows.Automation.AutomationProperties.SetName(search, "Filter rows");
        var filterLabel = new TextBlock { Text = "Filter rows · all columns", Margin = new(0,0,0,4) };
        DockPanel.SetDock(filterLabel,Dock.Top); root.Children.Add(filterLabel);
        DockPanel.SetDock(search,Dock.Top); root.Children.Add(search);
        feedback.Text = readOnly ? "Read-only service snapshot. Sorting changes display order only." : "Select a starting cell and paste tab-separated rows. Blue columns are locked. Dates: yyyy-MM-dd.";
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
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.PaddingProperty,new Thickness(9,7,9,7)));
            if (field.Calculated) style.Setters.Add(new Setter(TextBlock.BackgroundProperty,new SolidColorBrush(Color.FromRgb(224,239,247))));
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
                    if (Clipboard.ContainsText()) Paste(Clipboard.GetText());
                } catch (System.Runtime.InteropServices.ExternalException) { feedback.Text = "Clipboard is busy. Retry paste."; }
            }
        };
        Table.CellEditEnding += (_,_) => Dispatcher.BeginInvoke(new Action(() => Edited?.Invoke()));
        root.Children.Add(Table); Content = root;
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
            feedback.Text = $"Pasted {changes.Count} input cells. Values remain in memory; validate before mock save.";
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
