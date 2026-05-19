using System.Numerics;

namespace Mascaron.Visualization;

public enum FaceTemplate
{
    Standard,
    Hrothgar,
    Miqote,
    Viera,
}

public static class RaceTemplates
{
    public static FaceTemplate FromRace(byte race, byte tribe)
    {
        return race switch
        {
            7 => FaceTemplate.Hrothgar,
            4 => FaceTemplate.Miqote,
            8 => FaceTemplate.Viera,
            _ => FaceTemplate.Standard,
        };
    }

    public static Vector2? GetPosition(FaceTemplate template, string boneName)
    {
        var map = template switch
        {
            FaceTemplate.Hrothgar => Hrothgar,
            FaceTemplate.Miqote => Miqote,
            FaceTemplate.Viera => Viera,
            _ => Standard,
        };

        return map.TryGetValue(boneName, out var pos) ? pos : null;
    }

    public static string GetRaceName(byte race)
    {
        return race switch
        {
            1 => "Hyur",
            2 => "Elezen",
            3 => "Lalafell",
            4 => "Miqo'te",
            5 => "Roegadyn",
            6 => "Au Ra",
            7 => "Hrothgar",
            8 => "Viera",
            _ => "Unknown",
        };
    }

    public static string GetBackgroundFileName(FaceTemplate template)
    {
        return template switch
        {
            FaceTemplate.Hrothgar => "Hrothgar.png",
            FaceTemplate.Miqote => "Miqote.png",
            FaceTemplate.Viera => "Viera.png",
            _ => "Standard.png",
        };
    }

    // All coordinates extracted from Anamnesis PoseFaceGUIView.xaml DT layout,
    // normalized from 600x600 canvas to [0,1].
    // Lip bones mapped from the separate 220x300 mouth panel into the face canvas mouth region.

    private const float BoneViewHalfSize = 9f;
    private static Vector2 P(float x, float y) => new((x + BoneViewHalfSize) / 600f, (y + BoneViewHalfSize) / 600f);

    private static readonly Dictionary<string, Vector2> Standard = BuildStandard();
    private static readonly Dictionary<string, Vector2> Hrothgar = BuildHrothgar();
    private static readonly Dictionary<string, Vector2> Miqote = BuildMiqote();
    private static readonly Dictionary<string, Vector2> Viera = BuildViera();

    private static Dictionary<string, Vector2> BuildStandard()
    {
        var d = new Dictionary<string, Vector2>();

        // Eyes (shared, always visible)
        d["j_f_eye_l"] = P(224, 295);
        d["j_f_eye_r"] = P(382, 295);
        d["j_f_mab_l"] = P(222, 295);
        d["j_f_mab_r"] = P(384, 295);
        d["j_f_eyepuru_l"] = P(224, 295);
        d["j_f_eyepuru_r"] = P(382, 295);

        // Upper eyelids
        d["j_f_mabup_01_l"] = P(224, 275);
        d["j_f_mabup_01_r"] = P(382, 275);
        d["j_f_mabup_02out_l"] = P(176, 288);
        d["j_f_mabup_02out_r"] = P(430, 288);
        d["j_f_mabup_03in_l"] = P(262, 292);
        d["j_f_mabup_03in_r"] = P(348, 292);

        // Lower eyelids
        d["j_f_mabdn_01_l"] = P(224, 320);
        d["j_f_mabdn_01_r"] = P(382, 320);
        d["j_f_mabdn_02out_l"] = P(176, 306);
        d["j_f_mabdn_02out_r"] = P(430, 306);
        d["j_f_mabdn_03in_l"] = P(262, 310);
        d["j_f_mabdn_03in_r"] = P(348, 310);

        // Brows
        d["j_f_mayu_l"] = P(170, 250);
        d["j_f_mayu_r"] = P(435, 250);
        d["j_f_mmayu_l"] = P(212, 240);
        d["j_f_mmayu_r"] = P(394, 240);
        d["j_f_miken_01_l"] = P(250, 250);
        d["j_f_miken_01_r"] = P(355, 250);
        d["j_f_miken_02_l"] = P(280, 270);
        d["j_f_miken_02_r"] = P(330, 270);

        // Glabella
        d["j_f_dmiken_l"] = P(290, 300);
        d["j_f_dmiken_r"] = P(320, 300);

        // Nose

        d["j_f_uhana"] = P(305, 340);
        d["j_f_hana_l"] = P(285, 390);
        d["j_f_hana_r"] = P(325, 390);

        // Cheeks (shared + DT)
        d["j_f_hoho_l"] = P(220, 380);
        d["j_f_hoho_r"] = P(386, 380);
        d["j_f_dhoho_l"] = P(170, 420);
        d["j_f_dhoho_r"] = P(438, 420);
        d["j_f_shoho_l"] = P(220, 430);
        d["j_f_shoho_r"] = P(386, 430);
        d["j_f_dmemoto_l"] = P(260, 350);
        d["j_f_dmemoto_r"] = P(350, 350);

        // Teeth
        // Jaw
        d["j_ago"] = P(305, 508);
        d["j_f_ago"] = P(305, 508);
        d["j_f_dago"] = P(305, 538);

        AddLipPositions(d, 305);

        return d;
    }

