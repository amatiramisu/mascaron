using Dalamud.Configuration;
using Dalamud.Plugin;

namespace Mascaron;

[Serializable]
public class Configuration : IPluginConfiguration
{
    private const int CurrentVersion = 1;
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    public int Version { get; set; } = CurrentVersion;

    public bool MirrorEnabled { get; set; } = true;
    public bool LinkEyesEnabled { get; set; } = true;
    public bool RegionLockEnabled { get; set; }

    /// <summary>Brush radius in face-normalized canvas units.</summary>
    public float BrushRadius { get; set; } = 0.13f;

    /// <summary>Drag sensitivity slider position, 0 to 1 with 0.5 as the calibrated default.</summary>
    public float Sensitivity { get; set; } = 0.5f;

    public float BackgroundOpacity { get; set; } = 0.85f;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    [NonSerialized]
    private DateTime? pendingSaveAt;

    public void Initialize(IDalamudPluginInterface pi)
    {
        pluginInterface = pi;
        if (Version >= CurrentVersion)
            return;

        // Version 0 stored the brush radius in screen pixels.
        BrushRadius = 0.13f;
        Version = CurrentVersion;
        Save();
    }

    /// <summary>Schedules a save; repeated requests within the delay coalesce into one write.</summary>
    public void RequestSave()
    {
        pendingSaveAt = DateTime.UtcNow + SaveDelay;
    }

    public void FlushPendingSave()
    {
        if (pendingSaveAt is { } due && DateTime.UtcNow >= due)
            Save();
    }

    public void Save()
    {
        pendingSaveAt = null;
        pluginInterface?.SavePluginConfig(this);
    }
}
