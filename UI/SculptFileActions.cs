using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Mascaron.Core;
using Mascaron.Export;
using Mascaron.GameBridge;
using Mascaron.Preview;

namespace Mascaron.UI;

/// <summary>
/// Whole-sculpt operations: Customize+ export/import, file save/load and reset.
/// Each reports its outcome on the status line.
/// </summary>
public sealed class SculptFileActions
{
    private const string FileFilter = ".json";

    private readonly SculptSession session;
    private readonly MascaronFileFormat fileFormat;
    private readonly CustomizePlusIpc cplusIpc;
    private readonly PreviewReconciler previewReconciler;
    private readonly StatusLine status;
    private readonly string sculptDirectory;
    private readonly FileDialogManager fileDialog = new();

    public SculptFileActions(
        SculptSession session,
        MascaronFileFormat fileFormat,
        CustomizePlusIpc cplusIpc,
        PreviewReconciler previewReconciler,
        StatusLine status,
        string sculptDirectory)
    {
        this.session = session;
        this.fileFormat = fileFormat;
        this.cplusIpc = cplusIpc;
        this.previewReconciler = previewReconciler;
        this.status = status;
        this.sculptDirectory = sculptDirectory;
    }

    public bool HasEdits => session.State.ModifiedCount > 0;

    /// <summary>Draws the open file dialog, if any.</summary>
    public void DrawDialogs()
    {
        fileDialog.Draw();
    }

    public void ExportToCustomizePlus()
    {
        if (!HasEdits)
        {
            status.Set("Nothing to export, no bones modified.");
            return;
        }

        ImGui.SetClipboardText(CustomizePlusCodec.Encode(session.State));
        previewReconciler.BeginHandoff();
        status.Set("Customize+ share code copied to clipboard.");
    }

    public void ImportFromCustomizePlus()
    {
        var (result, imported) = cplusIpc.ImportActiveProfile();
        switch (result)
        {
            case CustomizePlusIpc.ImportResult.Success:
                session.ReplaceState(imported!.GetModified());
                previewReconciler.AdoptImportedState(imported);
                status.Set($"Imported {imported.ModifiedCount} bone(s) from Customize+.");
                break;
            case CustomizePlusIpc.ImportResult.NoPlugin:
                status.Set("Customize+ is not loaded.");
                break;
            case CustomizePlusIpc.ImportResult.VersionMismatch:
                status.Set("Installed Customize+ uses an unsupported IPC version.");
                break;
            case CustomizePlusIpc.ImportResult.NoProfile:
                status.Set("No active Customize+ profile found.");
                break;
            case CustomizePlusIpc.ImportResult.NoFaceBones:
                status.Set("The Customize+ profile has no face bone edits.");
                break;
        }
    }

    public void Save()
    {
        if (!HasEdits)
        {
            status.Set("Nothing to save.");
            return;
        }

        Directory.CreateDirectory(sculptDirectory);
        fileDialog.SaveFileDialog("Save Sculpt", FileFilter, "sculpt.json", FileFilter, (ok, path) =>
        {
            if (!ok)
                return;

            fileFormat.Save(session.State, path);
            status.Set($"Saved to {Path.GetFileName(path)}.");
        }, sculptDirectory);
    }

    public void Load()
    {
        Directory.CreateDirectory(sculptDirectory);
        fileDialog.OpenFileDialog("Load Sculpt", FileFilter, (ok, paths) =>
        {
            if (!ok || paths.Count == 0)
                return;

            var path = paths[0];
            if (fileFormat.Load(path) is not { } loaded)
            {
                status.Set("Failed to load, the file is malformed.");
                return;
            }

            session.ReplaceState(loaded.GetModified());
            previewReconciler.CancelHandoff();
            status.Set($"Loaded {Path.GetFileName(path)}.");
        }, 1, sculptDirectory);
    }

    public void ResetAll()
    {
        session.ReplaceState([]);
        previewReconciler.CancelHandoff();
        status.Set("All bones reset.");
    }
}
