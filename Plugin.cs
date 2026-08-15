using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Mascaron.Core;
using Mascaron.Export;
using Mascaron.GameBridge;
using Mascaron.Preview;
using Mascaron.UI;
using Mascaron.Visualization;

namespace Mascaron;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/mascaron";

    private readonly MascaronWindowSystem windowSystem;
    private readonly MainWindow mainWindow;
    private readonly HistoryWindow historyWindow;
    private readonly BridgeSelector boneApplicator;
    private readonly BoneTransformState transformState;
    private readonly CharacterResolver characterResolver;
    private readonly CustomizePlusIpc cplusIpc;
    private readonly PreviewReconciler previewReconciler;

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
        var configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configuration.Initialize(pluginInterface);

        transformState = new BoneTransformState();
        var topology = new TopologyGraph(FaceBoneRegistry.Bones);
        var sculptEngine = new SculptEngine(topology, transformState)
        {
            FalloffFactor = configuration.FalloffFactor,
            MirrorEnabled = configuration.MirrorEnabled,
            LinkEyesEnabled = configuration.LinkEyesEnabled,
        };

        characterResolver = new CharacterResolver(objectTable);
        boneApplicator = new BridgeSelector(pluginInterface, interop, sigScanner, objectTable, characterResolver, log);

        var fileFormat = new MascaronFileFormat();
        cplusIpc = new CustomizePlusIpc(pluginInterface, objectTable, log);
        previewReconciler = new PreviewReconciler(transformState);

        var strokeHistory = new SculptStrokeHistory();
        HistoryWindow? historyWindowRef = null;
        mainWindow = new MainWindow(
            transformState,
            sculptEngine,
            fileFormat,
            cplusIpc,
            previewReconciler,
            configuration,
            textureProvider,
            pluginInterface,
            strokeHistory,
            () => historyWindowRef?.ToggleOpen(),
            () => historyWindowRef?.IsOpen == true);
        historyWindow = new HistoryWindow(strokeHistory, sculptEngine, mainWindow);
        historyWindowRef = historyWindow;
        windowSystem = new MascaronWindowSystem(pluginInterface, mainWindow, historyWindow);

        framework.Update += OnFrameworkUpdate;

        commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the face bone sculpting window.",
        });

        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        Framework = framework;
    }

    private IDalamudPluginInterface PluginInterface { get; }
    private ICommandManager CommandManager { get; }
    private IFramework Framework { get; }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        windowSystem.Dispose();
        cplusIpc.Dispose();
        boneApplicator.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        windowSystem.MainWindow.IsOpen = true;
    }

    private void OnFrameworkUpdate(IFramework fw)
    {
        var appearance = characterResolver.GetVisualAppearance();
        if (appearance.HasValue)
            mainWindow.SetTemplate(RaceTemplates.FromRace(appearance.Value.Race, appearance.Value.Tribe), appearance.Value.Race);

        ReconcileCustomizePlusUpdate();

        if (boneApplicator.IsAvailable)
            boneApplicator.Apply(previewReconciler.PreviewState);
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
