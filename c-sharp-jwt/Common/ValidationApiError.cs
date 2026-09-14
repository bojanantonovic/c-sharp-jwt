using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace c_sharp_jwt.Common;

/// <summary>Body of a rejected request, carrying one message per offending field.</summary>
public record ValidationApiError(
    DateTimeOffset Timestamp,
    int Status,
    string Message,
    IReadOnlyDictionary<string, string> Errors)
{
    public static ValidationApiError Of(IReadOnlyDictionary<string, string> errors) =>
        new(DateTimeOffset.UtcNow, StatusCodes.Status400BadRequest, ValidationMessages.ValidationFailed, errors);
}
