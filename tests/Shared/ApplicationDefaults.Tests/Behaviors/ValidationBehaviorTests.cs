using ApplicationDefaults.Behaviors;

using FluentValidation;

namespace ApplicationDefaults.Tests.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_should_throw_validation_exception_when_request_is_invalidAsync()
    {
        var validators = new IValidator<FakeRequest>[]
        {
            new FakeRequestValidator()
        };

        var sut = new ValidationBehavior<FakeRequest, string>(validators);
        var request = new FakeRequest(Name: "", Amount: 10);

        Task<string> Act(CancellationToken cancellationToken)
        {
            return sut.Handle(request, _ => Task.FromResult("ok"), cancellationToken);
        }

        await Assert.ThrowsAsync<ValidationException>(() => Act(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_should_continue_pipeline_when_request_is_validAsync()
    {
        var validators = new IValidator<FakeRequest>[]
        {
            new FakeRequestValidator()
        };

        var sut = new ValidationBehavior<FakeRequest, string>(validators);
        var request = new FakeRequest(Name: "valid", Amount: 10);

        var result = await sut.Handle(request, _ => Task.FromResult("ok"), TestContext.Current.CancellationToken);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_should_continue_pipeline_when_no_validators_are_registeredAsync()
    {
        var sut = new ValidationBehavior<FakeRequest, string>([]);
        var request = new FakeRequest(Name: "", Amount: 0);

        var result = await sut.Handle(request, _ => Task.FromResult("ok"), TestContext.Current.CancellationToken);

        Assert.Equal("ok", result);
    }

    private sealed record FakeRequest(string Name, decimal Amount);

    private sealed class FakeRequestValidator : AbstractValidator<FakeRequest>
    {
        public FakeRequestValidator()
        {
            RuleFor(request => request.Name).NotEmpty();
            RuleFor(request => request.Amount).GreaterThan(0);
        }
    }
}
