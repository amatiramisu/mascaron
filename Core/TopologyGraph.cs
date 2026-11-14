namespace Mascaron.Core;

/// <summary>
/// Left/right relationships between face bones: mirror partners and linked pairs that move together.
/// </summary>
public class TopologyGraph
{
    private readonly Dictionary<string, string> mirrors = new();
    private readonly Dictionary<string, string> linkedBones = new()
    {
        ["j_f_eyepuru_l"] = "j_f_eyepuru_r",
        ["j_f_eyepuru_r"] = "j_f_eyepuru_l",
    };

    public TopologyGraph(IEnumerable<FaceBone> bones)
    {
        foreach (var bone in bones)
        {
            if (bone.Mirror != null)
                mirrors[bone.Codename] = bone.Mirror;
        }
    }

    public string? GetMirror(string codename) => mirrors.GetValueOrDefault(codename);

    /// <summary>
    /// Partner that receives the identical (unmirrored) delta when Link Eyes is on.
    /// </summary>
    public string? GetLinkedBone(string codename) => linkedBones.GetValueOrDefault(codename);
}
