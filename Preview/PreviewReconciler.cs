using System.Numerics;
using Mascaron.Core;

namespace Mascaron.Preview;

/// <summary>
/// Projects the editable sculpt into the transforms Mascaron still needs to preview
/// after Customize+ adopts an exported sculpt.
/// </summary>
public sealed class PreviewReconciler
{
    private const float TransformEpsilon = 0.0001f;

    private readonly BoneTransformState workspace;
    private readonly BoneTransformState previewState = new();

    private Dictionary<string, BoneTransform> handoffSnapshot = [];
    private Dictionary<string, BoneTransform> effectiveCustomizePlusTransforms = [];
    private bool hasObservedProfileUpdate;
    private int reconciliationVersion;
    private int projectedWorkspaceVersion = -1;
    private int projectedReconciliationVersion = -1;
    private int mascaronOwnedBoneCount;

    /// <summary>
    /// Creates a preview projection over the given editable workspace.
    /// </summary>
    public PreviewReconciler(BoneTransformState workspace)
    {
        this.workspace = workspace;
    }

    /// <summary>
    /// The filtered state that should be written by Mascaron's direct preview.
    /// </summary>
    public BoneTransformState PreviewState
    {
        get
        {
            RebuildProjectionIfNeeded();
            return previewState;
        }
    }

    /// <summary>
    /// Number of workspace bones that still receive any transform from Mascaron
    /// after the latest Customize+ handoff reconciliation.
    /// </summary>
    public int MascaronOwnedBoneCount
    {
        get
        {
            RebuildProjectionIfNeeded();
            return hasObservedProfileUpdate ? mascaronOwnedBoneCount : 0;
        }
    }

    /// <summary>
    /// Captures the transforms included in the latest Customize+ export.
    /// </summary>
    public void BeginHandoff()
    {
        handoffSnapshot = workspace.GetModified().ToDictionary(entry => entry.Key, entry => entry.Value);
        effectiveCustomizePlusTransforms.Clear();
        hasObservedProfileUpdate = false;
        InvalidateProjection();
    }

    /// <summary>
    /// Treats a state imported from the active Customize+ profile as already adopted.
    /// </summary>
    public void AdoptImportedState(BoneTransformState importedState)
    {
        BeginHandoff();
        ReconcileWith(importedState);
    }

    /// <summary>
    /// Reconciles the armed export against Customize+'s effective active-profile state.
    /// </summary>
    public void ReconcileWith(BoneTransformState effectiveProfileState)
    {
        if (handoffSnapshot.Count == 0)
            return;

        effectiveCustomizePlusTransforms = effectiveProfileState.GetModified()
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        hasObservedProfileUpdate = true;
        InvalidateProjection();
    }

    /// <summary>
    /// Discards handoff bookkeeping while preserving the workspace itself.
    /// </summary>
    public void CancelHandoff()
    {
        handoffSnapshot.Clear();
        effectiveCustomizePlusTransforms.Clear();
        hasObservedProfileUpdate = false;
        InvalidateProjection();
    }

    private void RebuildProjectionIfNeeded()
    {
        if (projectedWorkspaceVersion == workspace.Version &&
            projectedReconciliationVersion == reconciliationVersion)
            return;

        previewState.ResetAll();
        mascaronOwnedBoneCount = 0;

        foreach (var (boneName, workspaceTransform) in workspace.GetModified())
        {
            var projectedTransform = ProjectTransform(boneName, workspaceTransform);
            if (!projectedTransform.IsModified)
                continue;

            previewState.Set(boneName, projectedTransform);
            mascaronOwnedBoneCount++;
        }

        projectedWorkspaceVersion = workspace.Version;
        projectedReconciliationVersion = reconciliationVersion;
    }

    private BoneTransform ProjectTransform(string boneName, BoneTransform workspaceTransform)
    {
        if (!hasObservedProfileUpdate ||
            !handoffSnapshot.TryGetValue(boneName, out var handoffTransform) ||
            !effectiveCustomizePlusTransforms.TryGetValue(boneName, out var customizePlusTransform))
            return workspaceTransform;

        var projected = workspaceTransform;

        if (handoffTransform.Translation != Vector3.Zero &&
            ApproximatelyEqual(workspaceTransform.Translation, customizePlusTransform.Translation))
            projected.Translation = Vector3.Zero;

        if (handoffTransform.Rotation != Vector3.Zero &&
            ApproximatelyEqualRotation(workspaceTransform.Rotation, customizePlusTransform.Rotation))
            projected.Rotation = Vector3.Zero;

        if (handoffTransform.Scaling != Vector3.One &&
            ApproximatelyEqual(workspaceTransform.Scaling, customizePlusTransform.Scaling))
            projected.Scaling = Vector3.One;

        return projected;
    }

    private void InvalidateProjection()
    {
        reconciliationVersion++;
    }

    private static bool ApproximatelyEqual(Vector3 left, Vector3 right)
    {
        return MathF.Abs(left.X - right.X) <= TransformEpsilon &&
               MathF.Abs(left.Y - right.Y) <= TransformEpsilon &&
               MathF.Abs(left.Z - right.Z) <= TransformEpsilon;
    }

    private static bool ApproximatelyEqualRotation(Vector3 left, Vector3 right)
    {
        return AngularDistance(left.X, right.X) <= TransformEpsilon &&
               AngularDistance(left.Y, right.Y) <= TransformEpsilon &&
               AngularDistance(left.Z, right.Z) <= TransformEpsilon;
    }

    private static float AngularDistance(float left, float right)
    {
        var difference = MathF.Abs((left - right) % 360f);
        return MathF.Min(difference, 360f - difference);
    }
}
