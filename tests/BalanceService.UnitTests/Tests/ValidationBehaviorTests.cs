using BalanceService.Application.Balances.Queries;
using BalanceService.Application.Common.Behaviors;

using FluentAssertions;

using FluentValidation;

namespace BalanceService.UnitTests.Tests;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_should_throw_validation_exception_when_request_is_invalid()
    {
        var validators = new IValidator<GetPeriodBalanceQuery>[]
        {
            new GetPeriodBalanceQueryValidator()
        };

        var sut = new ValidationBehavior<GetPeriodBalanceQuery, string>(validators);
        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 12), new DateOnly(2026, 2, 10));

        var act = () => sut.Handle(query, _ => Task.FromResult("ok"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_should_continue_pipeline_when_request_is_valid()
    {
        var validators = new IValidator<GetDailyBalanceQuery>[]
        {
            new GetDailyBalanceQueryValidator()
        };

        var sut = new ValidationBehavior<GetDailyBalanceQuery, string>(validators);
        var query = new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10));

        var result = await sut.Handle(query, _ => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }
}
