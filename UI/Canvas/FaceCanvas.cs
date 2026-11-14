using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Mascaron.Core;
using Mascaron.UI.Input;
using Mascaron.Visualization;

namespace Mascaron.UI.Canvas;

/// <summary>
/// The sculpting surface: owns the view, turns mouse input into view changes and strokes,
/// and hands a frame description to <see cref="FaceCanvasRenderer"/>.
/// </summary>
public sealed class FaceCanvas
{
    private const float HitTestRadius = 8f;

    private readonly SculptSession session;
    private readonly EditorState editor;
    private readonly Configuration configuration;
    private readonly StatusLine status;
    private readonly FaceCanvasRenderer renderer;
    private readonly CanvasView view = new();
    private readonly BoneLayout layout = new();
    private readonly Dictionary<string, float> brushStrengths = new();

    private FaceTemplate template = FaceTemplate.Standard;
    private string? hoveredBone;
    private string? draggedBone;
    private Vector2 lastDragPosition;

    public FaceCanvas(
        SculptSession session,
        EditorState editor,
        Configuration configuration,
        StatusLine status,
        FaceCanvasRenderer renderer)
    {
        this.session = session;
        this.editor = editor;
        this.configuration = configuration;
        this.status = status;
        this.renderer = renderer;
    }

    public FaceTemplate Template => template;
    public bool IsDragging => draggedBone != null;

    public void SetTemplate(FaceTemplate newTemplate)
    {
        template = newTemplate;
        CancelDrag();
    }

    public void CancelDrag()
    {
        draggedBone = null;
        session.EndStroke();
    }

    /// <summary>Draws the canvas into the given screen rectangle and processes its input.</summary>
    public void Draw(Vector2 regionMin, Vector2 regionSize)
    {
        view.Layout(regionMin, regionSize);
        var frames = session.Frames.LatestFrames;
        layout.Rebuild(template, session.State, frames);

        ImGui.SetCursorScreenPos(regionMin);
        ImGui.InvisibleButton("##canvas", view.RegionSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight);
        var hovered = ImGui.IsItemHovered();
        var mouse = ImGui.GetIO().MousePos;

        HandleViewInput(hovered, mouse);
        HandleSculptInput(hovered, mouse, frames);
        UpdateBrushStrengths(mouse);

        renderer.Draw(new FaceCanvasFrame(
            view,
            layout,
            template,
            hoveredBone,
            draggedBone,
            brushStrengths,
            hovered && draggedBone == null,
            mouse,
            frames != null));
    }

    private void HandleViewInput(bool hovered, Vector2 mouse)
    {
        var io = ImGui.GetIO();
        if (hovered && io.MouseWheel != 0f)
        {
            if (io.KeyCtrl)
            {
                view.ZoomAt(mouse, io.MouseWheel);
            }
            else
            {
                session.Engine.StepBrushRadius(io.MouseWheel);
                configuration.BrushRadius = session.Engine.BrushRadius;
                configuration.RequestSave();
            }
        }

        if ((hovered || ImGui.IsItemActive()) && ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            view.PanBy(io.MouseDelta);

        if (hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Right))
            view.Reset();
    }

    private void HandleSculptInput(bool hovered, Vector2 mouse, BoneFrameSnapshot? frames)
    {
        if (draggedBone == null && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            TryBeginStroke(mouse, frames);

        if (draggedBone != null && session.ActiveStroke is { } stroke && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var delta = mouse - lastDragPosition;
            if (delta != Vector2.Zero)
            {
                var multiplier = DragSensitivity.GetMultiplier(configuration.Sensitivity);
                DragSensitivity.AddDrag(stroke, delta, ImGui.GetIO().KeyShift, multiplier);
                session.ApplyActiveStroke();
                lastDragPosition = mouse;
            }
        }

        if (draggedBone != null && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
            CancelDrag();

        hoveredBone = draggedBone ?? (hovered ? HitTest(mouse) : null);
    }

    private void TryBeginStroke(Vector2 mouse, BoneFrameSnapshot? frames)
    {
        var hit = HitTest(mouse);
        if (hit == null)
            return;

        if (frames == null)
        {
            status.Set("Character skeleton not loaded yet.");
            return;
        }

        var stroke = session.Engine.CreateStroke(hit, editor.Mode, view.ScreenToFace(mouse), layout.Positions, frames);
        if (stroke == null)
        {
            status.Set("This bone is missing from the current character.");
            return;
        }

        session.BeginStroke(stroke);
        draggedBone = hit;
        lastDragPosition = mouse;
    }

    private void UpdateBrushStrengths(Vector2 mouse)
    {
        brushStrengths.Clear();
        if (session.ActiveStroke is { } stroke)
        {
            foreach (var target in stroke.Targets)
            {
                var strength = stroke.GetTargetStrength(target);
                if (strength > 0f)
                    brushStrengths[target.Codename] = strength;
            }

            return;
        }

        if (hoveredBone == null)
            return;

        foreach (var influence in session.Engine.ComputeInfluence(hoveredBone, view.ScreenToFace(mouse), layout.Positions))
        {
            var (strength, _) = influence.Evaluate(session.Engine.BrushRadius);
            if (strength > 0f)
                brushStrengths[influence.Codename] = strength;
        }
    }

    private string? HitTest(Vector2 mouse)
    {
        var closestDistance = HitTestRadius * ImGuiHelpers.GlobalScale;
        string? closest = null;

        foreach (var (codename, position) in layout.Positions)
        {
            var distance = Vector2.Distance(mouse, view.FaceToScreen(position));
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = codename;
            }
        }

        return closest;
    }
}
