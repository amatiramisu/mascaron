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
    private int version;

    public int Version => version;

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

    public void ResetAll()
    {
        transforms.Clear();
        version++;
    }

    public Dictionary<string, BoneTransform> CreateSnapshot()
    {
        return new Dictionary<string, BoneTransform>(transforms);
    }

    public void RestoreSnapshot(IReadOnlyDictionary<string, BoneTransform> snapshot)
    {
        transforms.Clear();
        foreach (var (boneName, transform) in snapshot)
            transforms[boneName] = transform;
        version++;
    }

    public IEnumerable<KeyValuePair<string, BoneTransform>> GetModified()
    {
        return transforms.Where(kv => kv.Value.IsModified);
    }

    public int ModifiedCount => transforms.Count(kv => kv.Value.IsModified);
}
