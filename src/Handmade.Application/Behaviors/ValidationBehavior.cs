using FluentValidation;
using FluentValidation.Results;

namespace Handmade.Application.Behaviors;

/// <summary>
/// Invokes FluentValidation validators for a request model.
/// Use from application services until a richer pipeline is justified.
/// </summary>
public static class ValidationBehavior
{
    public static async Task ValidateAndThrowAsync<T>(
        T instance,
        IEnumerable<IValidator<T>> validators,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        IValidator<T>[] validatorList = validators as IValidator<T>[] ?? validators.ToArray();
        if (validatorList.Length == 0)
        {
            return;
        }

        ValidationContext<T> context = new(instance);
        ValidationResult[] results = await Task.WhenAll(
            validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));

        List<ValidationFailure> failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
