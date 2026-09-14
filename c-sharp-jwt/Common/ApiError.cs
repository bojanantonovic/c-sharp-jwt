namespace c_sharp_jwt.Common;

/// <summary>Body of every error response that does not carry per-field information.</summary>
public record ApiError(DateTimeOffset Timestamp, int Status, string Message)
{
    public static ApiError Of(int status, string message) => new(DateTimeOffset.UtcNow, status, message);
}
