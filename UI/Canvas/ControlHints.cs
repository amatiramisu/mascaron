using System.Numerics;
using Dalamud.Bindings.ImGui;
using Mascaron.Core;

namespace Mascaron.UI.Canvas;

/// <summary>
/// The control hint line at the top of the canvas: the active mode's drag axes, then the view controls when
/// the canvas is wide enough. Each axis binding uses Customize+'s column color, separators and view controls
/// the regular text color.
/// </summary>
public static class ControlHints
{
    private const string Separator = "  |  ";
    private const string GroupGap = "        ";
    private const string ViewControls = "Wheel: brush size  |  Ctrl+Wheel: zoom  |  Right-drag: pan  |  Double right-click: reset view";

    // Customize+ BoneEditorPanel axis colors (X/Roll red, Y/Pitch green, Z/Yaw blue), opaque for text.
    private static readonly Vector4 AxisXColor = new(0.80f, 0.28f, 0.28f, 1f);
    private static readonly Vector4 AxisYColor = new(0.35f, 0.72f, 0.35f, 1f);
    private static readonly Vector4 AxisZColor = new(0.32f, 0.52f, 0.95f, 1f);

    private readonly record struct Segment(string Text, Vector4? Color = null);

    private static readonly Segment[] TranslateAxes =
    [
        new("Drag left/right: X", AxisXColor), new(Separator),
        new("Drag up/down: Y", AxisYColor), new(Separator),
        new("Shift+Drag up/down: Z", AxisZColor),
    ];

    private static readonly Segment[] RotateAxes =
    [
        new("Drag left/right: Roll", AxisXColor), new(Separator),
        new("Drag up/down: Pitch", AxisYColor), new(Separator),
        new("Shift+Drag up/down: Yaw", AxisZColor),
    ];

    /// <summary>Draws the line with its top-left at <paramref name="position"/> and returns the height it took.</summary>
    public static float Draw(ImDrawListPtr drawList, Vector2 position, float maxWidth, SculptStrokeKind mode)
    {
        var segments = mode == SculptStrokeKind.Rotate ? RotateAxes : TranslateAxes;
        var textColor = ImGui.GetColorU32(ImGuiCol.Text);

        var axesWidth = MeasureWidth(segments);
        var viewText = GroupGap + ViewControls;
        var showView = axesWidth + ImGui.CalcTextSize(viewText).X <= maxWidth;

        foreach (var segment in segments)
            position.X += DrawText(drawList, position, segment.Color is { } color ? ImGui.GetColorU32(color) : textColor, segment.Text);

        if (showView)
            DrawText(drawList, position, textColor, viewText);

        return ImGui.GetTextLineHeightWithSpacing();
    }

    private static float MeasureWidth(Segment[] segments)
    {
        var width = 0f;
        foreach (var segment in segments)
            width += ImGui.CalcTextSize(segment.Text).X;
        return width;
    }

    private static float DrawText(ImDrawListPtr drawList, Vector2 position, uint color, string text)
    {
        drawList.AddText(position, color, text);
        return ImGui.CalcTextSize(text).X;
    }
}
