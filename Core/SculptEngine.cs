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
    public bool TopologyEnabled { get; set; }
    public FalloffCurve FalloffCurve { get; set; } = FalloffCurve.Smooth;
    public float BrushRadius { get; set; } = 80f;
    public bool BrushEnabled { get; set; } = true;

    public const float BrushRadiusMin = 15f;
    public const float BrushRadiusMax = 250f;
    public const float BrushRadiusFactor = 0.12f;

    public SculptEngine(TopologyGraph topology, BoneTransformState state)
    {
        this.topology = topology;
        this.state = state;
    }

    public float ComputeFalloff(float normalizedDistance)
    {
        var t = Math.Clamp(normalizedDistance, 0f, 1f);
        var curved = FalloffCurve switch
        {
            FalloffCurve.Linear => 1f - t,
            FalloffCurve.Smooth => (MathF.Cos(t * MathF.PI) + 1f) * 0.5f,
            FalloffCurve.Sharp => MathF.Pow(1f - t, 3f),
            _ => 1f - t,
        };
        return curved * (1f - FalloffFactor) + FalloffFactor;
    }

    public void ApplyDrag(string boneName, Vector3 delta, IReadOnlyList<(string Codename, float Strength)> affected)
    {
        foreach (var (code, strength) in affected)
            ApplyToBone(code, delta, strength);

        if (MirrorEnabled)
        {
            var mirrorDelta = new Vector3(-delta.X, delta.Y, delta.Z);
            foreach (var (code, strength) in affected)
            {
                var mirror = topology.GetMirror(code);
                if (mirror != null)
                    ApplyToBone(mirror, mirrorDelta, strength);
            }
        }
    }

    public void ApplyRotation(string boneName, Vector2 delta, IReadOnlyList<(string Codename, float Strength)> affected, bool yawAxis = false)
    {
        var rotDelta = yawAxis
            ? new Vector3(0, delta.X, 0)
            : new Vector3(delta.Y, 0, delta.X);
        foreach (var (code, strength) in affected)
            ApplyRotationToBone(code, rotDelta, strength);

        if (MirrorEnabled)
        {
            var mirrorDelta = yawAxis
                ? new Vector3(0, -rotDelta.Y, 0)
                : new Vector3(rotDelta.X, rotDelta.Y, -rotDelta.Z);
            foreach (var (code, strength) in affected)
            {
                var mirror = topology.GetMirror(code);
                if (mirror != null)
                    ApplyRotationToBone(mirror, mirrorDelta, strength);
            }
        }
    }

    public void ApplyScale(string boneName, Vector2 delta, IReadOnlyList<(string Codename, float Strength)> affected, bool depthAxis = false)
    {
        var baseDelta = depthAxis
            ? new Vector3(0, 0, -delta.Y)
            : new Vector3(delta.X, delta.Y, (delta.X + delta.Y) * 0.5f);
        foreach (var (code, strength) in affected)
        {
            var scaleFactor = Vector3.One + baseDelta * strength;
            ApplyScaleToBone(code, scaleFactor);
        }

        if (MirrorEnabled)
        {
            foreach (var (code, strength) in affected)
            {
                var mirror = topology.GetMirror(code);
                if (mirror != null)
                {
                    var scaleFactor = Vector3.One + baseDelta * strength;
                    ApplyScaleToBone(mirror, scaleFactor);
                }
            }
        }
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

        foreach (var bone in FaceBoneRegistry.Bones)
        {
            var pos = getScreenPos(bone.Codename);
            if (pos == null)
                continue;

            var dist = Vector2.Distance(cursorPos, pos.Value);
            if (dist > BrushRadius)
                continue;

            if (TopologyEnabled && !topology.IsReachable(centerBone, bone.Codename))
                continue;

            var strength = ComputeFalloff(dist / BrushRadius);
            if (strength > 0.001f)
                result.Add((bone.Codename, strength));
        }

        return result;
    }

    private void ApplyToBone(string boneName, Vector3 delta, float strength)
    {
        var current = state.Get(boneName);
        current.Translation += delta * strength;
        state.Set(boneName, current);
    }

    private void ApplyRotationToBone(string boneName, Vector3 delta, float strength)
    {
        var current = state.Get(boneName);
        current.Rotation += delta * strength;
        state.Set(boneName, current);
    }

    private void ApplyScaleToBone(string boneName, Vector3 scaleFactor)
    {
        var current = state.Get(boneName);
        current.Scaling *= scaleFactor;
        state.Set(boneName, current);
    }
}
