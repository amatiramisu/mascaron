using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Mascaron.Core;

namespace Mascaron.UI;

/// <summary>
/// Recent strokes, docked to the right of the main window until the user moves it.
/// Selecting a stroke highlights it on the canvas and exposes its brush radius for editing.
/// </summary>
public class HistoryWindow : Window
{
    private const float Width = 360f;
    private const float Gap = 8f;

    private readonly SculptSession session;
    private readonly EditorState editor;
    private readonly MainWindow anchor;
    private Vector2? expectedPosition;
    private bool docked = true;

    public HistoryWindow(SculptSession session, EditorState editor, MainWindow anchor)
        : base("Mascaron History###MascaronHistory", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings)
    {
        this.session = session;
        this.editor = editor;
        this.anchor = anchor;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(260, 220),
            MaximumSize = new Vector2(600, float.MaxValue),
        };
    }

    public override void PreOpenCheck()
    {
        if (!editor.HistoryToggleRequested)
            return;

        editor.HistoryToggleRequested = false;
        IsOpen = !IsOpen;
        if (IsOpen)
            docked = true;
    }

    public override void OnOpen()
    {
        editor.HistoryOpen = true;
    }

    public override void OnClose()
    {
        editor.HistoryOpen = false;
    }

    public override void PreDraw()
    {
        if (!docked || !anchor.IsOpen || anchor.LastSize == Vector2.Zero)
            return;

        var position = anchor.LastPosition + new Vector2(anchor.LastSize.X + Gap * ImGuiHelpers.GlobalScale, 0f);
        expectedPosition = position;
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(Width * ImGuiHelpers.GlobalScale, anchor.LastSize.Y), ImGuiCond.Always);
    }

    public override void Draw()
    {
        UpdateDocking();

        if (!session.History.HasStrokes)
        {
            ImGui.TextDisabled("No strokes yet.");
            return;
        }

        DrawSelectedStrokeEditor();
        DrawStrokeList();

        if (ImGui.Button("Clear History"))
            session.History.Clear();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Keep the current sculpt and forget the editable stroke history.");
    }

    private void DrawSelectedStrokeEditor()
    {
        var stroke = session.History.SelectedStroke;
        if (stroke == null || stroke == session.ActiveStroke)
        {
            ImGui.TextDisabled("Select a stroke to edit its brush radius.");
            return;
        }

        var radius = stroke.Radius * 100f;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderFloat("##StrokeRadius", ref radius, BrushInfluence.RadiusMin * 100f, BrushInfluence.RadiusMax * 100f,
                radius <= BrushInfluence.RadiusMin * 100f ? "Radius: direct" : "Radius %.0f%%"))
        {
            stroke.SetRadius(radius / 100f);
            session.ReplayHistory();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Brush radius of the selected stroke, as a share of the face width.\nThe sculpt replays with the new radius.");
    }

    private void DrawStrokeList()
    {
        var rowClicked = false;
        ImGui.BeginChild("##StrokeHistory", new Vector2(0, -ImGui.GetFrameHeightWithSpacing()), true);
        for (var i = 0; i < session.History.Strokes.Count; i++)
        {
            var stroke = session.History.Strokes[i];
            if (ImGui.Selectable(GetStrokeLabel(i, stroke), i == session.History.SelectedIndex))
            {
                rowClicked = true;
                session.History.Select(i);
            }
        }

        if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !rowClicked)
            session.History.ClearSelection();

        ImGui.EndChild();
    }

    private void UpdateDocking()
    {
        if (!docked || expectedPosition == null)
            return;

        var moved = Vector2.DistanceSquared(ImGui.GetWindowPos(), expectedPosition.Value) > 4f;
        if (moved && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            docked = false;
    }

    private static string GetStrokeLabel(int index, SculptStroke stroke)
    {
        var name = FaceBoneRegistry.GetByCodename(stroke.PrimaryBone)?.DisplayName ?? stroke.PrimaryBone;
        var scope = stroke.Radius <= BrushInfluence.RadiusMin ? "Direct" : $"Brush {stroke.Radius * 100f:0}%";
        return $"{index + 1:00}  {stroke.Kind}  {name}  {scope}  ({FormatStrokeDelta(stroke)})";
    }

    private static string FormatStrokeDelta(SculptStroke stroke)
    {
        var d = stroke.Delta;
        return stroke.Kind switch
        {
            SculptStrokeKind.Move => $"T {d.X:+0.0000;-0.0000} {d.Y:+0.0000;-0.0000} {d.Z:+0.0000;-0.0000}",
            SculptStrokeKind.Rotate => $"R {d.X:+0.0;-0.0} {d.Y:+0.0;-0.0} {d.Z:+0.0;-0.0}",
            SculptStrokeKind.Scale => $"S {1f + d.X:0.000} {1f + d.Y:0.000} {1f + d.Z:0.000}",
            _ => string.Empty,
        };
    }
}
