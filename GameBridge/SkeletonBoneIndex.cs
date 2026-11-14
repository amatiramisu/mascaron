using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.Havok.Animation.Rig;

namespace Mascaron.GameBridge;

/// <summary>
/// Maps bone names to (partial skeleton, bone index) for one character skeleton.
/// The map is keyed on the character address and each partial's skeleton resource,
/// so any model swap rebuilds it.
/// </summary>
public sealed unsafe class SkeletonBoneIndex
{
    private const int TruePoseIndex = 0;

    private readonly Dictionary<string, (int PartialIndex, int BoneIndex)> bones = new();
    private nint characterAddress;
    private nint[] resourceHandles = [];
    private bool[] posesLoaded = [];

    public int Count => bones.Count;

    /// <summary>
    /// Rebuilds the map when the skeleton differs from the one it was built for.
    /// Returns true when a rebuild happened.
    /// </summary>
    public bool Refresh(CharacterBase* cBase)
    {
        var skeleton = cBase->Skeleton;
        if (!HasChanged(cBase, skeleton))
            return false;

        characterAddress = (nint)cBase;
        resourceHandles = new nint[skeleton->PartialSkeletonCount];
        posesLoaded = new bool[skeleton->PartialSkeletonCount];
        bones.Clear();

        for (var partialIndex = 0; partialIndex < skeleton->PartialSkeletonCount; partialIndex++)
        {
            var partial = &skeleton->PartialSkeletons[partialIndex];
            resourceHandles[partialIndex] = (nint)partial->SkeletonResourceHandle;

            var pose = partial->GetHavokPose(TruePoseIndex);
            posesLoaded[partialIndex] = IsLoaded(pose);
            if (!posesLoaded[partialIndex])
                continue;

            for (var boneIndex = 0; boneIndex < pose->Skeleton->Bones.Length; boneIndex++)
            {
                var name = pose->Skeleton->Bones[boneIndex].Name.String;
                if (name == null)
                    continue;

                bones[name] = (partialIndex, boneIndex);
            }
        }

        return true;
    }

    /// <summary>
    /// Location of a bone; for names present in several partials the last partial wins.
    /// </summary>
    public bool TryGet(string boneName, out (int PartialIndex, int BoneIndex) location)
    {
        return bones.TryGetValue(boneName, out location);
    }

    private bool HasChanged(CharacterBase* cBase, Skeleton* skeleton)
    {
        if ((nint)cBase != characterAddress || skeleton->PartialSkeletonCount != resourceHandles.Length)
            return true;

        for (var i = 0; i < resourceHandles.Length; i++)
        {
            var partial = &skeleton->PartialSkeletons[i];
            if ((nint)partial->SkeletonResourceHandle != resourceHandles[i] ||
                IsLoaded(partial->GetHavokPose(TruePoseIndex)) != posesLoaded[i])
                return true;
        }

        return false;
    }

    private static bool IsLoaded(hkaPose* pose)
    {
        return pose != null && pose->Skeleton != null;
    }
}
