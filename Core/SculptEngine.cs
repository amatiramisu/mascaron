using System.Numerics;

namespace Mascaron.Core;

/// <summary>
/// Brush settings and stroke construction: decides which bones a gesture touches and how strongly.
/// </summary>
public sealed class SculptEngine
{
    private const float RadiusStepFactor = 0.12f;

    private readonly TopologyGraph topology;
    private readonly BoneTransformState state;
    private float brushRadius = 0.13f;

    public SculptEngine(TopologyGraph topology, BoneTransformState state)
    {
        this.topology = topology;
        this.state = state;
    }

    public TopologyGraph Topology => topology;
    public bool MirrorEnabled { get; set; } = true;
    public bool LinkEyesEnabled { get; set; } = true;
    public bool RegionLockEnabled { get; set; }

    /// <summary>Brush radius in face-normalized canvas units.</summary>
    public float BrushRadius
    {
        get => brushRadius;
        set => brushRadius = Math.Clamp(value, BrushInfluence.RadiusMin, BrushInfluence.RadiusMax);
    }

    public bool IsDirectMode => BrushRadius <= BrushInfluence.RadiusMin;

    public void StepBrushRadius(float wheelSteps)
    {
        BrushRadius *= MathF.Pow(1f + RadiusStepFactor, wheelSteps);
    }

    /// <summary>
    /// Computes how each visible bone relates to a brush placed at <paramref name="brushCenter"/>
    /// while <paramref name="primaryBone"/> is grabbed. Positions are face-normalized.
    /// </summary>
    public List<BrushInfluence> ComputeInfluence(
        string primaryBone,
        Vector2 brushCenter,
        IReadOnlyDictionary<string, Vector2> bonePositions)
    {
        var result = new List<BrushInfluence>();
        if (!bonePositions.ContainsKey(primaryBone))
            return result;

        var linkedPrimary = LinkEyesEnabled ? topology.GetLinkedBone(primaryBone) : null;
        var mirrorPrimary = MirrorEnabled && linkedPrimary == null ? topology.GetMirror(primaryBone) : null;
        var primaryRegion = FaceBoneRegistry.GetRegion(primaryBone);

        foreach (var (codename, position) in bonePositions)
        {
            if (RegionLockEnabled && FaceBoneRegistry.GetRegion(codename) != primaryRegion)
                continue;

            var role = codename == primaryBone || codename == linkedPrimary
                ? BrushRole.Primary
                : codename == mirrorPrimary ? BrushRole.MirrorPrimary : BrushRole.Falloff;

            var directDistance = Vector2.Distance(brushCenter, position);
            var mirrorDistance = float.PositiveInfinity;

            var linkedPartner = LinkEyesEnabled ? topology.GetLinkedBone(codename) : null;
            if (linkedPartner != null)
            {
                if (bonePositions.TryGetValue(linkedPartner, out var partnerPosition))
                    directDistance = MathF.Min(directDistance, Vector2.Distance(brushCenter, partnerPosition));
            }
            else if (MirrorEnabled && topology.GetMirror(codename) is { } mirror &&
                     bonePositions.TryGetValue(mirror, out var mirrorPosition))
            {
                mirrorDistance = Vector2.Distance(brushCenter, mirrorPosition);
            }

            if (role == BrushRole.Falloff && MathF.Min(directDistance, mirrorDistance) >= BrushInfluence.RadiusMax)
                continue;

            result.Add(new BrushInfluence(codename, directDistance, mirrorDistance, role));
        }

        return result;
    }

    /// <summary>
    /// Starts a stroke on <paramref name="primaryBone"/>. Bones missing from the live skeleton are skipped;
    /// returns null when the grabbed bone itself has no frame.
    /// </summary>
    public SculptStroke? CreateStroke(
        string primaryBone,
        SculptStrokeKind kind,
        Vector2 brushCenter,
        IReadOnlyDictionary<string, Vector2> bonePositions,
        BoneFrameSnapshot frames)
    {
        if (!frames.TryGet(primaryBone, out _))
            return null;

        var targets = new List<SculptStrokeTarget>();
        foreach (var influence in ComputeInfluence(primaryBone, brushCenter, bonePositions))
        {
            if (frames.TryGet(influence.Codename, out var frame))
                targets.Add(new SculptStrokeTarget(influence, state.Get(influence.Codename), frame));
        }

        return new SculptStroke(primaryBone, kind, BrushRadius, targets);
    }
}
