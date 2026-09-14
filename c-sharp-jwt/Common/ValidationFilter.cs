namespace c_sharp_jwt.Common;

/// <summary>
/// The minimal API counterpart of Spring's <c>@Valid</c>: it validates the request body before the endpoint
/// delegate runs, so no endpoint has to repeat the check or the error shape.
/// </summary>
public class ValidationFilter<TRequest> : IEndpointFilter where TRequest : notnull
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return Results.BadRequest(ApiError.Of(StatusCodes.Status400BadRequest,
                                                  ValidationMessages.MissingRequestBody));
        }

        var errors = ValidationHelper.Validate(request);

        return errors.Count > 0
            ? Results.BadRequest(ValidationApiError.Of(errors))
            : await next(context);
    }
}
