using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Mascaron.Core;

namespace Mascaron.UI;

public class HistoryWindow : Window
{
    private readonly SculptStrokeHistory strokeHistory;
    private readonly SculptEngine sculptEngine;
    private readonly Window anchor;
    private Vector2? expectedAttachedPosition;
    private Vector2? expectedAttachedSize;
    private bool attachedToAnchor;

    private const float Width = 340f;
    private const float Gap = 8f;

    public HistoryWindow(
        SculptStrokeHistory strokeHistory,
        SculptEngine sculptEngine,
        Window anchor)
        : base("Mascaron History###MascaronHistory", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        this.strokeHistory = strokeHistory;
        this.sculptEngine = sculptEngine;
        this.anchor = anchor;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(260, 220),
            MaximumSize = new Vector2(420, 1200),
        };
    }

    public void ToggleOpen()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
            attachedToAnchor = true;
    }

    public override void PreDraw()
    {
        if (attachedToAnchor && TryGetAttachedPlacement(out var position, out var size))
        {
            expectedAttachedPosition = position;
            expectedAttachedSize = size;
            Position = position;
            Size = size;
            ImGui.SetNextWindowPos(position, ImGuiCond.Always);
            ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        }
    }

    public override void OnClose()
    {
    }

    public override void Draw()
    {
        if (!strokeHistory.HasStrokes)
        {
            ImGui.TextDisabled("No strokes yet.");
            UpdateAttachmentState();
            return;
        }

        var rowClicked = false;
        ImGui.SetNextWindowContentSize(new Vector2(ImGui.GetContentRegionAvail().X, 0));
        ImGui.BeginChild("##StrokeHistory", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), true);
        for (var i = 0; i < strokeHistory.Strokes.Count; i++)
        {
            var stroke = strokeHistory.Strokes[i];
            var selected = i == strokeHistory.SelectedIndex;
            if (ImGui.Selectable(GetStrokeLabel(i, stroke), selected))
            {
                rowClicked = true;
                strokeHistory.Select(i);
                sculptEngine.FalloffFactor = stroke.Influence;
                if (stroke.BrushEnabled)
                    sculptEngine.FalloffCurve = stroke.Curve;
            }
        }

        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !rowClicked)
            strokeHistory.ClearSelection();

        ImGui.EndChild();

        if (ImGui.Button("Clear History"))
            strokeHistory.Clear();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Keep the current sculpted result and forget editable stroke history.");

        UpdateAttachmentState();
    }

    private bool TryGetAttachedPlacement(out Vector2 position, out Vector2 size)
    {
        if (anchor.Position is { } anchorPosition && anchor.Size is { } anchorSize)
        {
            position = anchorPosition + new Vector2(anchorSize.X + Gap, 0);
            size = new Vector2(Width, anchorSize.Y);
            return true;
        }

        position = Vector2.Zero;
        size = Vector2.Zero;
        return false;
    }

    private void UpdateAttachmentState()
    {
        if (!attachedToAnchor || expectedAttachedPosition == null || expectedAttachedSize == null)
            return;

        var moved = Vector2.DistanceSquared(ImGui.GetWindowPos(), expectedAttachedPosition.Value) > 4f;
        var resized = Vector2.DistanceSquared(ImGui.GetWindowSize(), expectedAttachedSize.Value) > 4f;
        if ((moved || resized) && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            attachedToAnchor = false;
    }

    private static string GetStrokeLabel(int index, SculptStroke stroke)
    {
        var bone = FaceBoneRegistry.GetByCodename(stroke.PrimaryBone);
        var name = bone?.DisplayName ?? stroke.PrimaryBone;
        var scope = stroke.BrushEnabled ? $"Brush {stroke.Curve}" : "Direct";
        return $"{index + 1:00}  {scope}  {stroke.Kind}  {name}  {stroke.Influence * 100f:0}%  ({FormatStrokeDelta(stroke)})";
    }

    private static string FormatStrokeDelta(SculptStroke stroke)
    {
        var d = stroke.Delta;
        return stroke.Kind switch
        {
            SculptStrokeKind.Move => $"T{d.X:+0.000;-0.000} {d.Y:+0.000;-0.000} {d.Z:+0.000;-0.000}",
            SculptStrokeKind.Rotate => $"R{d.X:+0.0;-0.0} {d.Y:+0.0;-0.0} {d.Z:+0.0;-0.0}",
            SculptStrokeKind.Scale => $"S{1f + d.X:0.000} {1f + d.Y:0.000} {1f + d.Z:0.000}",
            _ => "Identity",
        };
    }
}
