using Dalamud.Configuration;
using Dalamud.Plugin;

namespace Mascaron;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public float FalloffFactor { get; set; } = 0.75f;
    public bool MirrorEnabled { get; set; } = true;
    public float WindowOpacity { get; set; } = 0.85f;
    public float BrushRadius { get; set; } = 80f;
    public int FalloffCurve { get; set; } = 1;
    public bool TopologyEnabled { get; set; }
    public bool BrushEnabled { get; set; } = true;

    public float? WindowX { get; set; }
    public float? WindowY { get; set; }
    public float? WindowWidth { get; set; }
    public float? WindowHeight { get; set; }

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        pluginInterface = pi;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }
}
