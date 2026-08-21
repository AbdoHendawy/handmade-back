using FluentValidation;
using Handmade.Application.Behaviors;

namespace Handmade.Application.Tests;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task ValidateAndThrowAsync_WhenValid_DoesNotThrow()
    {
        SampleRequest request = new("Handmade");
        SampleValidator validator = new();

        await ValidationBehavior.ValidateAndThrowAsync(request, [validator]);
    }

    [Fact]
    public async Task ValidateAndThrowAsync_WhenInvalid_ThrowsValidationException()
    {
        SampleRequest request = new("");
        SampleValidator validator = new();

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => ValidationBehavior.ValidateAndThrowAsync(request, [validator]));

        Assert.Contains(exception.Errors, e => e.PropertyName == nameof(SampleRequest.Name));
    }

    private sealed record SampleRequest(string Name);

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
