using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Common;

public static class ValidationHelper
{
    /// <summary>
    /// Collects the data annotation failures of <paramref name="instance"/>, keyed by the offending property in
    /// the same camel case the property is serialized with. A property that breaks several rules is reported
    /// once, with the message of the first rule it broke.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Validate<T>(T instance) where T : notnull
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        return results
            .SelectMany(result => result.MemberNames.Select(memberName => (memberName, result.ErrorMessage)))
            .GroupBy(failure => ToCamelCase(failure.memberName))
            .ToDictionary(
                failures => failures.Key,
                failures => failures.First().ErrorMessage ?? ValidationMessages.InvalidValue);
    }

    private static string ToCamelCase(string memberName) =>
        char.ToLowerInvariant(memberName[0]) + memberName[1..];
}
