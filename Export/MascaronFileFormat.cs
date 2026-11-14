using System.Numerics;
using Mascaron.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mascaron.Export;

public class MascaronFileFormat
{
    public void Save(BoneTransformState state, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var bones = new JObject();
        foreach (var (boneName, transform) in state.GetModified())
        {
            bones[boneName] = new JObject
            {
                ["Translation"] = VecToJson(transform.Translation),
                ["Rotation"] = VecToJson(transform.Rotation),
                ["Scaling"] = VecToJson(transform.Scaling),
            };
        }

        var file = new JObject
        {
            ["SavedAt"] = DateTimeOffset.UtcNow,
            ["Bones"] = bones,
        };

        File.WriteAllText(path, file.ToString(Formatting.Indented));
    }

    /// <summary>
    /// Reads a saved sculpt; returns null when the file is missing or malformed.
    /// </summary>
    public BoneTransformState? Load(string path)
    {
        if (!File.Exists(path))
            return null;

        JObject obj;
        try
        {
            obj = JObject.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }

        if (obj["Bones"] is not JObject bonesObj)
            return null;

        var state = new BoneTransformState();
        foreach (var (boneName, value) in bonesObj)
        {
            if (value is not JObject boneObj)
                continue;

            var transform = new BoneTransform
            {
                Translation = JsonToVec(boneObj["Translation"], Vector3.Zero),
                Rotation = JsonToVec(boneObj["Rotation"], Vector3.Zero),
                Scaling = JsonToVec(boneObj["Scaling"], Vector3.One),
            };

            if (transform.IsModified)
                state.Set(boneName, transform);
        }

        return state;
    }

    private static JObject VecToJson(Vector3 v) => new() { ["X"] = v.X, ["Y"] = v.Y, ["Z"] = v.Z };

    private static Vector3 JsonToVec(JToken? token, Vector3 fallback)
    {
        if (token is not JObject obj)
            return fallback;

        return new Vector3(
            obj["X"]?.ToObject<float>() ?? fallback.X,
            obj["Y"]?.ToObject<float>() ?? fallback.Y,
            obj["Z"]?.ToObject<float>() ?? fallback.Z);
    }
}
