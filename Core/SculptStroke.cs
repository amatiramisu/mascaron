using System.Numerics;

namespace Mascaron.Core;

public enum SculptStrokeKind
{
    Move,
    Rotate,
    Scale,
}

/// <summary>
/// Which transform a stroke builds on when it is applied.
/// </summary>
public enum StrokeBase
{
    /// <summary>The bone transforms captured when the stroke started (live dragging).</summary>
    CapturedBaseline,

    /// <summary>Whatever the state currently holds (history replay).</summary>
    CurrentState,
}

/// <summary>
/// A bone touched by a stroke, with the transform and model-space frame captured at stroke start.
/// </summary>
public readonly record struct SculptStrokeTarget(BrushInfluence Influence, BoneTransform Baseline, BoneFrame Frame)
{
    public string Codename => Influence.Codename;
    public bool IsPrimary => Influence.Role != BrushRole.Falloff;
}

/// <summary>
/// One drag gesture. Move is recorded in character model space (X = character left, Y = up, Z = forward)
/// and converted per target into Customize+ bone-local translation. Rotate and Scale are recorded directly
/// in Customize+ bone-local values (rotation degrees, scale offsets).
/// </summary>
public sealed class SculptStroke
{
    private readonly List<SculptStrokeTarget> targets = [];

    public SculptStroke(string primaryBone, SculptStrokeKind kind, float radius, IEnumerable<SculptStrokeTarget> targets)
    {
        PrimaryBone = primaryBone;
        Kind = kind;
        Radius = ClampRadius(radius);
        this.targets.AddRange(targets);
        PrimaryFrame = FindPrimaryFrame();
    }

    public string PrimaryBone { get; }

    /// <summary>Model-space frame of the grabbed bone captured at stroke start; null when it is no target.</summary>
    public BoneFrame? PrimaryFrame { get; }
    public SculptStrokeKind Kind { get; }
    public IReadOnlyList<SculptStrokeTarget> Targets => targets;

    /// <summary>Brush radius in face-normalized units; editable after the fact from history.</summary>
    public float Radius { get; private set; }

    /// <summary>Model-space translation (Move), Customize+ rotation degrees (Rotate) or per-axis scale offset (Scale).</summary>
    public Vector3 Delta { get; private set; }

    public void AddDelta(Vector3 delta)
    {
        Delta += delta;
    }

    public void SetRadius(float radius)
    {
        Radius = ClampRadius(radius);
    }

    public float GetTargetStrength(in SculptStrokeTarget target)
    {
        return target.Influence.Evaluate(Radius).Strength;
    }

    public void Apply(BoneTransformState state, StrokeBase source)
    {
        foreach (var target in targets)
        {
            var (strength, mirrored) = target.Influence.Evaluate(Radius);
            if (strength <= 0f)
                continue;

            var current = source == StrokeBase.CapturedBaseline ? target.Baseline : state.Get(target.Codename);
            state.Set(target.Codename, ApplyToTarget(current, target.Frame, strength, mirrored));
        }
    }

    private BoneTransform ApplyToTarget(BoneTransform transform, BoneFrame frame, float strength, bool mirrored)
    {
        switch (Kind)
        {
            case SculptStrokeKind.Move:
            {
                var modelDelta = (mirrored ? MirrorVector(Delta) : Delta) * strength;
                var translationFrame = CustomizePlusTransformMath.GetTranslationFrame(frame.BoneToModel, transform.Rotation);
                transform.Translation += Vector3.Transform(modelDelta, Quaternion.Inverse(translationFrame));
                break;
            }
            case SculptStrokeKind.Rotate:
            {
                var rotationDelta = (mirrored ? MirrorRotationDegrees(Delta) : Delta) * strength;
                transform.Rotation = CustomizePlusTransformMath.WrapAngles(transform.Rotation + rotationDelta);
                break;
            }
            case SculptStrokeKind.Scale:
            {
                var axisFrame = CustomizePlusTransformMath.GetTranslationFrame(frame.BoneToModel, transform.Rotation);
                transform.Scaling *= MapModelScaleToLocal(Delta * strength, axisFrame);
                break;
            }
        }

        return transform;
    }

    /// <summary>
    /// Assigns each bone-local axis the model-axis scale offset it is most aligned with.
    /// </summary>
    private static Vector3 MapModelScaleToLocal(Vector3 modelOffset, Quaternion localToModel)
    {
        return new Vector3(
            1f + PickAlignedComponent(Vector3.Transform(Vector3.UnitX, localToModel), modelOffset),
            1f + PickAlignedComponent(Vector3.Transform(Vector3.UnitY, localToModel), modelOffset),
            1f + PickAlignedComponent(Vector3.Transform(Vector3.UnitZ, localToModel), modelOffset));
    }

    private static float PickAlignedComponent(Vector3 axisInModel, Vector3 modelOffset)
    {
        var abs = Vector3.Abs(axisInModel);
        if (abs.X >= abs.Y && abs.X >= abs.Z)
            return modelOffset.X;
        return abs.Y >= abs.Z ? modelOffset.Y : modelOffset.Z;
    }

    /// <summary>Reflection across the character's sagittal plane for a position delta.</summary>
    private static Vector3 MirrorVector(Vector3 v) => new(-v.X, v.Y, v.Z);

    /// <summary>
    /// Mirror partner rotation with Customize+'s face bone reflection (BoneTransform.GetStandardReflection):
    /// yaw (X) and pitch (Y) flip, roll (Z) carries over.
    /// </summary>
    private static Vector3 MirrorRotationDegrees(Vector3 v) => new(-v.X, -v.Y, v.Z);

    private BoneFrame? FindPrimaryFrame()
    {
        foreach (var target in targets)
        {
            if (target.Codename == PrimaryBone)
                return target.Frame;
        }

        return null;
    }

    private static float ClampRadius(float radius) => Math.Clamp(radius, BrushInfluence.RadiusMin, BrushInfluence.RadiusMax);
}
