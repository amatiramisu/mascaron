using Mascaron.Core;

namespace Mascaron.GameBridge;

public interface IBoneApplicator : IDisposable
{
    bool IsAvailable { get; }
    void Apply(BoneTransformState state);
}
