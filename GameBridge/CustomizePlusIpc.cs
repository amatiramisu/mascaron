using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Mascaron.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mascaron.GameBridge;

public class CustomizePlusIpc
{
    private readonly ICallGateSubscriber<ushort, (int, Guid?)> getActiveProfileId;
    private readonly ICallGateSubscriber<Guid, (int, string?)> getProfileById;
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;

    public CustomizePlusIpc(IDalamudPluginInterface pluginInterface, IObjectTable objectTable, IPluginLog log)
    {
        this.objectTable = objectTable;
        this.log = log;
        getActiveProfileId = pluginInterface.GetIpcSubscriber<ushort, (int, Guid?)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
        getProfileById = pluginInterface.GetIpcSubscriber<Guid, (int, string?)>("CustomizePlus.Profile.GetByUniqueId");
    }

    public enum ImportResult { Success, NoPlugin, NoProfile, NoFaceBones }

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

            var (profileError, json) = getProfileById.InvokeFunc(profileId.Value);
            if (profileError != 0 || string.IsNullOrEmpty(json))
                return (ImportResult.NoProfile, null);

            var state = ParseProfile(json);
            if (state == null || state.ModifiedCount == 0)
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

    private static BoneTransformState? ParseProfile(string json)
    {
        var obj = JObject.Parse(json);
        var bonesToken = obj["Bones"];
        if (bonesToken is not JObject bonesObj)
            return null;

        var state = new BoneTransformState();
        foreach (var (boneName, value) in bonesObj)
        {
            if (FaceBoneRegistry.GetByCodename(boneName) == null)
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
