using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using Mascaron.Core;

namespace Mascaron.GameBridge;

public unsafe class DirectBoneApplicator : IBoneApplicator
{
    private const string RenderHookSignature = "E8 ?? ?? ?? ?? 48 81 C3 ?? ?? ?? ?? BF ?? ?? ?? ?? 33 ED";
    private const int TruePoseIndex = 0;

    private delegate nint RenderDelegate(nint a1, nint a2, nint a3, int a4);

    private readonly CharacterResolver characterResolver;
    private readonly Hook<RenderDelegate>? renderHook;
    private readonly IPluginLog log;

    private BoneTransformState? pendingState;
    private Dictionary<string, (int PartialIndex, int BoneIndex)> boneIndexMap = new();
    private int lastSkeletonHash;

    public bool IsAvailable { get; private set; }

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
            IsAvailable = false;
        }
    }

    public void Apply(BoneTransformState state)
    {
        pendingState = state;
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
            ApplyTransforms();
        }
        catch (Exception ex)
        {
            log.Error($"Error in render hook: {ex}");
        }

        return renderHook!.Original(a1, a2, a3, a4);
    }

    private void ApplyTransforms()
    {
        if (pendingState == null || pendingState.ModifiedCount == 0)
            return;

        var cBase = characterResolver.GetLocalPlayerCharacterBase();
        if (cBase == null || cBase->Skeleton == null)
            return;

        RebuildIndexMapIfNeeded(cBase);

        foreach (var (boneName, transform) in pendingState.GetModified())
        {
            if (!boneIndexMap.TryGetValue(boneName, out var indices))
                continue;

            var partialSkeleton = &cBase->Skeleton->PartialSkeletons[indices.PartialIndex];
            var pose = partialSkeleton->GetHavokPose(TruePoseIndex);
            if (pose == null || pose->ModelInSync == 0)
                continue;

            if (indices.BoneIndex >= pose->ModelPose.Length)
                continue;

            var currentTransform = pose->ModelPose[indices.BoneIndex];
            currentTransform.Translation.X += transform.Translation.X;
            currentTransform.Translation.Y += transform.Translation.Y;
            currentTransform.Translation.Z += transform.Translation.Z;
            currentTransform.Scale.X *= transform.Scaling.X;
            currentTransform.Scale.Y *= transform.Scaling.Y;
            currentTransform.Scale.Z *= transform.Scaling.Z;

            if (transform.Rotation != System.Numerics.Vector3.Zero)
            {
                var euler = transform.Rotation * (MathF.PI / 180f);
                var rot = Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z);
                var existing = new Quaternion(
                    currentTransform.Rotation.X,
                    currentTransform.Rotation.Y,
                    currentTransform.Rotation.Z,
                    currentTransform.Rotation.W);
                var combined = Quaternion.Multiply(existing, rot);
                currentTransform.Rotation.X = combined.X;
                currentTransform.Rotation.Y = combined.Y;
                currentTransform.Rotation.Z = combined.Z;
                currentTransform.Rotation.W = combined.W;
            }

            pose->ModelPose.Data[indices.BoneIndex] = currentTransform;
        }
    }

    private void RebuildIndexMapIfNeeded(CharacterBase* cBase)
    {
        var hash = ComputeSkeletonHash(cBase);
        if (hash == lastSkeletonHash)
            return;

        lastSkeletonHash = hash;
        boneIndexMap.Clear();

        for (var pIdx = 0; pIdx < cBase->Skeleton->PartialSkeletonCount; pIdx++)
        {
            var pose = cBase->Skeleton->PartialSkeletons[pIdx].GetHavokPose(TruePoseIndex);
            if (pose == null || pose->Skeleton == null)
                continue;

            for (var bIdx = 0; bIdx < pose->Skeleton->Bones.Length; bIdx++)
            {
                var name = pose->Skeleton->Bones[bIdx].Name.String;
                if (name != null)
                    boneIndexMap[name] = (pIdx, bIdx);
            }
        }

        log.Debug($"Rebuilt bone index map: {boneIndexMap.Count} bones across {cBase->Skeleton->PartialSkeletonCount} partials");
    }

    private static int ComputeSkeletonHash(CharacterBase* cBase)
    {
        var hash = (int)cBase->Skeleton->PartialSkeletonCount;
        for (var i = 0; i < cBase->Skeleton->PartialSkeletonCount; i++)
        {
            var pose = cBase->Skeleton->PartialSkeletons[i].GetHavokPose(TruePoseIndex);
            if (pose != null && pose->Skeleton != null)
                hash = HashCode.Combine(hash, pose->Skeleton->Bones.Length);
        }
        return hash;
    }
}
