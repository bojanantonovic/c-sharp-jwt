using c_sharp_jwt.Auth;
using Microsoft.AspNetCore.Diagnostics;

namespace c_sharp_jwt.Common;

/// <summary>
/// Turns the exceptions the application raises on purpose into their HTTP status codes. Anything else is left to
/// the default handling, which answers with a 500 and does not leak the message.
/// </summary>
public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            EmailAlreadyInUseException => StatusCodes.Status409Conflict,
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            _ => 0
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(ApiError.Of(status, exception.Message), cancellationToken);

        return true;
    }
}
