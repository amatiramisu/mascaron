namespace Mascaron.Core;

/// <summary>
/// The editable sculpt: transform state, brush engine, stroke history and the stroke in progress.
/// </summary>
public sealed class SculptSession
{
    private bool activeStrokeRecorded;

    public SculptSession(BoneTransformState state, SculptEngine engine, SculptStrokeHistory history, IBoneFrameSource frames)
    {
        State = state;
        Engine = engine;
        History = history;
        Frames = frames;
    }

    public BoneTransformState State { get; }
    public SculptEngine Engine { get; }
    public SculptStrokeHistory History { get; }
    public IBoneFrameSource Frames { get; }
    public SculptStroke? ActiveStroke { get; private set; }

    public void BeginStroke(SculptStroke stroke)
    {
        ActiveStroke = stroke;
        activeStrokeRecorded = false;
    }

    /// <summary>
    /// Applies the active stroke after its delta changed; the first change records it in history.
    /// </summary>
    public void ApplyActiveStroke()
    {
        if (ActiveStroke == null)
            return;

        if (!activeStrokeRecorded)
        {
            History.Add(ActiveStroke, State);
            activeStrokeRecorded = true;
        }

        ActiveStroke.Apply(State, StrokeBase.CapturedBaseline);
    }

    public void EndStroke()
    {
        ActiveStroke = null;
        activeStrokeRecorded = false;
    }

    public bool Undo()
    {
        EndStroke();
        return History.UndoLatest(State);
    }

    /// <summary>
    /// Re-applies history after a past stroke was edited.
    /// </summary>
    public void ReplayHistory()
    {
        History.Replay(State);
    }

    /// <summary>
    /// Replaces the whole sculpt; history restarts from the new state.
    /// </summary>
    public void ReplaceState(IEnumerable<KeyValuePair<string, BoneTransform>> transforms)
    {
        EndStroke();
        History.Clear();
        State.ResetAll();
        foreach (var (bone, transform) in transforms)
            State.Set(bone, transform);
    }
}
