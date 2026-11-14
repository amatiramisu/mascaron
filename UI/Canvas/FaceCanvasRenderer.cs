using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Mascaron.Core;
using Mascaron.Preview;
using Mascaron.Visualization;

namespace Mascaron.UI.Canvas;

/// <summary>
/// Everything the renderer needs to know about the canvas for one frame.
/// </summary>
public readonly record struct FaceCanvasFrame(
    CanvasView View,
    BoneLayout Layout,
    FaceTemplate Template,
    string? HoveredBone,
    string? DraggedBone,
    IReadOnlyDictionary<string, float> BrushStrengths,
    bool ShowBrushRing,
    Vector2 Mouse,
    bool SkeletonAvailable);

/// <summary>
/// Draws the face image, bone handles, brush ring and canvas overlays.
/// </summary>
public sealed class FaceCanvasRenderer
{
    private const float HandleRadius = 5f;
    private const float ImageAlpha = 0.25f;
    private const int BrushGradientBands = 12;

    private static readonly Vector4 DraggedColor = new(1f, 0.4f, 0.2f, 1f);
    private static readonly Vector4 HoveredColor = new(1f, 0.85f, 0.4f, 1f);
    private static readonly Vector4 BrushColor = new(1f, 0.72f, 0.28f, 1f);
    private static readonly Vector4 FocusColor = new(0.35f, 0.95f, 1f, 1f);
    private static readonly Vector4 IdleColor = new(0.6f, 0.6f, 0.6f, 0.5f);
    private static readonly Vector4 HandoffColor = new(1f, 0.42f, 0.34f, 0.95f);

    private readonly SculptSession session;
    private readonly EditorState editor;
    private readonly StatusLine status;
    private readonly PreviewReconciler previewReconciler;
    private readonly ITextureProvider textureProvider;
    private readonly string assetsPath;

    private FaceTemplate? loadedTemplate;
    private ISharedImmediateTexture? backgroundTexture;

    public FaceCanvasRenderer(
        SculptSession session,
        EditorState editor,
        StatusLine status,
        PreviewReconciler previewReconciler,
        ITextureProvider textureProvider,
        string assetsPath)
    {
        this.session = session;
        this.editor = editor;
        this.status = status;
        this.previewReconciler = previewReconciler;
        this.textureProvider = textureProvider;
        this.assetsPath = assetsPath;
    }

    public void Draw(in FaceCanvasFrame frame)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(frame.View.RegionMin, frame.View.RegionMax, true);

        DrawFaceImage(drawList, frame);
        DrawMirrorLink(drawList, frame);
        DrawHandles(drawList, frame);
        if (frame.ShowBrushRing)
            DrawBrushRing(drawList, frame);
        DrawBoneReadout(drawList, frame);
        DrawTopOverlay(drawList, frame);

