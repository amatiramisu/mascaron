using System.IO.Compression;
using System.Numerics;
using System.Text;
using Mascaron.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mascaron.Export;

public static class CustomizePlusCodec
{
    private const int TemplateVersion = 6;

    public static string Encode(BoneTransformState state)
    {
        var bonesObj = new JObject();
        foreach (var (boneName, transform) in state.GetModified())
        {
            bonesObj[boneName] = JObject.FromObject(new
            {
                Translation = new { transform.Translation.X, transform.Translation.Y, transform.Translation.Z },
                Rotation = new { transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z },
                Scaling = new { transform.Scaling.X, transform.Scaling.Y, transform.Scaling.Z },
                PropagateTranslation = false,
                PropagateRotation = false,
                PropagateScale = false,
                ChildScalingIndependent = false,
            });
        }

        var template = new JObject
        {
            ["Version"] = TemplateVersion,
            ["UniqueId"] = Guid.NewGuid(),
            ["CreationDate"] = DateTimeOffset.UtcNow,
            ["ModifiedDate"] = DateTimeOffset.UtcNow,
            ["Name"] = "Mascaron",
            ["Bones"] = bonesObj,
            ["IsWriteProtected"] = false,
        };

        var bytes = Encoding.UTF8.GetBytes(template.ToString(Formatting.None));
        using var compressedStream = new MemoryStream();
        using (var gzip = new GZipStream(compressedStream, CompressionMode.Compress))
        {
            gzip.WriteByte(TemplateVersion);
            gzip.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(compressedStream.ToArray());
    }

    public static BoneTransformState? Decode(string base64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch
        {
            return null;
        }

        using var compressedStream = new MemoryStream(bytes);
        using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        gzip.CopyTo(resultStream);

        var raw = resultStream.ToArray();
        if (raw.Length < 2)
            return null;

        var json = Encoding.UTF8.GetString(raw, 1, raw.Length - 1);

        JObject obj;
        try
        {
            obj = JObject.Parse(json);
        }
        catch
        {
            return null;
        }

        var bonesToken = obj["Bones"];
        if (bonesToken is not JObject bonesObj)
            return null;

        var state = new BoneTransformState();
        foreach (var (boneName, value) in bonesObj)
        {
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
