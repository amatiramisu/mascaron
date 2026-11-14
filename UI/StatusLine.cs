namespace Mascaron.UI;

/// <summary>
/// A transient status message that fades out over its last half second.
/// </summary>
public sealed class StatusLine
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(4);
    private const float FadeSeconds = 0.5f;

    private DateTime expiry;

    public string Message { get; private set; } = string.Empty;

    public void Set(string message)
    {
        Message = message;
        expiry = DateTime.UtcNow + Duration;
    }

    /// <summary>Current opacity, 0 once the message has expired.</summary>
    public float Alpha
    {
        get
        {
            if (Message.Length == 0)
                return 0f;

            var remaining = (float)(expiry - DateTime.UtcNow).TotalSeconds;
            return Math.Clamp(remaining / FadeSeconds, 0f, 1f);
        }
    }
}
