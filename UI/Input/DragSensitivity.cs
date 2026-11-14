using System.Numerics;
using Mascaron.Core;

namespace Mascaron.UI.Input;

/// <summary>
/// Converts mouse drag in screen pixels into model-space stroke deltas.
/// Base steps are fixed per pixel (independent of window size and zoom) and calibrated against
/// Ktisis' per-race expression library: a median complete facial expression moves a face bone by
/// about 0.0015 units, 3.5 degrees or 5 percent scale, which these steps reach in roughly 75 to 120 pixels.
/// </summary>
public static class DragSensitivity
{
    public const float MoveUnitsPerPixel = 0.00002f;
    public const float RotateDegreesPerPixel = 0.03f;
    public const float ScalePerPixel = 0.0005f;

    private static readonly Vector3 ModelScreenRight = -Vector3.UnitX;
    private static readonly Vector3 ModelScreenDown = -Vector3.UnitY;
    private static readonly Vector3 ModelForward = Vector3.UnitZ;

    /// <summary>
    /// Maps the 0..1 slider exponentially to a 0.1x..10x multiplier, 0.5 being the calibrated base.
    /// </summary>
    public static float GetMultiplier(float slider)
    {
        return MathF.Pow(10f, (Math.Clamp(slider, 0f, 1f) - 0.5f) * 2f);
    }

    /// <summary>
    /// Feeds one frame of drag into the stroke. Model space: X = character left (screen left on the canvas),
    /// Y = up, Z = toward the viewer. <paramref name="alternateAxis"/> is the Shift modifier.
    /// </summary>
    public static void AddDrag(SculptStroke stroke, Vector2 pixels, bool alternateAxis, float multiplier)
    {
        switch (stroke.Kind)
        {
            case SculptStrokeKind.Move:
                var move = alternateAxis
                    ? new Vector3(0f, 0f, -pixels.Y)
                    : new Vector3(-pixels.X, -pixels.Y, 0f);
                stroke.AddDelta(move * (MoveUnitsPerPixel * multiplier));
                break;

            case SculptStrokeKind.Rotate:
                if (stroke.PrimaryFrame is { } frame)
                    stroke.AddDelta(GetRotation(stroke.PrimaryBone, frame, pixels, alternateAxis) * (RotateDegreesPerPixel * multiplier));
                break;

            case SculptStrokeKind.Scale:
                var scale = alternateAxis
                    ? new Vector3(0f, 0f, -pixels.Y)
                    : new Vector3(pixels.X, pixels.Y, (pixels.X + pixels.Y) * 0.5f);
                stroke.AddDelta(scale * (ScalePerPixel * multiplier));
                break;
        }
    }

    /// <summary>
    /// Customize+ rotation for a drag on the grabbed bone, named after Customize+'s editor columns. Every channel
    /// moves the bone in the mouse direction, like Move and Scale: vertical drag drives Pitch (Y) and lowers the
    /// grabbed bone's outer end when dragging down; horizontal drag drives Roll (X) and swings its front toward the
    /// mouse; Shift + vertical drag drives Yaw (Z) and tips its front toward the mouse. Each channel's sign comes
    /// from the bone's frame, so the same mouse motion looks the same on both sides of the face.
    /// </summary>
    private static Vector3 GetRotation(string primaryBone, BoneFrame frame, Vector2 pixels, bool alternateAxis)
    {
        if (alternateAxis)
            return new Vector3(0f, 0f, GetChannelSign(frame, Vector3.UnitZ, ModelForward, ModelScreenDown) * pixels.Y);

        // Center bones tilt like left-side ones.
        var outerEnd = FaceBoneRegistry.GetSide(primaryBone) == FaceSide.Right ? -Vector3.UnitX : Vector3.UnitX;
        return new Vector3(
            GetChannelSign(frame, Vector3.UnitY, ModelForward, ModelScreenRight) * pixels.X,
            GetChannelSign(frame, Vector3.UnitX, outerEnd, ModelScreenDown) * pixels.Y,
            0f);
    }

    /// <summary>
    /// +1 when a positive rotation about the bone-local <paramref name="localAxis"/> moves the model-space point
    /// <paramref name="probe"/> (relative to the pivot) toward the model-space <paramref name="direction"/>, else -1.
    /// </summary>
    private static float GetChannelSign(BoneFrame frame, Vector3 localAxis, Vector3 probe, Vector3 direction)
    {
        var axis = Vector3.Transform(localAxis, frame.BoneToModel);
        return Vector3.Dot(Vector3.Cross(axis, probe), direction) >= 0f ? 1f : -1f;
    }
}
