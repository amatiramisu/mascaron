using System.Numerics;

namespace Mascaron.UI.Canvas;

/// <summary>
/// Maps face-normalized coordinates (0..1 across the template image) to screen pixels.
/// The face is fit into the canvas and centered; pan is stored in face units so resizing
/// keeps the face anchored and scaled with the window.
/// </summary>
public sealed class CanvasView
{
    private const float ZoomMin = 0.5f;
    private const float ZoomMax = 6f;
    private const float ZoomStep = 0.12f;

    private Vector2 pan;

    public float Zoom { get; private set; } = 1f;
    public Vector2 RegionMin { get; private set; }
    public Vector2 RegionSize { get; private set; }

    /// <summary>Screen pixels per face unit.</summary>
    public float LogicalSize { get; private set; }

    public Vector2 RegionMax => RegionMin + RegionSize;

    public void Layout(Vector2 regionMin, Vector2 regionSize)
    {
        RegionMin = regionMin;
        RegionSize = Vector2.Max(regionSize, Vector2.One);
        LogicalSize = MathF.Min(RegionSize.X, RegionSize.Y) * Zoom;
    }

    public Vector2 FaceToScreen(Vector2 face) => GetFaceOrigin() + face * LogicalSize;

    public Vector2 ScreenToFace(Vector2 screen) => (screen - GetFaceOrigin()) / LogicalSize;

    /// <summary>Zooms around <paramref name="anchor"/>, keeping the face point under it fixed.</summary>
    public void ZoomAt(Vector2 anchor, float wheelSteps)
    {
        var anchoredFacePoint = ScreenToFace(anchor);
        Zoom = Math.Clamp(Zoom * MathF.Pow(1f + ZoomStep, wheelSteps), ZoomMin, ZoomMax);
        LogicalSize = MathF.Min(RegionSize.X, RegionSize.Y) * Zoom;

        var desiredOrigin = anchor - anchoredFacePoint * LogicalSize;
        pan = (desiredOrigin - GetCenteredOrigin()) / LogicalSize;
    }

    public void PanBy(Vector2 pixels)
    {
        pan += pixels / LogicalSize;
    }

    public void Reset()
    {
        Zoom = 1f;
        pan = Vector2.Zero;
        LogicalSize = MathF.Min(RegionSize.X, RegionSize.Y);
    }

    private Vector2 GetCenteredOrigin() => RegionMin + (RegionSize - new Vector2(LogicalSize)) * 0.5f;

    private Vector2 GetFaceOrigin() => GetCenteredOrigin() + pan * LogicalSize;
}
