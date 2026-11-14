using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Mascaron.Core;
using Mascaron.Preview;
using Mascaron.UI.Canvas;
using Mascaron.Visualization;

namespace Mascaron.UI;

/// <summary>
/// The sculpting window: one toolbar row above a canvas that fills the rest of the window.
/// The window draws without padding so the canvas background is the window background.
/// </summary>
public class MainWindow : Window
{
    private static readonly Vector2 DefaultSize = new(720, 760);
    private const float MinimumHeight = 360f;

    private readonly SculptSession session;
    private readonly Configuration configuration;
    private readonly PreviewReconciler previewReconciler;
    private readonly StatusLine status;
    private readonly MainToolbar toolbar;
    private readonly FaceCanvas canvas;
    private readonly SculptFileActions fileActions;

    private Vector2 contentPadding;
    private bool paddingPushed;

    public MainWindow(
        SculptSession session,
        EditorState editor,
        Configuration configuration,
        PreviewReconciler previewReconciler,
        StatusLine status,
        SculptFileActions fileActions,
        FaceCanvasRenderer canvasRenderer)
        : base("Mascaron###MascaronMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.session = session;
        this.configuration = configuration;
        this.previewReconciler = previewReconciler;
        this.status = status;
        this.fileActions = fileActions;

        canvas = new FaceCanvas(session, editor, configuration, status, canvasRenderer);
        toolbar = new MainToolbar(session, editor, configuration, fileActions, Undo);

        Size = DefaultSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        UpdateSizeConstraints();
    }

    /// <summary>Actual screen position of the window during the last draw.</summary>
    public Vector2 LastPosition { get; private set; }

    /// <summary>Actual screen size of the window during the last draw.</summary>
    public Vector2 LastSize { get; private set; }

    public void SetTemplate(FaceTemplate template, byte race)
    {
        if (template == canvas.Template)
            return;

        canvas.SetTemplate(template);
        session.ReplaceState([]);
        previewReconciler.CancelHandoff();
        status.Set($"Race changed, now sculpting {RaceTemplates.GetRaceName(race)}.");
    }

    public override void PreDraw()
    {
        BgAlpha = configuration.BackgroundOpacity;
        contentPadding = ImGui.GetStyle().WindowPadding;

        // Zero padding is captured by Begin for this window only; Draw pops it so popups and tooltips keep theirs.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        paddingPushed = true;
    }

    public override void PostDraw()
    {
        PopPadding();
    }

    public override void Draw()
    {
        PopPadding();
        LastPosition = ImGui.GetWindowPos();
        LastSize = ImGui.GetWindowSize();

        HandleShortcuts();

        ImGui.SetCursorPos(new Vector2(contentPadding.X, ImGui.GetCursorPosY() + contentPadding.Y * 0.5f));
        toolbar.Draw(contentPadding);
        UpdateSizeConstraints();

        ImGui.SetCursorPos(new Vector2(0f, ImGui.GetCursorPosY() + contentPadding.Y * 0.5f));
        canvas.Draw(ImGui.GetCursorScreenPos(), ImGui.GetContentRegionAvail());

        fileActions.DrawDialogs();
    }

    private void HandleShortcuts()
    {
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) || canvas.IsDragging)
            return;

        var io = ImGui.GetIO();
        if (io.KeyCtrl && !io.WantTextInput && ImGui.IsKeyPressed(ImGuiKey.Z, false))
            Undo();
    }

    private void Undo()
    {
        canvas.CancelDrag();
        session.Undo();
    }

    private void UpdateSizeConstraints()
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(toolbar.MinimumWidth, MinimumHeight),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    private void PopPadding()
    {
        if (!paddingPushed)
            return;

        ImGui.PopStyleVar();
        paddingPushed = false;
    }
}