    private static Dictionary<string, Vector2> BuildHrothgar()
    {
        var d = new Dictionary<string, Vector2>();

        // Eyes
        d["j_f_eye_l"] = P(200, 284);
        d["j_f_eye_r"] = P(370, 284);
        d["j_f_mab_l"] = P(198, 284);
        d["j_f_mab_r"] = P(372, 284);
        d["j_f_eyepuru_l"] = P(200, 284);
        d["j_f_eyepuru_r"] = P(370, 284);

        // Upper eyelids
        d["j_f_mabup_01_l"] = P(200, 265);
        d["j_f_mabup_01_r"] = P(370, 265);
        d["j_f_mabup_02out_l"] = P(156, 260);
        d["j_f_mabup_02out_r"] = P(416, 260);
        d["j_f_mabup_03in_l"] = P(240, 290);
        d["j_f_mabup_03in_r"] = P(330, 290);

        // Lower eyelids
        d["j_f_mabdn_01_l"] = P(190, 305);
        d["j_f_mabdn_01_r"] = P(380, 305);
        d["j_f_mabdn_02out_l"] = P(156, 280);
        d["j_f_mabdn_02out_r"] = P(416, 280);
        d["j_f_mabdn_03in_l"] = P(230, 305);
        d["j_f_mabdn_03in_r"] = P(340, 305);

        // Brows
        d["j_f_mayu_l"] = P(150, 230);
        d["j_f_mayu_r"] = P(420, 230);
        d["j_f_mmayu_l"] = P(185, 235);
        d["j_f_mmayu_r"] = P(385, 235);
        d["j_f_miken_01_l"] = P(220, 240);
        d["j_f_miken_01_r"] = P(350, 240);
        d["j_f_miken_02_l"] = P(255, 260);
        d["j_f_miken_02_r"] = P(315, 260);

        // Glabella
        d["j_f_dmiken_l"] = P(270, 290);
        d["j_f_dmiken_r"] = P(300, 290);

        // Nose
        d["j_f_uhana"] = P(285, 320);
        d["j_f_hana_l"] = P(260, 370);
        d["j_f_hana_r"] = P(310, 370);

        // Cheeks
        d["j_f_hoho_l"] = P(170, 340);
        d["j_f_hoho_r"] = P(400, 340);
        d["j_f_dhoho_l"] = P(135, 420);
        d["j_f_dhoho_r"] = P(440, 420);
        d["j_f_shoho_l"] = P(170, 395);
        d["j_f_shoho_r"] = P(400, 395);
        d["j_f_dmemoto_l"] = P(210, 340);
        d["j_f_dmemoto_r"] = P(360, 340);

        // Teeth
        // Jaw
        d["j_ago"] = P(285, 504);
        d["j_f_ago"] = P(285, 504);
        d["j_f_dago"] = P(285, 538);

        // Whiskers
        d["j_f_hige_l"] = P(120, 366);
        d["j_f_hige_r"] = P(450, 366);

        AddLipPositions(d, 285);

        return d;
    }

