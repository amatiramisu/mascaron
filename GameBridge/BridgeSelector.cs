using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Mascaron.Core;

namespace Mascaron.GameBridge;

public class BridgeSelector : IBoneApplicator
{
    private readonly DirectBoneApplicator directApplicator;

    public BridgeSelector(
        IDalamudPluginInterface pluginInterface,
        IGameInteropProvider interop,
        ISigScanner sigScanner,
        IObjectTable objectTable,
        CharacterResolver characterResolver,
        IPluginLog log)
    {
        directApplicator = new DirectBoneApplicator(interop, sigScanner, characterResolver, log);
    }

    public bool IsAvailable => directApplicator.IsAvailable;

    public void Apply(BoneTransformState state)
    {
        directApplicator.Apply(state);
    }

    public void Dispose()
    {
        directApplicator.Dispose();
    }
}
