using c_sharp_jwt.Auth;
using Microsoft.AspNetCore.Diagnostics;

namespace c_sharp_jwt.Common;

public record ApiError(DateTimeOffset Timestamp, int Status, string Message);

public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            EmailAlreadyInUseException ex => (StatusCodes.Status409Conflict, ex.Message),
            InvalidCredentialsException ex => (StatusCodes.Status401Unauthorized, ex.Message),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiError(DateTimeOffset.UtcNow, status, message), cancellationToken);
        return true;
    }
}
