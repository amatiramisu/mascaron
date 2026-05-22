using System.Numerics;

namespace Mascaron.Core;

public enum SculptStrokeKind
{
    Move,
    Rotate,
    Scale,
}

public readonly record struct SculptStrokeTarget(
    string Codename,
    float Strength,
    float NormalizedDistance,
    bool IsPrimary,
    bool IsMirrored,
    BoneTransform Baseline);

public class SculptStroke
{
    private readonly List<SculptStrokeTarget> targets = [];

    public SculptStroke(string primaryBone, SculptStrokeKind kind, IEnumerable<SculptStrokeTarget> targets, float influence, bool brushEnabled, FalloffCurve curve)
    {
        PrimaryBone = primaryBone;
        Kind = kind;
        this.targets.AddRange(targets);
        Influence = Math.Clamp(influence, 0f, 1f);
        BrushEnabled = brushEnabled;
        Curve = curve;
    }

    public string PrimaryBone { get; }
    public SculptStrokeKind Kind { get; }
    public IReadOnlyList<SculptStrokeTarget> Targets => targets;
    public float Influence { get; private set; }
    public bool BrushEnabled { get; }
    public FalloffCurve Curve { get; private set; }
    public Vector3 Delta { get; private set; }

    public void AddDelta(Vector3 delta)
    {
        Delta += delta;
    }

    public void SetInfluence(float influence)
    {
        Influence = Math.Clamp(influence, 0f, 1f);
    }

    public void SetCurve(FalloffCurve curve)
    {
        Curve = curve;
    }

    public float GetTargetStrength(SculptStrokeTarget target)
    {
        if (target.IsPrimary)
            return 1f;

        if (BrushEnabled)
            return SculptEngine.ComputeCurveFalloff(Curve, target.NormalizedDistance) * Influence;

        return target.Strength * Influence;
    }

    public void ApplyTo(BoneTransformState state)
    {
        foreach (var target in targets)
        {
            var strength = GetTargetStrength(target);
            var transform = target.Baseline;

            switch (Kind)
            {
                case SculptStrokeKind.Move:
                    transform.Translation += GetMoveDelta(target) * strength;
                    break;
                case SculptStrokeKind.Rotate:
                    transform.Rotation += GetRotationDelta(target) * strength;
                    break;
                case SculptStrokeKind.Scale:
                    transform.Scaling *= Vector3.One + Delta * strength;
                    break;
            }

            state.Set(target.Codename, transform);
        }
    }

    public void ApplyToCurrent(BoneTransformState state)
    {
        foreach (var target in targets)
        {
            var strength = GetTargetStrength(target);
            var transform = state.Get(target.Codename);

            switch (Kind)
            {
                case SculptStrokeKind.Move:
                    transform.Translation += GetMoveDelta(target) * strength;
                    break;
                case SculptStrokeKind.Rotate:
                    transform.Rotation += GetRotationDelta(target) * strength;
                    break;
                case SculptStrokeKind.Scale:
                    transform.Scaling *= Vector3.One + Delta * strength;
                    break;
            }

            state.Set(target.Codename, transform);
        }
    }

    private Vector3 GetMoveDelta(SculptStrokeTarget target)
    {
        return target.IsMirrored ? new Vector3(-Delta.X, Delta.Y, Delta.Z) : Delta;
    }

    private Vector3 GetRotationDelta(SculptStrokeTarget target)
    {
        return target.IsMirrored ? new Vector3(Delta.X, -Delta.Y, -Delta.Z) : Delta;
    }
}
