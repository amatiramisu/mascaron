using System.Numerics;
using Mascaron.Core;
using Mascaron.Visualization;

namespace Mascaron.UI.Canvas;

/// <summary>
/// Where each sculptable bone is drawn, in face-normalized units: its template position plus the
/// bone's real model-space displacement scaled to the template, so handles stay on the drawn features.
/// </summary>
public sealed class BoneLayout
{
    private readonly Dictionary<string, Vector2> positions = new();
    private readonly Dictionary<string, float> depths = new();

    /// <summary>Face-normalized position of every bone the current template shows.</summary>
    public IReadOnlyDictionary<string, Vector2> Positions => positions;

    /// <summary>Face units per model unit; 0 while no skeleton sample exists.</summary>
    public float FaceUnitsPerModelUnit { get; private set; }

    /// <summary>Forward displacement in face units (positive = toward the viewer).</summary>
    public float GetDepth(string codename) => depths.GetValueOrDefault(codename);

    public void Rebuild(FaceTemplate template, BoneTransformState state, BoneFrameSnapshot? frames)
    {
        positions.Clear();
        depths.Clear();
        FaceUnitsPerModelUnit = ComputeScale(template, frames);

        foreach (var bone in FaceBoneRegistry.Bones)
        {
            if (!bone.IsSculptable || RaceTemplates.GetPosition(template, bone.Codename) is not { } position)
                continue;

            var transform = state.Get(bone.Codename);
            if (transform.Translation != Vector3.Zero && frames != null && FaceUnitsPerModelUnit > 0f &&
                frames.TryGet(bone.Codename, out var frame))
            {
                var translationFrame = CustomizePlusTransformMath.GetTranslationFrame(frame.BoneToModel, transform.Rotation);
                var modelOffset = Vector3.Transform(transform.Translation, translationFrame) * FaceUnitsPerModelUnit;
                position += new Vector2(-modelOffset.X, -modelOffset.Y);
                depths[bone.Codename] = modelOffset.Z;
            }

            positions[bone.Codename] = position;
        }
    }

    private static float ComputeScale(FaceTemplate template, BoneFrameSnapshot? frames)
    {
        if (frames == null || frames.EyeDistance <= 0f ||
            RaceTemplates.GetPosition(template, "j_f_eye_l") is not { } leftEye ||
            RaceTemplates.GetPosition(template, "j_f_eye_r") is not { } rightEye)
            return 0f;

        return Vector2.Distance(leftEye, rightEye) / frames.EyeDistance;
    }
}
