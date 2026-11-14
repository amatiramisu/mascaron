namespace Mascaron.Core;

public enum BrushRole
{
    /// <summary>Bone inside the brush ring, weighted by distance.</summary>
    Falloff,

    /// <summary>The grabbed bone, or its linked partner when Link Eyes is on.</summary>
    Primary,

    /// <summary>Mirror partner of the grabbed bone when Mirror is on.</summary>
    MirrorPrimary,
}

/// <summary>
/// How one bone relates to a brush placement. Distances are in face-normalized canvas units
/// (1 = full template width); <see cref="MirrorDistance"/> is the distance from the brush to the
/// bone's mirror partner and is infinite when the bone receives no mirrored contribution.
/// </summary>
public readonly record struct BrushInfluence(string Codename, float DirectDistance, float MirrorDistance, BrushRole Role)
{
    public const float RadiusMin = 0.02f;
    public const float RadiusMax = 0.7f;

    /// <summary>
    /// Resolves this bone's strength for the given brush radius and whether it receives the mirrored delta.
    /// A radius at <see cref="RadiusMin"/> is direct mode: only primary bones move.
    /// </summary>
    public (float Strength, bool Mirrored) Evaluate(float radius)
    {
        switch (Role)
        {
            case BrushRole.Primary:
                return (1f, false);
            case BrushRole.MirrorPrimary:
                return (1f, true);
        }

        if (radius <= RadiusMin)
            return (0f, false);

        var direct = Falloff(DirectDistance / radius);
        var mirrored = Falloff(MirrorDistance / radius);
        return mirrored > direct ? (mirrored, true) : (direct, false);
    }

    /// <summary>
    /// Cosine falloff from 1 at the brush center to 0 at the ring.
    /// </summary>
    public static float Falloff(float normalizedDistance)
    {
        if (!(normalizedDistance < 1f))
            return 0f;

        var t = MathF.Max(normalizedDistance, 0f);
        return (MathF.Cos(t * MathF.PI) + 1f) * 0.5f;
    }
}
