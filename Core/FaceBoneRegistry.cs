using System.Numerics;

namespace Mascaron.Core;

public enum BoneRegion
{
    Eyes,
    Eyelids,
    Brows,
    Glabella,
    Nose,
    Cheeks,
    Lips,
    Jaw,
    Hrothgar,
}

public record FaceBone(
    string Codename,
    string DisplayName,
    BoneRegion Region,
    string? Parent,
    string? Mirror,
    Vector2 CanvasPosition);

public static class FaceBoneRegistry
{
    public static FaceBone[] Bones { get; } = BuildRegistry();

    private static readonly Dictionary<string, FaceBone> ByCodename = new();

    static FaceBoneRegistry()
    {
        foreach (var bone in Bones)
            ByCodename[bone.Codename] = bone;
    }

    public static FaceBone? GetByCodename(string codename)
    {
        return ByCodename.GetValueOrDefault(codename);
    }

    private static FaceBone[] BuildRegistry()
    {
        // Dawntrail face skeleton — positions normalized to [0,1] on a 600x600 canvas.
        // Coordinates derived from Anamnesis PoseFaceGUIView.xaml DT layout.
        return
        [
            // Eyes
            B("j_f_eye_l", "Eye Left", BoneRegion.Eyes, "j_f_face", "j_f_eye_r", 0.35f, 0.38f),
            B("j_f_eye_r", "Eye Right", BoneRegion.Eyes, "j_f_face", "j_f_eye_l", 0.65f, 0.38f),
            B("j_f_mab_l", "Eye Socket Left", BoneRegion.Eyes, "j_f_face", "j_f_mab_r", 0.34f, 0.37f),
            B("j_f_mab_r", "Eye Socket Right", BoneRegion.Eyes, "j_f_face", "j_f_mab_l", 0.66f, 0.37f),
            B("j_f_eyepuru_l", "Iris Left", BoneRegion.Eyes, "j_f_face", "j_f_eyepuru_r", 0.35f, 0.39f),
            B("j_f_eyepuru_r", "Iris Right", BoneRegion.Eyes, "j_f_face", "j_f_eyepuru_l", 0.65f, 0.39f),

            // Upper eyelids
            B("j_f_mabup_01_l", "Upper Eyelid Left", BoneRegion.Eyelids, "j_f_face", "j_f_mabup_01_r", 0.35f, 0.35f),
            B("j_f_mabup_01_r", "Upper Eyelid Right", BoneRegion.Eyelids, "j_f_face", "j_f_mabup_01_l", 0.65f, 0.35f),
            B("j_f_mabup_02out_l", "Upper Eyelid Outer Left", BoneRegion.Eyelids, "j_f_face", "j_f_mabup_02out_r", 0.30f, 0.36f),
            B("j_f_mabup_02out_r", "Upper Eyelid Outer Right", BoneRegion.Eyelids, "j_f_face", "j_f_mabup_02out_l", 0.70f, 0.36f),
            B("j_f_mabup_03in_l", "Upper Eyelid Inner Left", BoneRegion.Eyelids, "j_f_face", "j_f_mabup_03in_r", 0.40f, 0.36f),
            B("j_f_mabup_03in_r", "Upper Eyelid Inner Right", BoneRegion.Eyelids, "j_f_face", "j_f_mabup_03in_l", 0.60f, 0.36f),

            // Lower eyelids
            B("j_f_mabdn_01_l", "Lower Eyelid Left", BoneRegion.Eyelids, "j_f_face", "j_f_mabdn_01_r", 0.35f, 0.41f),
            B("j_f_mabdn_01_r", "Lower Eyelid Right", BoneRegion.Eyelids, "j_f_face", "j_f_mabdn_01_l", 0.65f, 0.41f),
            B("j_f_mabdn_02out_l", "Lower Eyelid Outer Left", BoneRegion.Eyelids, "j_f_face", "j_f_mabdn_02out_r", 0.30f, 0.40f),
            B("j_f_mabdn_02out_r", "Lower Eyelid Outer Right", BoneRegion.Eyelids, "j_f_face", "j_f_mabdn_02out_l", 0.70f, 0.40f),
            B("j_f_mabdn_03in_l", "Lower Eyelid Inner Left", BoneRegion.Eyelids, "j_f_face", "j_f_mabdn_03in_r", 0.40f, 0.40f),
            B("j_f_mabdn_03in_r", "Lower Eyelid Inner Right", BoneRegion.Eyelids, "j_f_face", "j_f_mabdn_03in_l", 0.60f, 0.40f),

            // Brows
            B("j_f_mayu_l", "Brow Outer Left", BoneRegion.Brows, "j_f_face", "j_f_mayu_r", 0.28f, 0.30f),
            B("j_f_mayu_r", "Brow Outer Right", BoneRegion.Brows, "j_f_face", "j_f_mayu_l", 0.72f, 0.30f),
            B("j_f_mmayu_l", "Brow Mid Left", BoneRegion.Brows, "j_f_face", "j_f_mmayu_r", 0.37f, 0.29f),
            B("j_f_mmayu_r", "Brow Mid Right", BoneRegion.Brows, "j_f_face", "j_f_mmayu_l", 0.63f, 0.29f),
            B("j_f_miken_01_l", "Brow Ridge Left", BoneRegion.Brows, "j_f_mmayu_l", "j_f_miken_01_r", 0.43f, 0.30f),
            B("j_f_miken_01_r", "Brow Ridge Right", BoneRegion.Brows, "j_f_mmayu_r", "j_f_miken_01_l", 0.57f, 0.30f),
            B("j_f_miken_02_l", "Brow Ridge B Left", BoneRegion.Brows, "j_f_miken_01_l", "j_f_miken_02_r", 0.45f, 0.31f),
            B("j_f_miken_02_r", "Brow Ridge B Right", BoneRegion.Brows, "j_f_miken_01_r", "j_f_miken_02_l", 0.55f, 0.31f),

            // Glabella
            B("j_f_dmiken_l", "Glabella Left", BoneRegion.Glabella, "j_f_face", "j_f_dmiken_r", 0.46f, 0.34f),
            B("j_f_dmiken_r", "Glabella Right", BoneRegion.Glabella, "j_f_face", "j_f_dmiken_l", 0.54f, 0.34f),

            // Nose
            B("j_f_hana_l", "Nose Left", BoneRegion.Nose, "j_f_face", "j_f_hana_r", 0.46f, 0.51f),
            B("j_f_hana_r", "Nose Right", BoneRegion.Nose, "j_f_face", "j_f_hana_l", 0.54f, 0.51f),
            B("j_f_uhana", "Nose Bridge", BoneRegion.Nose, "j_f_face", null, 0.50f, 0.44f),

            // Cheeks
            B("j_f_hoho_l", "Cheek Left", BoneRegion.Cheeks, "j_f_face", "j_f_hoho_r", 0.28f, 0.50f),
            B("j_f_hoho_r", "Cheek Right", BoneRegion.Cheeks, "j_f_face", "j_f_hoho_l", 0.72f, 0.50f),
            B("j_f_dhoho_l", "Outer Cheek Left", BoneRegion.Cheeks, "j_f_face", "j_f_dhoho_r", 0.22f, 0.48f),
            B("j_f_dhoho_r", "Outer Cheek Right", BoneRegion.Cheeks, "j_f_face", "j_f_dhoho_l", 0.78f, 0.48f),
            B("j_f_shoho_l", "Middle Cheek Left", BoneRegion.Cheeks, "j_f_face", "j_f_shoho_r", 0.25f, 0.44f),
            B("j_f_shoho_r", "Middle Cheek Right", BoneRegion.Cheeks, "j_f_face", "j_f_shoho_l", 0.75f, 0.44f),
            B("j_f_dmemoto_l", "Inner Cheek Left", BoneRegion.Cheeks, "j_f_face", "j_f_dmemoto_r", 0.38f, 0.46f),
            B("j_f_dmemoto_r", "Inner Cheek Right", BoneRegion.Cheeks, "j_f_face", "j_f_dmemoto_l", 0.62f, 0.46f),

            // Lips (Dawntrail 16-bone topology)
            B("j_f_umlip_01_l", "Lip Upper Left A", BoneRegion.Lips, "j_f_ago", "j_f_umlip_01_r", 0.44f, 0.60f),
            B("j_f_umlip_02_l", "Lip Upper Left B", BoneRegion.Lips, "j_f_ago", "j_f_umlip_02_r", 0.42f, 0.61f),
            B("j_f_umlip_01_r", "Lip Upper Right A", BoneRegion.Lips, "j_f_ago", "j_f_umlip_01_l", 0.56f, 0.60f),
            B("j_f_umlip_02_r", "Lip Upper Right B", BoneRegion.Lips, "j_f_ago", "j_f_umlip_02_l", 0.58f, 0.61f),
            B("j_f_dmlip_01_l", "Lip Lower Left A", BoneRegion.Lips, "j_f_ago", "j_f_dmlip_01_r", 0.44f, 0.66f),
            B("j_f_dmlip_02_l", "Lip Lower Left B", BoneRegion.Lips, "j_f_ago", "j_f_dmlip_02_r", 0.42f, 0.65f),
            B("j_f_dmlip_01_r", "Lip Lower Right A", BoneRegion.Lips, "j_f_ago", "j_f_dmlip_01_l", 0.56f, 0.66f),
            B("j_f_dmlip_02_r", "Lip Lower Right B", BoneRegion.Lips, "j_f_ago", "j_f_dmlip_02_l", 0.58f, 0.65f),
            B("j_f_ulip_01_l", "Lip Upper Center Left A", BoneRegion.Lips, "j_f_ago", "j_f_ulip_01_r", 0.47f, 0.59f),
            B("j_f_ulip_02_l", "Lip Upper Center Left B", BoneRegion.Lips, "j_f_ago", "j_f_ulip_02_r", 0.48f, 0.58f),
            B("j_f_ulip_01_r", "Lip Upper Center Right A", BoneRegion.Lips, "j_f_ago", "j_f_ulip_01_l", 0.53f, 0.59f),
            B("j_f_ulip_02_r", "Lip Upper Center Right B", BoneRegion.Lips, "j_f_ago", "j_f_ulip_02_l", 0.52f, 0.58f),
            B("j_f_dlip_01_l", "Lip Lower Center Left A", BoneRegion.Lips, "j_f_ago", "j_f_dlip_01_r", 0.47f, 0.67f),
            B("j_f_dlip_02_l", "Lip Lower Center Left B", BoneRegion.Lips, "j_f_ago", "j_f_dlip_02_r", 0.48f, 0.68f),
            B("j_f_dlip_01_r", "Lip Lower Center Right A", BoneRegion.Lips, "j_f_ago", "j_f_dlip_01_l", 0.53f, 0.67f),
            B("j_f_dlip_02_r", "Lip Lower Center Right B", BoneRegion.Lips, "j_f_ago", "j_f_dlip_02_l", 0.52f, 0.68f),
            B("j_f_uslip_l", "Lip Corner Upper Left", BoneRegion.Lips, "j_f_ago", "j_f_uslip_r", 0.38f, 0.62f),
            B("j_f_uslip_r", "Lip Corner Upper Right", BoneRegion.Lips, "j_f_ago", "j_f_uslip_l", 0.62f, 0.62f),
            B("j_f_dslip_l", "Lip Corner Lower Left", BoneRegion.Lips, "j_f_ago", "j_f_dslip_r", 0.38f, 0.64f),
            B("j_f_dslip_r", "Lip Corner Lower Right", BoneRegion.Lips, "j_f_ago", "j_f_dslip_l", 0.62f, 0.64f),

            // Jaw
            B("j_ago", "Jaw", BoneRegion.Jaw, "j_f_face", null, 0.50f, 0.70f),
            B("j_f_ago", "Jaw Front", BoneRegion.Jaw, "j_f_face", null, 0.50f, 0.72f),
            B("j_f_dago", "Chin", BoneRegion.Jaw, "j_f_face", null, 0.50f, 0.78f),

            // Hrothgar-specific
            B("j_f_hige_l", "Whisker Left", BoneRegion.Hrothgar, "j_f_face", "j_f_hige_r", 0.22f, 0.55f),
            B("j_f_hige_r", "Whisker Right", BoneRegion.Hrothgar, "j_f_face", "j_f_hige_l", 0.78f, 0.55f),
        ];
    }

    private static FaceBone B(string codename, string displayName, BoneRegion region,
        string? parent, string? mirror, float x, float y)
    {
        return new FaceBone(codename, displayName, region, parent, mirror, new Vector2(x, y));
    }
}
