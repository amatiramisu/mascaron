using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Mascaron.Core;

namespace Mascaron.UI;

/// <summary>
/// The single icon row above the canvas: sculpt modes, symmetry toggles, the sensitivity slider
/// and the sculpt-level actions.
/// </summary>
public sealed class MainToolbar
{
    private const string ResetPopupId = "##MascaronResetConfirm";
    private const string SettingsPopupId = "##MascaronSettings";
    private const float SliderMinWidth = 90f;

    private readonly SculptSession session;
    private readonly EditorState editor;
    private readonly Configuration configuration;
    private readonly SculptFileActions fileActions;
    private readonly Action onUndo;

    private float actionGroupWidth;

    public MainToolbar(
        SculptSession session,
        EditorState editor,
        Configuration configuration,
        SculptFileActions fileActions,
        Action onUndo)
    {
        this.session = session;
        this.editor = editor;
        this.configuration = configuration;
        this.fileActions = fileActions;
        this.onUndo = onUndo;
    }

    /// <summary>Width the row needs with the slider at its minimum, in unscaled pixels.</summary>
    public float MinimumWidth { get; private set; } = 600f;

    /// <summary>
    /// Draws the row starting at the current cursor. The host window draws without padding so the canvas
    /// can reach its edges; <paramref name="padding"/> is the regular window padding, kept free on the right.
    /// </summary>
    public void Draw(Vector2 padding)
    {
        var rowStartX = ImGui.GetCursorScreenPos().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        DrawModeButton(FontAwesomeIcon.ArrowsUpDownLeftRight, SculptStrokeKind.Move, "Move");
        DrawModeButton(FontAwesomeIcon.SyncAlt, SculptStrokeKind.Rotate, "Rotate");
        DrawModeButton(FontAwesomeIcon.ExpandAlt, SculptStrokeKind.Scale, "Scale");
        DrawGroupGap();

        DrawToggle(FontAwesomeIcon.ArrowsAltH, session.Engine.MirrorEnabled, "Mirror: the opposite side follows with mirrored motion.",
            value => { session.Engine.MirrorEnabled = value; configuration.MirrorEnabled = value; });
        DrawToggle(FontAwesomeIcon.Link, session.Engine.LinkEyesEnabled, "Link Eyes: both irises receive the identical motion.",
            value => { session.Engine.LinkEyesEnabled = value; configuration.LinkEyesEnabled = value; });
        DrawToggle(FontAwesomeIcon.Lock, session.Engine.RegionLockEnabled, "Region Lock: the brush stays inside the grabbed bone's facial region.",
            value => { session.Engine.RegionLockEnabled = value; configuration.RegionLockEnabled = value; });
        DrawGroupGap();

        var leftWidth = ImGui.GetCursorScreenPos().X - rowStartX;
        var sliderWidth = MathF.Max(SliderMinWidth * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().X - actionGroupWidth - spacing - padding.X);
        DrawSensitivitySlider(sliderWidth);

        ImGui.SameLine();
        var actionsStartX = ImGui.GetCursorScreenPos().X;
        DrawActions();
        actionGroupWidth = ImGui.GetItemRectMax().X - actionsStartX;

        MinimumWidth = (leftWidth + SliderMinWidth * ImGuiHelpers.GlobalScale + spacing + actionGroupWidth + padding.X * 2f) / ImGuiHelpers.GlobalScale;
    }

    private void DrawSensitivitySlider(float width)
    {
        ImGui.SetNextItemWidth(width);
        var percent = configuration.Sensitivity * 100f;
        if (ImGui.SliderFloat("##Sensitivity", ref percent, 0f, 100f, "Sensitivity %.0f%%"))
            configuration.Sensitivity = Math.Clamp(percent / 100f, 0f, 1f);
        if (ImGui.IsItemDeactivatedAfterEdit())
            configuration.RequestSave();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Drag step per mouse pixel. 50% is calibrated to real facial expression ranges;\n0% is ten times finer, 100% ten times coarser. Ctrl+click to type a value.");
    }

    private void DrawActions()
    {
        ImGui.BeginDisabled(!session.History.HasStrokes);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Undo))
            onUndo();
        ImGui.EndDisabled();
        Tooltip("Undo last stroke (Ctrl+Z)");

        ImGui.SameLine();
        if (IconToggle(FontAwesomeIcon.History, editor.HistoryOpen))
            editor.HistoryToggleRequested = true;
        Tooltip("Stroke history: select a stroke to edit its brush radius.");
        DrawGroupGap();

        if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
            fileActions.ImportFromCustomizePlus();
        Tooltip("Import the active Customize+ profile's face bones.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.FileExport))
            fileActions.ExportToCustomizePlus();
        Tooltip("Copy a Customize+ share code to the clipboard.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.FolderOpen))
            fileActions.Load();
        Tooltip("Load a saved sculpt.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Save))
            fileActions.Save();
        Tooltip("Save the sculpt to a file.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
            ImGui.OpenPopup(ResetPopupId);
        Tooltip("Reset all bones.");
        DrawResetPopup();

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            ImGui.OpenPopup(SettingsPopupId);
        Tooltip("Settings");
        DrawSettingsPopup();
    }

    private void DrawResetPopup()
    {
        if (ImGui.BeginPopup(ResetPopupId))
        {
            ImGui.TextUnformatted("Reset every bone and clear the stroke history?");
            if (ImGui.Button("Reset All"))
            {
                fileActions.ResetAll();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void DrawSettingsPopup()
    {
        if (ImGui.BeginPopup(SettingsPopupId))
        {
            var opacity = configuration.BackgroundOpacity * 100f;
            ImGui.SetNextItemWidth(160f * ImGuiHelpers.GlobalScale);
            if (ImGui.SliderFloat("Background opacity", ref opacity, 20f, 100f, "%.0f%%"))
                configuration.BackgroundOpacity = opacity / 100f;
            if (ImGui.IsItemDeactivatedAfterEdit())
                configuration.RequestSave();
            ImGui.EndPopup();
        }
    }

    private void DrawModeButton(FontAwesomeIcon icon, SculptStrokeKind mode, string tooltip)
    {
        if (IconToggle(icon, editor.Mode == mode))
            editor.Mode = mode;
        Tooltip(tooltip);
        ImGui.SameLine();
    }

    private void DrawToggle(FontAwesomeIcon icon, bool active, string tooltip, Action<bool> setter)
    {
        if (IconToggle(icon, active))
        {
            setter(!active);
            configuration.RequestSave();
        }

        Tooltip(tooltip);
        ImGui.SameLine();
    }

    private static bool IconToggle(FontAwesomeIcon icon, bool active)
    {
        var activeColor = active ? ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive] : (Vector4?)null;
        return ImGuiComponents.IconButton(icon, activeColor);
    }

    private static void DrawGroupGap()
    {
        ImGui.SameLine(0f, ImGui.GetStyle().ItemSpacing.X * 3f);
    }

    private static void Tooltip(string text)
    {
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(text);
    }
}
