using System.Text.Json;

namespace TradeVault.Desktop;

internal sealed record WorksheetEditState(List<Dictionary<string,string>> Rows,decimal Opening,int Terms)
{
    private string? signature;
    internal string Signature=>signature??=JsonSerializer.Serialize(this);
}
internal sealed record RemovedWorksheetRecord(Dictionary<string,string> Data,int Index)
{
    internal Dictionary<string,string>? StoredData{get;set;}
}
internal sealed record WorksheetHistoryStep(WorksheetEditState Before,WorksheetEditState After,RemovedWorksheetRecord? Removed);

/// <summary>Memory-only input history for one worksheet. Calculated values are never replayed.</summary>
internal sealed class WorksheetHistory
{
    private readonly List<WorksheetHistoryStep> undo=[];
    private readonly Stack<WorksheetHistoryStep> redo=[];
    private WorksheetEditState? current;
    internal WorksheetHistoryStep? UndoStep=>undo.LastOrDefault();
    internal WorksheetHistoryStep? RedoStep=>redo.TryPeek(out var step)?step:null;
    internal void Reset(WorksheetEditState state){undo.Clear();redo.Clear();current=state;}
    internal void Capture(WorksheetEditState state,RemovedWorksheetRecord? removed=null)
    {
        if(current==null){Reset(state);return;}
        if(current.Signature==state.Signature)return;
        undo.Add(new(current,state,removed));redo.Clear();current=state;
        while(undo.Count>100||undo.Count>1&&undo.Sum(s=>(long)s.Before.Signature.Length+s.After.Signature.Length)>8_000_000)undo.RemoveAt(0);
    }
    internal void CompleteUndo(){var step=undo[^1];undo.RemoveAt(undo.Count-1);redo.Push(step);current=step.Before;}
    internal void CompleteRedo(){var step=redo.Pop();undo.Add(step);current=step.After;}
}