        drawList.PopClipRect();
    }

    private void DrawFaceImage(ImDrawListPtr drawList, in FaceCanvasFrame frame)
    {
        if (loadedTemplate != frame.Template)
        {
            var filePath = Path.Combine(assetsPath, RaceTemplates.GetBackgroundFileName(frame.Template));
            backgroundTexture = File.Exists(filePath) ? textureProvider.GetFromFile(filePath) : null;
            loadedTemplate = frame.Template;
        }

        var wrap = backgroundTexture?.GetWrapOrEmpty();
        if (wrap == null || wrap.Handle == nint.Zero)
            return;

        drawList.AddImage(
            wrap.Handle,
            frame.View.FaceToScreen(Vector2.Zero),
            frame.View.FaceToScreen(Vector2.One),
            Vector2.Zero,
            Vector2.One,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, ImageAlpha)));
    }

    private void DrawMirrorLink(ImDrawListPtr drawList, in FaceCanvasFrame frame)
    {
        var activeBone = frame.DraggedBone ?? frame.HoveredBone;
        if (activeBone == null || !session.Engine.MirrorEnabled ||
            session.Engine.Topology.GetMirror(activeBone) is not { } mirror ||
            !frame.Layout.Positions.TryGetValue(activeBone, out var from) ||
            !frame.Layout.Positions.TryGetValue(mirror, out var to))
            return;

        var color = ImGui.GetColorU32(new Vector4(0.3f, 0.6f, 0.9f, 0.3f));
        drawList.AddLine(frame.View.FaceToScreen(from), frame.View.FaceToScreen(to), color, ImGuiHelpers.GlobalScale);
    }

    private void DrawHandles(ImDrawListPtr drawList, in FaceCanvasFrame frame)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var focusedStroke = editor.GetFocusedStroke(session.History);
        var focusedTargets = focusedStroke?.Targets.ToDictionary(target => target.Codename);

        foreach (var (codename, position) in frame.Layout.Positions)
        {
            var screen = frame.View.FaceToScreen(position);
            var depthOffset = Math.Clamp(frame.Layout.GetDepth(codename) * frame.View.LogicalSize * 0.5f, -4f, 4f) * scale;
            var modified = session.State.Get(codename).IsModified;
            var hasBrush = frame.BrushStrengths.TryGetValue(codename, out var strength);

            Vector4 color;
            float radius;
            if (codename == frame.DraggedBone)
                (color, radius) = (DraggedColor, (HandleRadius + 2f) * scale + depthOffset);
            else if (codename == frame.HoveredBone)
                (color, radius) = (HoveredColor, (HandleRadius + 1f) * scale + depthOffset);
            else if (hasBrush)
                (color, radius) = (BrushColor with { W = 0.3f + 0.5f * strength }, (HandleRadius + strength) * scale + depthOffset);
            else if (modified)
                (color, radius) = (new Vector4(0.4f, 0.9f, 0.6f, 0.9f), HandleRadius * scale + depthOffset);
            else
                (color, radius) = (IdleColor, (HandleRadius - 1f) * scale);

            drawList.AddCircleFilled(screen, radius, ImGui.GetColorU32(color));

            if (hasBrush)
            {
                var ringColor = ImGui.GetColorU32(BrushColor with { W = 0.35f + 0.45f * strength });
                drawList.AddCircle(screen, radius + (3f + strength * 2f) * scale, ringColor, 24, (1f + strength) * scale);
            }

            if (focusedStroke != null && focusedTargets!.TryGetValue(codename, out var target))
                DrawFocusRing(drawList, screen, radius, focusedStroke.GetTargetStrength(target), target.IsPrimary);
        }
    }

    private static void DrawFocusRing(ImDrawListPtr drawList, Vector2 screen, float radius, float strength, bool primary)
    {
        if (strength <= 0f)
            return;

        var scale = ImGuiHelpers.GlobalScale;
        var color = ImGui.GetColorU32(FocusColor with { W = 0.35f + 0.45f * strength });
        var ringRadius = radius + (4f + strength * 2f) * scale;
        drawList.AddCircle(screen, ringRadius, color, 24, (primary ? 2.5f : 1.5f) * scale);
        if (primary)
            drawList.AddCircle(screen, ringRadius + 3f * scale, color, 24, scale);
    }

    private void DrawBrushRing(ImDrawListPtr drawList, in FaceCanvasFrame frame)
    {
        var center = frame.Mouse;
        var coreColor = ImGui.GetColorU32(BrushColor with { W = 0.5f });
        drawList.AddCircleFilled(center, 3f * ImGuiHelpers.GlobalScale, coreColor, 12);

        if (session.Engine.IsDirectMode)
            return;

        var radius = session.Engine.BrushRadius * frame.View.LogicalSize;
        var segments = Math.Clamp((int)(radius * 0.4f), 24, 128);
        for (var band = BrushGradientBands; band >= 1; band--)
        {
            var t = (float)band / BrushGradientBands;
            var alpha = 0.02f + BrushInfluence.Falloff(t) * 0.035f;
            drawList.AddCircleFilled(center, radius * t, ImGui.GetColorU32(BrushColor with { W = alpha }), segments);
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(BrushColor with { W = 0.4f }), segments, ImGuiHelpers.GlobalScale);
    }

    private void DrawBoneReadout(ImDrawListPtr drawList, in FaceCanvasFrame frame)
    {
        var activeBone = frame.DraggedBone ?? frame.HoveredBone;
        if (activeBone == null || FaceBoneRegistry.GetByCodename(activeBone) is not { } bone ||
            !frame.Layout.Positions.TryGetValue(activeBone, out var position))
            return;

        var scale = ImGuiHelpers.GlobalScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var cursor = frame.View.FaceToScreen(position) + new Vector2(12f, -8f) * scale;
        var textColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f));
        var dimColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f));

        drawList.AddText(cursor, textColor, bone.DisplayName);
        cursor.Y += lineHeight;

        var transform = session.State.Get(activeBone);
        if (transform.Translation != Vector3.Zero)
            AddLine(ref cursor, dimColor, $"T: {transform.Translation.X:+0.0000;-0.0000} {transform.Translation.Y:+0.0000;-0.0000} {transform.Translation.Z:+0.0000;-0.0000}");
        if (transform.Rotation != Vector3.Zero)
            AddLine(ref cursor, dimColor, $"R: {transform.Rotation.X:+0.0;-0.0} {transform.Rotation.Y:+0.0;-0.0} {transform.Rotation.Z:+0.0;-0.0}");
        if (transform.Scaling != Vector3.One)
            AddLine(ref cursor, dimColor, $"S: {transform.Scaling.X:0.000} {transform.Scaling.Y:0.000} {transform.Scaling.Z:0.000}");

        var focusedStroke = editor.GetFocusedStroke(session.History);
        var focusedTarget = focusedStroke?.Targets.FirstOrDefault(target => target.Codename == activeBone);
        if (focusedStroke != null && focusedTarget is { Codename: not null } target)
            AddLine(ref cursor, ImGui.GetColorU32(FocusColor with { W = 0.85f }), $"Stroke: {focusedStroke.GetTargetStrength(target) * 100f:0}%");

        void AddLine(ref Vector2 at, uint color, string text)
        {
            drawList.AddText(at, color, text);
            at.Y += lineHeight;
        }
    }

    /// <summary>
    /// Top-left of the canvas: the control hint line, then transient status and handoff messages.
    /// </summary>
    private void DrawTopOverlay(ImDrawListPtr drawList, in FaceCanvasFrame frame)
    {
        var padding = new Vector2(8f, 6f) * ImGuiHelpers.GlobalScale;
        var cursor = frame.View.RegionMin + padding;
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();

        cursor.Y += ControlHints.Draw(drawList, cursor, frame.View.RegionSize.X - padding.X * 2f, editor.Mode);

        if (status.Alpha > 0f)
        {
            drawList.AddText(cursor, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.8f * status.Alpha)), status.Message);
            cursor.Y += lineHeight;
        }

        if (!frame.SkeletonAvailable)
        {
            drawList.AddText(cursor, ImGui.GetColorU32(HandoffColor), "Waiting for the character skeleton.");
            cursor.Y += lineHeight;
        }

        var ownedBoneCount = previewReconciler.MascaronOwnedBoneCount;
        if (ownedBoneCount > 0)
        {
            var message = ownedBoneCount == 1
                ? "One bone still answers to Mascaron."
                : $"{ownedBoneCount} bones still answer to Mascaron.";
            drawList.AddText(cursor, ImGui.GetColorU32(HandoffColor), message);
        }
    }
}