    private static Dictionary<string, Vector2> BuildMiqote()
    {
        var d = new Dictionary<string, Vector2>();

        // Eyes (shared)
        d["j_f_eye_l"] = P(218, 322);
        d["j_f_eye_r"] = P(380, 322);
        d["j_f_mab_l"] = P(216, 322);
        d["j_f_mab_r"] = P(382, 322);
        d["j_f_eyepuru_l"] = P(218, 322);
        d["j_f_eyepuru_r"] = P(380, 322);

        // Upper eyelids
        d["j_f_mabup_01_l"] = P(220, 302);
        d["j_f_mabup_01_r"] = P(378, 302);
        d["j_f_mabup_02out_l"] = P(174, 314);
        d["j_f_mabup_02out_r"] = P(422, 314);
        d["j_f_mabup_03in_l"] = P(250, 320);
        d["j_f_mabup_03in_r"] = P(348, 320);

        // Lower eyelids
        d["j_f_mabdn_01_l"] = P(212, 344);
        d["j_f_mabdn_01_r"] = P(386, 344);
        d["j_f_mabdn_02out_l"] = P(174, 334);
        d["j_f_mabdn_02out_r"] = P(424, 334);
        d["j_f_mabdn_03in_l"] = P(250, 340);
        d["j_f_mabdn_03in_r"] = P(348, 340);

        // Brows
        d["j_f_mayu_l"] = P(180, 280);
        d["j_f_mayu_r"] = P(425, 280);
        d["j_f_mmayu_l"] = P(210, 270);
        d["j_f_mmayu_r"] = P(395, 270);
        d["j_f_miken_01_l"] = P(250, 280);
        d["j_f_miken_01_r"] = P(355, 280);
        d["j_f_miken_02_l"] = P(280, 294);
        d["j_f_miken_02_r"] = P(320, 294);

        // Glabella
        d["j_f_dmiken_l"] = P(290, 320);
        d["j_f_dmiken_r"] = P(310, 320);

        // Nose
        d["j_f_uhana"] = P(300, 350);
        d["j_f_hana_l"] = P(284, 404);
        d["j_f_hana_r"] = P(316, 404);

        // Cheeks (shared + DT)
        d["j_f_hoho_l"] = P(210, 380);
        d["j_f_hoho_r"] = P(390, 380);
        d["j_f_dhoho_l"] = P(160, 410);
        d["j_f_dhoho_r"] = P(440, 410);
        d["j_f_shoho_l"] = P(210, 440);
        d["j_f_shoho_r"] = P(390, 440);
        d["j_f_dmemoto_l"] = P(260, 370);
        d["j_f_dmemoto_r"] = P(340, 370);

        // Teeth
        // Jaw
        d["j_ago"] = P(300, 502);
        d["j_f_ago"] = P(300, 502);
        d["j_f_dago"] = P(300, 538);

        AddLipPositions(d, 300);

        return d;
    }

