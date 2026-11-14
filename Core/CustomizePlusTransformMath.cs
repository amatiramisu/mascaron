using System.Numerics;

namespace Mascaron.Core;

/// <summary>
/// Customize+ bone transform semantics, mirrored from CustomizePlus Core/Data/BoneTransform.cs
/// (ModifyExistingTransform, IsEdited, ClampAngles) and Core/Extensions/VectorExtensions.cs (ToQuaternion).
/// Mascaron stores and previews transforms with exactly these rules, so an exported sculpt
/// applied by Customize+ reproduces the preview.
/// </summary>
public static class CustomizePlusTransformMath
{
    private const float TranslationEpsilon = 0.00001f;
    private const float RotationEpsilonDegrees = 0.1f;
    private const float ScaleEpsilon = 0.00001f;
    private const float DegreesToRadians = MathF.PI / 180f;
    private const float RadiansToDegrees = 180f / MathF.PI;

    /// <summary>
    /// Converts a Customize+ rotation vector (degrees) to a quaternion.
    /// Customize+ passes X as yaw, Y as pitch and Z as roll.
    /// </summary>
    public static Quaternion ToQuaternion(Vector3 rotationDegrees)
    {
        return Quaternion.CreateFromYawPitchRoll(
            rotationDegrees.X * DegreesToRadians,
            rotationDegrees.Y * DegreesToRadians,
            rotationDegrees.Z * DegreesToRadians);
    }

    /// <summary>
    /// Exact inverse of <see cref="ToQuaternion"/>, wrapped to the Customize+ angle range.
    /// </summary>
    public static Vector3 ToRotationDegrees(Quaternion rotation)
    {
        var q = Quaternion.Normalize(rotation);
        var m02 = 2f * (q.X * q.Z + q.W * q.Y);
        var m22 = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        var m12 = 2f * (q.Y * q.Z - q.W * q.X);
        var m10 = 2f * (q.X * q.Y + q.W * q.Z);
        var m11 = 1f - 2f * (q.X * q.X + q.Z * q.Z);

        var yaw = MathF.Atan2(m02, m22);
        var pitch = MathF.Asin(Math.Clamp(-m12, -1f, 1f));
        var roll = MathF.Atan2(m10, m11);

        return WrapAngles(new Vector3(yaw, pitch, roll) * RadiansToDegrees);
    }

    /// <summary>
    /// Wraps each angle into the (-180, 180] range Customize+ stores.
    /// </summary>
    public static Vector3 WrapAngles(Vector3 degrees)
    {
        return new Vector3(WrapAngle(degrees.X), WrapAngle(degrees.Y), WrapAngle(degrees.Z));
    }

    /// <summary>
    /// Customize+ only applies a bone whose transform passes this test.
    /// </summary>
    public static bool IsEdited(in BoneTransform transform)
    {
        return !IsApproximately(transform.Translation, Vector3.Zero, TranslationEpsilon)
               || !IsApproximately(transform.Rotation, Vector3.Zero, RotationEpsilonDegrees)
               || !IsApproximately(transform.Scaling, Vector3.One, ScaleEpsilon);
    }

    /// <summary>
    /// Applies a transform to a model-space bone pose the way Customize+ does:
    /// scale, then local rotation, then translation along the rotated bone axes.
    /// </summary>
    public static void ApplyToModelPose(in BoneTransform transform, ref Vector3 translation, ref Quaternion rotation, ref Vector3 scale)
    {
        scale *= transform.Scaling;
        rotation = Quaternion.Multiply(rotation, ToQuaternion(transform.Rotation));
        translation += Vector3.Transform(transform.Translation, rotation);
    }

    /// <summary>
    /// The orientation Customize+ uses to place a bone's translation, expressed relative to
    /// the frame <paramref name="boneOrientation"/> is given in.
    /// </summary>
    public static Quaternion GetTranslationFrame(Quaternion boneOrientation, Vector3 rotationDegrees)
    {
        return Quaternion.Multiply(boneOrientation, ToQuaternion(rotationDegrees));
    }

    private static float WrapAngle(float degrees)
    {
        var wrapped = degrees % 360f;
        if (wrapped > 180f)
            wrapped -= 360f;
        else if (wrapped <= -180f)
            wrapped += 360f;
        return wrapped;
    }

    private static bool IsApproximately(Vector3 value, Vector3 other, float margin)
    {
        return MathF.Abs(value.X - other.X) < margin
               && MathF.Abs(value.Y - other.Y) < margin
               && MathF.Abs(value.Z - other.Z) < margin;
    }
}
