using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Mascaron.Core;
using Mascaron.Export;
using Mascaron.GameBridge;
using Mascaron.UI;
using Mascaron.Visualization;

namespace Mascaron;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/mascaron";

    private readonly MascaronWindowSystem windowSystem;
    private readonly MainWindow mainWindow;
    private readonly BridgeSelector boneApplicator;
    private readonly BoneTransformState transformState;
    private readonly CharacterResolver characterResolver;

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
        };

        characterResolver = new CharacterResolver(objectTable);
        boneApplicator = new BridgeSelector(pluginInterface, interop, sigScanner, objectTable, characterResolver, log);

        var fileFormat = new MascaronFileFormat();
        var cplusIpc = new CustomizePlusIpc(pluginInterface, objectTable, log);

        mainWindow = new MainWindow(transformState, sculptEngine, fileFormat, cplusIpc, configuration, textureProvider, pluginInterface);
        windowSystem = new MascaronWindowSystem(pluginInterface, mainWindow);

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

        if (boneApplicator.IsAvailable)
            boneApplicator.Apply(transformState);
    }
}
