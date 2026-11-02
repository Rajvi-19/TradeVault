using System.Windows;
using System.Windows.Controls;

namespace TradeVault.Desktop;

public sealed partial class MainWindow
{
    private void EnableMasterNameEditing()
    {
        if(activePage is not("parties" or "agents")||grid==null)return;
        ((DataGridTextColumn)grid.Table.Columns[0]).Binding=new System.Windows.Data.Binding("[A]"){Mode=System.Windows.Data.BindingMode.TwoWay,UpdateSourceTrigger=System.Windows.Data.UpdateSourceTrigger.Explicit};
        grid.Table.IsReadOnly=false;grid.ShowNameEditingHint();SetSaveState(true);
        foreach(var column in grid.Table.Columns)column.IsReadOnly=column!=grid.Table.Columns[0];
        var row=grid.Table.CurrentItem as GridRow??grid.Table.Items.Cast<GridRow>().FirstOrDefault();
        if(row==null)return;
        grid.Table.CurrentCell=new DataGridCellInfo(row,grid.Table.Columns[0]);grid.Table.ScrollIntoView(row,grid.Table.Columns[0]);grid.Table.UpdateLayout();grid.Table.SelectedCells.Clear();grid.Table.SelectedCells.Add(grid.Table.CurrentCell);grid.Table.Focus();grid.Table.BeginEdit();
        status.Text="Edit a name, then press Enter or leave the cell to save.";
    }
    private bool MasterNameEditing=>activePage is "parties" or "agents"&&grid?.Table.IsReadOnly==false;
    private void FinishMasterNameEditing()
    {
        if(grid==null||!MasterNameEditing)return;
        if(!grid.Table.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.Table.CommitEdit(DataGridEditingUnit.Row,true))return;
        grid.Table.IsReadOnly=true;grid.ShowNameEditingHint(false);SetSaveState(false);status.Text="Name changes saved · read-only";
    }
    internal void CheckMasterRenamePages(){foreach(var page in new[]{"parties","agents"}){OpenPage(page);CheckMasterRenameInteraction();}}
    private void CheckMasterRenameInteraction()
    {
        if(grid!.Table.Items.Count==0)return;
        var wasVisible=IsVisible;var oldLeft=Left;var oldTop=Top;if(!wasVisible){Left=-20000;Top=-20000;ShowInTaskbar=false;ShowActivated=false;Show();UpdateLayout();ThemeChecks.Flush();}
        var table=grid.Table;table.CurrentCell=new DataGridCellInfo(table.Items[0],table.Columns[0]);EnableMasterNameEditing();table.UpdateLayout();ThemeChecks.Flush();
        static IEnumerable<TextBox> Inputs(DependencyObject node){if(node is TextBox input)yield return input;for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Inputs(System.Windows.Media.VisualTreeHelper.GetChild(node,i)))yield return child;}
        var editor=Inputs(table).FirstOrDefault()??throw new InvalidOperationException("Edit master must open a name editor.");
        if(table.Columns.Skip(1).Any(c=>!c.IsReadOnly))throw new InvalidOperationException("Only the master name may be edited.");
        var row=(GridRow)table.CurrentItem;var name="SYNTHETIC renamed "+Guid.NewGuid().ToString("N");editor.Text=name;
        if(!table.CommitEdit(DataGridEditingUnit.Cell,true))throw new InvalidOperationException("Master rename did not commit.");table.CommitEdit(DataGridEditingUnit.Row,true);ThemeChecks.Flush();
        var saved=activePage=="parties"?store.GetParties().Single(p=>p.Id.ToString()==row["_id"]).Name:store.GetAgents().Single(a=>a.Id==row["_id"]).Name;
        if(saved!=name||row["A"]!=name)throw new InvalidOperationException("Edited master name was not persisted and reflected in the grid.");
        if(!saveButton.IsEnabled)throw new InvalidOperationException("Save must be enabled during master-name editing.");
        saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));ThemeChecks.Flush();
        if(!table.IsReadOnly||saveButton.IsEnabled)throw new InvalidOperationException("Save must finish master editing and restore read-only mode.");
        if(!wasVisible){Hide();Left=oldLeft;Top=oldTop;}
    }
    private void ConfigureMasterNameEditing()
    {
        var table=grid!.Table;var page=activePage;
        table.CellEditEnding+=(_,e)=>
        {
            if(e.EditAction!=DataGridEditAction.Commit||e.Column!=table.Columns[0]||e.Row.Item is not GridRow row||e.EditingElement is not TextBox input)return;
            try
            {
                if(page=="parties")store.RenameParty(Guid.Parse(row["_id"]),input.Text);else store.RenameAgent(row["_id"],input.Text);
                var normalized=string.Join(" ",input.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));input.Text=normalized;row["A"]=normalized;
                Dispatcher.BeginInvoke(new Action(()=>{RefreshParties(allPartiesView?AllPartiesId:controller.PartyId);RefreshAgents();status.Text="Name saved across the workspace";}));
            }
            catch(Exception ex){e.Cancel=true;status.Text=ex.Message;Dispatcher.BeginInvoke(new Action(()=>{input.Focus();input.SelectAll();}));}
        };
    }
}








