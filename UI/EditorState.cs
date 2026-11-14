using Mascaron.Core;

namespace Mascaron.UI;

/// <summary>
/// UI state shared between the main window and the history panel.
/// </summary>
public sealed class EditorState
{
    public SculptStrokeKind Mode { get; set; } = SculptStrokeKind.Move;

    /// <summary>Whether the history panel is currently shown; maintained by the panel.</summary>
    public bool HistoryOpen { get; set; }

    /// <summary>Set by the toolbar; the history panel consumes it on its next frame.</summary>
    public bool HistoryToggleRequested { get; set; }

    /// <summary>The stroke highlighted on the canvas: the history selection while the panel is open.</summary>
    public SculptStroke? GetFocusedStroke(SculptStrokeHistory history)
    {
        return HistoryOpen ? history.SelectedStroke : null;
    }
}
