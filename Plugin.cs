using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Mascaron.Core;
using Mascaron.Export;
using Mascaron.GameBridge;
using Mascaron.Preview;
using Mascaron.UI;
using Mascaron.UI.Canvas;
using Mascaron.Visualization;

namespace Mascaron;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/mascaron";

    private readonly ICommandManager commandManager;
    private readonly IFramework framework;
    private readonly Configuration configuration;
    private readonly CharacterResolver characterResolver;
    private readonly DirectBoneApplicator boneApplicator;
    private readonly CustomizePlusIpc cplusIpc;
    private readonly PreviewReconciler previewReconciler;
    private readonly MainWindow mainWindow;
    private readonly MascaronWindowSystem windowSystem;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IObjectTable objectTable,
        IGameInteropProvider interop,
        ISigScanner sigScanner,
        IPluginLog log,
        IFramework framework,
        ITextureProvider textureProvider)
    {
        this.commandManager = commandManager;
        this.framework = framework;

        configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configuration.Initialize(pluginInterface);

        characterResolver = new CharacterResolver(objectTable);
        boneApplicator = new DirectBoneApplicator(interop, sigScanner, characterResolver, log);
        cplusIpc = new CustomizePlusIpc(pluginInterface, objectTable, log);

        var state = new BoneTransformState();
        previewReconciler = new PreviewReconciler(state);
        var engine = new SculptEngine(new TopologyGraph(FaceBoneRegistry.Bones), state)
        {
            MirrorEnabled = configuration.MirrorEnabled,
            LinkEyesEnabled = configuration.LinkEyesEnabled,
            RegionLockEnabled = configuration.RegionLockEnabled,
            BrushRadius = configuration.BrushRadius,
        };
        var session = new SculptSession(state, engine, new SculptStrokeHistory(), boneApplicator);

        var editor = new EditorState();
        var status = new StatusLine();
        var assetsPath = Path.Combine(pluginInterface.AssemblyLocation.Directory!.FullName, "Assets", "Faces");
        var sculptDirectory = Path.Combine(pluginInterface.GetPluginConfigDirectory(), "Sculpts");
        var fileActions = new SculptFileActions(session, new MascaronFileFormat(), cplusIpc, previewReconciler, status, sculptDirectory);
        var canvasRenderer = new FaceCanvasRenderer(session, editor, status, previewReconciler, textureProvider, assetsPath);

        mainWindow = new MainWindow(session, editor, configuration, previewReconciler, status, fileActions, canvasRenderer);
        var historyWindow = new HistoryWindow(session, editor, mainWindow);
        windowSystem = new MascaronWindowSystem(pluginInterface, mainWindow, historyWindow);

        framework.Update += OnFrameworkUpdate;
        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the face bone sculpting window.",
        });
    }

    public void Dispose()
    {
        framework.Update -= OnFrameworkUpdate;
        commandManager.RemoveHandler(CommandName);
        windowSystem.Dispose();
        cplusIpc.Dispose();
        boneApplicator.Dispose();
        configuration.Save();
    }

    private void OnCommand(string command, string args)
    {
        windowSystem.OpenMainWindow();
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        var appearance = characterResolver.GetVisualAppearance();
        if (appearance.HasValue)
            mainWindow.SetTemplate(RaceTemplates.FromRace(appearance.Value.Race, appearance.Value.Tribe), appearance.Value.Race);

        ReconcileCustomizePlusUpdate();

        boneApplicator.CaptureFrames = mainWindow.IsOpen;
        if (boneApplicator.IsAvailable)
            boneApplicator.Apply(previewReconciler.PreviewState);

        configuration.FlushPendingSave();
    }

    private void ReconcileCustomizePlusUpdate()
    {
        if (!cplusIpc.TryDequeueLocalProfileUpdate(out var profileId))
            return;

        var (result, effectiveState) = cplusIpc.ReadProfile(profileId);
        if (result == CustomizePlusIpc.ProfileReadResult.Success && effectiveState != null)
            previewReconciler.ReconcileWith(effectiveState);
    }
}
