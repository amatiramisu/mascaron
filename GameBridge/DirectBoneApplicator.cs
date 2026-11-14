using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using Mascaron.Core;

namespace Mascaron.GameBridge;

/// <summary>
/// Writes Mascaron's preview into the local player's model pose from the same render hook
/// Customize+ uses, with Customize+'s transform math, and samples face bone frames for the editor.
/// </summary>
public sealed unsafe class DirectBoneApplicator : IBoneFrameSource, IDisposable
{
    private const string RenderHookSignature = "E8 ?? ?? ?? ?? 48 81 C3 ?? ?? ?? ?? BF ?? ?? ?? ?? 33 ED";
    private const int TruePoseIndex = 0;
    private const string LeftEyeBone = "j_f_eye_l";
    private const string RightEyeBone = "j_f_eye_r";

    private delegate nint RenderDelegate(nint a1, nint a2, nint a3, int a4);

    private readonly CharacterResolver characterResolver;
    private readonly Hook<RenderDelegate>? renderHook;
    private readonly IPluginLog log;
    private readonly SkeletonBoneIndex boneIndex = new();
    private readonly List<(string Bone, BoneTransform Transform)> editedBones = [];

    private BoneTransformState? pendingState;
    private int pendingVersion = -1;
    private BoneFrameSnapshot? latestFrames;

    public DirectBoneApplicator(
        IGameInteropProvider interop,
        ISigScanner sigScanner,
        CharacterResolver characterResolver,
        IPluginLog log)
    {
        this.characterResolver = characterResolver;
        this.log = log;

        try
        {
            var renderAddress = sigScanner.ScanText(RenderHookSignature);
            renderHook = interop.HookFromAddress<RenderDelegate>(renderAddress, OnRender);
            renderHook.Enable();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            log.Error($"Failed to establish render hook: {ex}");
        }
    }

    public bool IsAvailable { get; }

    /// <summary>Samples face bone frames each render while true (the editor window is open).</summary>
    public bool CaptureFrames { get; set; }

    public BoneFrameSnapshot? LatestFrames => Volatile.Read(ref latestFrames);

    /// <summary>
    /// Sets the state written on every render. The edited-bone list is rebuilt when its version changes.
    /// </summary>
    public void Apply(BoneTransformState state)
    {
        if (ReferenceEquals(state, pendingState) && state.Version == pendingVersion)
            return;

        editedBones.Clear();
        foreach (var (bone, transform) in state.GetModified())
        {
            if (CustomizePlusTransformMath.IsEdited(transform))
                editedBones.Add((bone, transform));
        }

        pendingState = state;
        pendingVersion = state.Version;
    }

    public void Dispose()
    {
        renderHook?.Disable();
        renderHook?.Dispose();
    }

    private nint OnRender(nint a1, nint a2, nint a3, int a4)
    {
        try
        {
            var cBase = characterResolver.GetLocalPlayerCharacterBase();
            if (cBase != null && cBase->Skeleton != null)
            {
                boneIndex.Refresh(cBase);
                if (CaptureFrames)
                    Volatile.Write(ref latestFrames, SampleFrames(cBase));
                WriteEditedBones(cBase);
            }
            else if (CaptureFrames)
            {
                Volatile.Write(ref latestFrames, null);
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error in render hook: {ex}");
        }

        return renderHook!.Original(a1, a2, a3, a4);
    }

    private void WriteEditedBones(CharacterBase* cBase)
    {
        foreach (var (bone, transform) in editedBones)
        {
            var pose = TryGetModelPose(cBase, bone, out var index);
            if (pose == null)
                continue;

            ref var target = ref pose[index];
            var translation = new Vector3(target.Translation.X, target.Translation.Y, target.Translation.Z);
            var rotation = new Quaternion(target.Rotation.X, target.Rotation.Y, target.Rotation.Z, target.Rotation.W);
            var scale = new Vector3(target.Scale.X, target.Scale.Y, target.Scale.Z);

            CustomizePlusTransformMath.ApplyToModelPose(transform, ref translation, ref rotation, ref scale);

            target.Translation.X = translation.X;
            target.Translation.Y = translation.Y;
            target.Translation.Z = translation.Z;
            target.Rotation.X = rotation.X;
            target.Rotation.Y = rotation.Y;
            target.Rotation.Z = rotation.Z;
            target.Rotation.W = rotation.W;
            target.Scale.X = scale.X;
            target.Scale.Y = scale.Y;
            target.Scale.Z = scale.Z;
        }
    }

    private BoneFrameSnapshot? SampleFrames(CharacterBase* cBase)
    {
        var frames = new Dictionary<string, BoneFrame>(FaceBoneRegistry.Bones.Length);
        foreach (var bone in FaceBoneRegistry.Bones)
        {
            var pose = TryGetModelPose(cBase, bone.Codename, out var index);
            if (pose != null)
                frames[bone.Codename] = new BoneFrame(Quaternion.Normalize(ToQuaternion(pose[index])));
        }

        if (frames.Count == 0)
            return null;

        var leftEye = TryGetModelPose(cBase, LeftEyeBone, out var leftIndex);
        var rightEye = TryGetModelPose(cBase, RightEyeBone, out var rightIndex);
        var eyeDistance = leftEye != null && rightEye != null
            ? Vector3.Distance(ToPosition(leftEye[leftIndex]), ToPosition(rightEye[rightIndex]))
            : 0f;

        return new BoneFrameSnapshot(frames, eyeDistance);
    }

    /// <summary>
    /// Returns the model pose array holding <paramref name="bone"/> when it is in sync, else null.
    /// </summary>
    private hkQsTransformf* TryGetModelPose(CharacterBase* cBase, string bone, out int index)
    {
        index = 0;
        if (!boneIndex.TryGet(bone, out var location))
            return null;

        var pose = cBase->Skeleton->PartialSkeletons[location.PartialIndex].GetHavokPose(TruePoseIndex);
        if (pose == null || pose->ModelInSync == 0 || location.BoneIndex >= pose->ModelPose.Length)
            return null;

        index = location.BoneIndex;
        return pose->ModelPose.Data;
    }

    private static Quaternion ToQuaternion(in hkQsTransformf transform)
    {
        return new Quaternion(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z, transform.Rotation.W);
    }

    private static Vector3 ToPosition(in hkQsTransformf transform)
    {
        return new Vector3(transform.Translation.X, transform.Translation.Y, transform.Translation.Z);
    }
}
