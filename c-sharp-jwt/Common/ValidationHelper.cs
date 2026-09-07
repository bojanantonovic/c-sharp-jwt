using System.ComponentModel.DataAnnotations;

namespace c_sharp_jwt.Common;

public static class ValidationHelper
{
    public static Dictionary<string, string> Validate<T>(T instance) where T : notnull
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        return results
            .SelectMany(result => result.MemberNames.Select(member => (member, result.ErrorMessage)))
            .ToDictionary(
                entry => char.ToLowerInvariant(entry.member[0]) + entry.member[1..],
                entry => entry.ErrorMessage ?? "Invalid value");
    }
}
