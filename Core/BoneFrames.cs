using System.Numerics;

namespace Mascaron.Core;

/// <summary>
/// Model-space orientation of a face bone (character space: X = character left, Y = up, Z = forward),
/// sampled from the live skeleton before any Mascaron edit is written.
/// </summary>
public readonly record struct BoneFrame(Quaternion BoneToModel);

/// <summary>
/// One sample of face bone frames taken from the local player's skeleton.
/// </summary>
public sealed class BoneFrameSnapshot
{
    private readonly Dictionary<string, BoneFrame> frames;

    public BoneFrameSnapshot(Dictionary<string, BoneFrame> frames, float eyeDistance)
    {
        this.frames = frames;
        EyeDistance = eyeDistance;
    }

    /// <summary>
    /// Model-space distance between j_f_eye_l and j_f_eye_r, used to scale canvas display.
    /// </summary>
    public float EyeDistance { get; }

    public bool TryGet(string codename, out BoneFrame frame)
    {
        return frames.TryGetValue(codename, out frame);
    }
}

/// <summary>
/// Supplies the most recent bone frame sample, or null while no character skeleton is available.
/// </summary>
public interface IBoneFrameSource
{
    BoneFrameSnapshot? LatestFrames { get; }
}
