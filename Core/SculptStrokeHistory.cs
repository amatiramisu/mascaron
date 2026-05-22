namespace Mascaron.Core;

public class SculptStrokeHistory
{
    private const int MaxStrokes = 50;

    private readonly List<SculptStroke> strokes = [];
    private Dictionary<string, BoneTransform> baseState = [];
    private int selectedIndex = -1;

    public IReadOnlyList<SculptStroke> Strokes => strokes;
    public int SelectedIndex => selectedIndex;
    public SculptStroke? SelectedStroke => selectedIndex >= 0 && selectedIndex < strokes.Count ? strokes[selectedIndex] : null;
    public bool HasStrokes => strokes.Count > 0;

    public void Add(SculptStroke stroke, BoneTransformState state)
    {
        if (strokes.Count > 0 && ReferenceEquals(strokes[^1], stroke))
            return;

        if (strokes.Count == 0)
            baseState = state.CreateSnapshot();

        strokes.Add(stroke);
        selectedIndex = strokes.Count - 1;

        if (strokes.Count > MaxStrokes)
            CompactOldestStroke();
    }

    public void Select(int index)
    {
        if (index < 0 || index >= strokes.Count)
            return;

        selectedIndex = index;
    }

    public void ClearSelection()
    {
        selectedIndex = -1;
    }

    public void SelectLatest()
    {
        selectedIndex = strokes.Count - 1;
    }

    public void Clear()
    {
        strokes.Clear();
        baseState.Clear();
        selectedIndex = -1;
    }

    public bool UndoLatest(BoneTransformState state)
    {
        if (strokes.Count == 0)
            return false;

        strokes.RemoveAt(strokes.Count - 1);
        selectedIndex = Math.Min(selectedIndex, strokes.Count - 1);

        state.RestoreSnapshot(baseState);
        foreach (var stroke in strokes)
            stroke.ApplyToCurrent(state);

        if (strokes.Count == 0)
            baseState.Clear();

        return true;
    }

    public void Replay(BoneTransformState state)
    {
        if (strokes.Count == 0)
            return;

        state.RestoreSnapshot(baseState);
        foreach (var stroke in strokes)
            stroke.ApplyToCurrent(state);
    }

    private void CompactOldestStroke()
    {
        var compacted = new BoneTransformState();
        compacted.RestoreSnapshot(baseState);
        strokes[0].ApplyToCurrent(compacted);
        baseState = compacted.CreateSnapshot();
        strokes.RemoveAt(0);
        selectedIndex = Math.Max(0, selectedIndex - 1);
    }
}
