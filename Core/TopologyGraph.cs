namespace Mascaron.Core;

public class TopologyGraph
{
    private readonly Dictionary<string, List<string>> children = new();
    private readonly Dictionary<string, string?> parents = new();
    private readonly Dictionary<string, string?> mirrors = new();
    private readonly Dictionary<string, string?> linkedBones = new();
    private readonly Dictionary<string, HashSet<string>> reachableSets = new();

    public TopologyGraph(FaceBone[] bones)
    {
        foreach (var bone in bones)
        {
            parents[bone.Codename] = bone.Parent;
            mirrors[bone.Codename] = bone.Mirror;
            linkedBones[bone.Codename] = bone.Codename switch
            {
                "j_f_eyepuru_l" => "j_f_eyepuru_r",
                "j_f_eyepuru_r" => "j_f_eyepuru_l",
                _ => null,
            };

            if (bone.Parent != null)
            {
                if (!children.ContainsKey(bone.Parent))
                    children[bone.Parent] = [];
                children[bone.Parent].Add(bone.Codename);
            }
        }

        foreach (var bone in bones)
            reachableSets[bone.Codename] = BuildReachableSet(bone.Codename);
    }

    public string? GetParent(string codename) => parents.GetValueOrDefault(codename);

    public string? GetMirror(string codename) => mirrors.GetValueOrDefault(codename);

    public string? GetLinkedBone(string codename) => linkedBones.GetValueOrDefault(codename);

    public IReadOnlyList<string> GetChildren(string codename)
    {
        return children.GetValueOrDefault(codename) ?? (IReadOnlyList<string>)[];
    }

    public bool IsReachable(string from, string to)
    {
        if (from == to)
            return true;
        return reachableSets.TryGetValue(from, out var set) && set.Contains(to);
    }

    private HashSet<string> BuildReachableSet(string startBone, int maxDepth = 6)
    {
        var result = new HashSet<string>();
        var visited = new HashSet<string> { startBone, "j_f_face" };
        var queue = new Queue<(string Codename, int Depth)>();

        var parent = GetParent(startBone);
        if (parent != null && visited.Add(parent))
            queue.Enqueue((parent, 1));

        foreach (var child in GetChildren(startBone))
        {
            if (visited.Add(child))
                queue.Enqueue((child, 1));
        }

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            result.Add(current);

            if (depth >= maxDepth)
                continue;

            var p = GetParent(current);
            if (p != null && visited.Add(p))
                queue.Enqueue((p, depth + 1));

            foreach (var child in GetChildren(current))
            {
                if (visited.Add(child))
                    queue.Enqueue((child, depth + 1));
            }
        }

        return result;
    }
}
