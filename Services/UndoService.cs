namespace UmlStudio.Services;

public sealed class UndoService
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private const int Max = 80;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Push(string snapshotJson)
    {
        _undo.Push(snapshotJson);
        if (_undo.Count > Max) Truncate(_undo);
        _redo.Clear();
    }

    public string? Undo(string currentJson)
    {
        if (_undo.Count == 0) return null;
        _redo.Push(currentJson);
        return _undo.Pop();
    }

    public string? Redo(string currentJson)
    {
        if (_redo.Count == 0) return null;
        _undo.Push(currentJson);
        return _redo.Pop();
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private static void Truncate(Stack<string> stack)
    {
        var arr = stack.ToArray();
        stack.Clear();
        for (int i = Math.Min(Max - 1, arr.Length - 1); i >= 0; i--)
            stack.Push(arr[i]);
    }
}
