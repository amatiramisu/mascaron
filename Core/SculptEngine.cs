using System.Numerics;

namespace Mascaron.Core;

public enum FalloffCurve
{
    Linear,
    Smooth,
    Sharp,
}

public class SculptEngine
{
    private readonly TopologyGraph topology;
    private readonly BoneTransformState state;

    public float FalloffFactor { get; set; } = 0f;
    public bool MirrorEnabled { get; set; } = true;
    public bool LinkEyesEnabled { get; set; } = true;
    public bool TopologyEnabled { get; set; }
    public FalloffCurve FalloffCurve { get; set; } = FalloffCurve.Smooth;
    public float BrushRadius { get; set; } = 80f;
    public bool BrushEnabled { get; set; } = true;

    public const float BrushRadiusMin = 15f;
    public const float BrushRadiusMax = 400f;
    public const float BrushRadiusFactor = 0.12f;

    public SculptEngine(TopologyGraph topology, BoneTransformState state)
    {
        this.topology = topology;
        this.state = state;
    }

    public float ComputeFalloff(float normalizedDistance)
    {
        return ComputeCurveFalloff(normalizedDistance) * FalloffFactor;
    }

    public float ComputeCurveFalloff(float normalizedDistance)
    {
        var t = Math.Clamp(normalizedDistance, 0f, 1f);
        return FalloffCurve switch
        {
            FalloffCurve.Linear => 1f - t,
            FalloffCurve.Smooth => (MathF.Cos(t * MathF.PI) + 1f) * 0.5f,
            FalloffCurve.Sharp => MathF.Pow(1f - t, 3f),
            _ => 1f - t,
        };
    }

    public SculptStroke CreateStroke(
        string centerBone,
        SculptStrokeKind kind,
        IReadOnlyList<(string Codename, float Strength)> affected)
    {
        var affectedCodes = affected.Select(x => x.Codename).ToHashSet();
        var targets = new Dictionary<string, SculptStrokeTarget>();

        void AddTarget(string codename, float strength, bool isPrimary, bool isMirrored)
        {
            var baseline = state.Get(codename);
            if (targets.TryGetValue(codename, out var existing))
            {
                targets[codename] = existing with
                {
                    Strength = MathF.Max(existing.Strength, strength),
                    IsPrimary = existing.IsPrimary || isPrimary,
                };
                return;
            }

            targets[codename] = new SculptStrokeTarget(codename, strength, isPrimary, isMirrored, baseline);
        }

        foreach (var (code, strength) in affected)
        {
            var isPrimary = code == centerBone;
            AddTarget(code, strength, isPrimary, false);

            var linked = topology.GetLinkedBone(code);
            if (LinkEyesEnabled && linked != null && (isPrimary || !affectedCodes.Contains(linked)))
                AddTarget(linked, strength, isPrimary, false);

            if (MirrorEnabled)
            {
                if (LinkEyesEnabled && linked != null)
                    continue;

                var mirror = topology.GetMirror(code);
                if (mirror != null && !affectedCodes.Contains(mirror))
                    AddTarget(mirror, strength, isPrimary, true);
            }
        }

        return new SculptStroke(centerBone, kind, targets.Values, FalloffFactor, BrushEnabled);
    }

    public List<(string Codename, float Strength)> ComputeAffectedBones(
        string centerBone,
        Func<string, Vector2?> getScreenPos,
        Vector2 cursorPos)
    {
        var result = new List<(string, float)>();
        var centerPos = getScreenPos(centerBone);
        if (centerPos == null)
            return result;

        if (!BrushEnabled)
        {
            result.Add((centerBone, 1f));
            return result;
        }

        var centerRegion = FaceBoneRegistry.GetRegion(centerBone);
        foreach (var bone in FaceBoneRegistry.Bones)
        {
            if (!bone.IsSculptable)
                continue;

            var pos = getScreenPos(bone.Codename);
            if (pos == null)
                continue;

            var dist = Vector2.Distance(cursorPos, pos.Value);
            if (dist > BrushRadius)
                continue;

            if (TopologyEnabled && centerRegion != FaceBoneRegistry.GetRegion(bone.Codename))
                continue;

            var strength = bone.Codename == centerBone ? 1f : ComputeFalloff(dist / BrushRadius);
            if (strength > 0.001f)
                result.Add((bone.Codename, strength));
        }

        return result;
    }

    public List<(string Codename, float Strength)> ComputeStrokeAffectedBones(
        string centerBone,
        Func<string, Vector2?> getScreenPos,
        Vector2 cursorPos)
    {
        var result = new List<(string, float)>();
        var centerPos = getScreenPos(centerBone);
        if (centerPos == null)
            return result;

        if (!BrushEnabled)
        {
            result.Add((centerBone, 1f));
            return result;
        }

        var centerRegion = FaceBoneRegistry.GetRegion(centerBone);
        foreach (var bone in FaceBoneRegistry.Bones)
        {
            if (!bone.IsSculptable)
                continue;

            var pos = getScreenPos(bone.Codename);
            if (pos == null)
                continue;

            var dist = Vector2.Distance(cursorPos, pos.Value);
            if (dist > BrushRadius)
                continue;

            if (TopologyEnabled && centerRegion != FaceBoneRegistry.GetRegion(bone.Codename))
                continue;

            var strength = bone.Codename == centerBone ? 1f : ComputeCurveFalloff(dist / BrushRadius);
            if (strength > 0.001f)
                result.Add((bone.Codename, strength));
        }

        return result;
    }
}