    private static Dictionary<string, Vector2> BuildViera()
    {
        var d = new Dictionary<string, Vector2>();

        // Eyes
        d["j_f_eye_l"] = P(226, 370);
        d["j_f_eye_r"] = P(354, 370);
        d["j_f_mab_l"] = P(224, 370);
        d["j_f_mab_r"] = P(356, 370);
        d["j_f_eyepuru_l"] = P(226, 370);
        d["j_f_eyepuru_r"] = P(354, 370);

        // Upper eyelids
        d["j_f_mabup_01_l"] = P(226, 350);
        d["j_f_mabup_01_r"] = P(354, 350);
        d["j_f_mabup_02out_l"] = P(188, 355);
        d["j_f_mabup_02out_r"] = P(392, 355);
        d["j_f_mabup_03in_l"] = P(258, 370);
        d["j_f_mabup_03in_r"] = P(324, 370);

        // Lower eyelids
        d["j_f_mabdn_01_l"] = P(226, 390);
        d["j_f_mabdn_01_r"] = P(354, 390);
        d["j_f_mabdn_02out_l"] = P(188, 374);
        d["j_f_mabdn_02out_r"] = P(392, 374);
        d["j_f_mabdn_03in_l"] = P(258, 388);
        d["j_f_mabdn_03in_r"] = P(324, 388);

        // Brows
        d["j_f_mayu_l"] = P(180, 320);
        d["j_f_mayu_r"] = P(400, 320);
        d["j_f_mmayu_l"] = P(216, 315);
        d["j_f_mmayu_r"] = P(368, 315);
        d["j_f_miken_01_l"] = P(246, 320);
        d["j_f_miken_01_r"] = P(338, 320);
        d["j_f_miken_02_l"] = P(272, 335);
        d["j_f_miken_02_r"] = P(312, 335);

        // Glabella
        d["j_f_dmiken_l"] = P(282, 355);
        d["j_f_dmiken_r"] = P(302, 355);

        // Nose
        d["j_f_uhana"] = P(292, 390);
        d["j_f_hana_l"] = P(272, 430);
        d["j_f_hana_r"] = P(312, 430);

        // Cheeks
        d["j_f_hoho_l"] = P(210, 430);
        d["j_f_hoho_r"] = P(372, 430);
        d["j_f_dhoho_l"] = P(186, 468);
        d["j_f_dhoho_r"] = P(396, 468);
        d["j_f_shoho_l"] = P(230, 470);
        d["j_f_shoho_r"] = P(354, 470);
        d["j_f_dmemoto_l"] = P(242, 414);
        d["j_f_dmemoto_r"] = P(342, 414);

        // Teeth
        // Jaw
        d["j_ago"] = P(292, 520);
        d["j_f_ago"] = P(292, 520);
        d["j_f_dago"] = P(292, 546);

        AddLipPositions(d, 292);

        return d;
    }

    private static void AddLipPositions(Dictionary<string, Vector2> d, float centerX)
    {
        // Lip bones derived from Anamnesis mouth panel (220x300 canvas).
        // Mapped into face canvas mouth region: ±65px wide from center, ~40px tall.
        float Lx(float panelX) => centerX + (panelX - 100f) * 0.867f;
        float Ly(float baseY, float panelY) => baseY + (panelY - 130f) * 0.75f;
        float baseY = centerX == 305 ? 475 : centerX == 285 ? 470 : centerX == 300 ? 478 : 500;

        // Lip corners
        d["j_f_uslip_l"] = P(Lx(25), Ly(baseY, 115));
        d["j_f_uslip_r"] = P(Lx(175), Ly(baseY, 115));
        d["j_f_dslip_l"] = P(Lx(25), Ly(baseY, 145));
        d["j_f_dslip_r"] = P(Lx(175), Ly(baseY, 145));

        // Upper lip outer
        d["j_f_umlip_02_l"] = P(Lx(58), Ly(baseY, 115));
        d["j_f_umlip_02_r"] = P(Lx(142), Ly(baseY, 115));
        d["j_f_umlip_01_l"] = P(Lx(58), Ly(baseY, 90));
        d["j_f_umlip_01_r"] = P(Lx(142), Ly(baseY, 90));

        // Upper lip inner
        d["j_f_ulip_02_l"] = P(Lx(85), Ly(baseY, 115));
        d["j_f_ulip_02_r"] = P(Lx(115), Ly(baseY, 115));
        d["j_f_ulip_01_l"] = P(Lx(85), Ly(baseY, 90));
        d["j_f_ulip_01_r"] = P(Lx(115), Ly(baseY, 90));

        // Lower lip outer
        d["j_f_dmlip_02_l"] = P(Lx(58), Ly(baseY, 145));
        d["j_f_dmlip_02_r"] = P(Lx(142), Ly(baseY, 145));
        d["j_f_dmlip_01_l"] = P(Lx(58), Ly(baseY, 170));
        d["j_f_dmlip_01_r"] = P(Lx(142), Ly(baseY, 170));

        // Lower lip inner
        d["j_f_dlip_02_l"] = P(Lx(85), Ly(baseY, 145));
        d["j_f_dlip_02_r"] = P(Lx(115), Ly(baseY, 145));
        d["j_f_dlip_01_l"] = P(Lx(85), Ly(baseY, 170));
        d["j_f_dlip_01_r"] = P(Lx(115), Ly(baseY, 170));
    }

}
