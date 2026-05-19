using System.Numerics;

namespace Mascaron.Core;

public struct BoneTransform
{
    public Vector3 Translation;
    public Vector3 Rotation;
    public Vector3 Scaling;

    public static BoneTransform Identity => new()
    {
        Translation = Vector3.Zero,
        Rotation = Vector3.Zero,
        Scaling = Vector3.One,
    };

    public bool IsModified =>
        Translation != Vector3.Zero ||
        Rotation != Vector3.Zero ||
        Scaling != Vector3.One;
}

public class BoneTransformState
{
    private readonly Dictionary<string, BoneTransform> transforms = new();
    private readonly List<Dictionary<string, BoneTransform>> undoStack = new();
    private const int MaxUndoLevels = 50;
    private int version;

    public int Version => version;
    public bool CanUndo => undoStack.Count > 0;

    public BoneTransform Get(string codename)
    {
        return transforms.GetValueOrDefault(codename, BoneTransform.Identity);
    }

    public void Set(string codename, BoneTransform transform)
    {
        transforms[codename] = transform;
        version++;
    }

    public void Reset(string codename)
    {
        transforms.Remove(codename);
        version++;
    }

    public void PushUndo()
    {
        if (undoStack.Count >= MaxUndoLevels)
            undoStack.RemoveAt(0);
        undoStack.Add(new Dictionary<string, BoneTransform>(transforms));
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
            return;

        var snapshot = undoStack[^1];
        undoStack.RemoveAt(undoStack.Count - 1);
        transforms.Clear();
        foreach (var (k, v) in snapshot)
            transforms[k] = v;
        version++;
    }

    public void ResetAll()
    {
        transforms.Clear();
        undoStack.Clear();
        version++;
    }

    public IEnumerable<KeyValuePair<string, BoneTransform>> GetModified()
    {
        return transforms.Where(kv => kv.Value.IsModified);
    }

    public int ModifiedCount => transforms.Count(kv => kv.Value.IsModified);
}
