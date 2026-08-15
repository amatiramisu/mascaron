using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Mascaron.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mascaron.GameBridge;

public sealed class CustomizePlusIpc : IDisposable
{
    private readonly ICallGateSubscriber<ushort, (int, Guid?)> getActiveProfileId;
    private readonly ICallGateSubscriber<Guid, (int, string?)> getProfileById;
    private readonly ICallGateSubscriber<ushort, Guid, object?> profileUpdated;
    private readonly ConcurrentQueue<(ushort ObjectIndex, Guid ProfileId)> profileUpdates = new();
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;

    public CustomizePlusIpc(IDalamudPluginInterface pluginInterface, IObjectTable objectTable, IPluginLog log)
    {
        this.objectTable = objectTable;
        this.log = log;
        getActiveProfileId = pluginInterface.GetIpcSubscriber<ushort, (int, Guid?)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
        getProfileById = pluginInterface.GetIpcSubscriber<Guid, (int, string?)>("CustomizePlus.Profile.GetByUniqueId");
        profileUpdated = pluginInterface.GetIpcSubscriber<ushort, Guid, object?>("CustomizePlus.Profile.OnUpdate");
        profileUpdated.Subscribe(OnProfileUpdated);
    }

    public enum ImportResult { Success, NoPlugin, NoProfile, NoFaceBones }
    public enum ProfileReadResult { Success, NoPlugin, NoProfile }

    public void Dispose()
    {
        profileUpdated.Unsubscribe(OnProfileUpdated);
    }

    public (ImportResult Result, BoneTransformState? State) ImportActiveProfile()
    {
        try
        {
            var localPlayer = objectTable.LocalPlayer;
            if (localPlayer == null)
                return (ImportResult.NoProfile, null);

            var (idError, profileId) = getActiveProfileId.InvokeFunc(localPlayer.ObjectIndex);
            if (idError != 0 || profileId == null)
                return (ImportResult.NoProfile, null);

            var (readResult, state) = ReadProfile(profileId.Value);
            if (readResult == ProfileReadResult.NoPlugin)
                return (ImportResult.NoPlugin, null);
            if (readResult != ProfileReadResult.Success || state == null)
                return (ImportResult.NoProfile, null);

            if (state.ModifiedCount == 0)
                return (ImportResult.NoFaceBones, null);

            return (ImportResult.Success, state);
        }
        catch (IpcNotReadyError)
        {
            log.Warning("Customize+ IPC unavailable — plugin may not be loaded.");
            return (ImportResult.NoPlugin, null);
        }
        catch (Exception ex)
        {
            log.Error($"Failed to import from Customize+: {ex}");
            return (ImportResult.NoPlugin, null);
        }
    }

    /// <summary>
    /// Reads the effective face-bone state exposed for a Customize+ profile.
    /// </summary>
    public (ProfileReadResult Result, BoneTransformState? State) ReadProfile(Guid profileId)
    {
        if (profileId == Guid.Empty)
            return (ProfileReadResult.Success, new BoneTransformState());

        try
        {
            var (profileError, json) = getProfileById.InvokeFunc(profileId);
            if (profileError != 0 || string.IsNullOrEmpty(json))
                return (ProfileReadResult.NoProfile, null);

            var state = ParseProfile(json);
            return state == null
                ? (ProfileReadResult.NoProfile, null)
                : (ProfileReadResult.Success, state);
        }
        catch (IpcNotReadyError)
        {
            return (ProfileReadResult.NoPlugin, null);
        }
        catch (Exception ex)
        {
            log.Error($"Failed to read Customize+ profile: {ex}");
            return (ProfileReadResult.NoProfile, null);
        }
    }

    /// <summary>
    /// Returns the newest queued profile update for the local player.
    /// </summary>
    public bool TryDequeueLocalProfileUpdate(out Guid profileId)
    {
        profileId = Guid.Empty;
        var localPlayer = objectTable.LocalPlayer;
        var found = false;

        while (profileUpdates.TryDequeue(out var update))
        {
            if (localPlayer != null && update.ObjectIndex == localPlayer.ObjectIndex)
            {
                profileId = update.ProfileId;
                found = true;
            }
        }

        return found;
    }

    private void OnProfileUpdated(ushort objectIndex, Guid profileId)
    {
        profileUpdates.Enqueue((objectIndex, profileId));
    }

    private static BoneTransformState? ParseProfile(string json)
    {
        var obj = JObject.Parse(json);
        var bonesToken = obj["Bones"];
        if (bonesToken is not JObject bonesObj)
            return null;

        var state = new BoneTransformState();
        foreach (var (boneName, value) in bonesObj)
        {
            if (!FaceBoneRegistry.IsSculptable(boneName))
                continue;

            if (value is not JObject boneObj)
                continue;

            var transform = new BoneTransform
            {
                Translation = ParseVector3(boneObj["Translation"], Vector3.Zero),
                Rotation = ParseVector3(boneObj["Rotation"], Vector3.Zero),
                Scaling = ParseVector3(boneObj["Scaling"], Vector3.One),
            };

            if (transform.IsModified)
                state.Set(boneName, transform);
        }

        return state;
    }

    private static Vector3 ParseVector3(JToken? token, Vector3 fallback)
    {
        if (token is not JObject obj)
            return fallback;

        return new Vector3(
            obj["X"]?.ToObject<float>() ?? fallback.X,
            obj["Y"]?.ToObject<float>() ?? fallback.Y,
            obj["Z"]?.ToObject<float>() ?? fallback.Z);
    }
}
