using ApplicationDefaults.Behaviors;

using BalanceService.Application.Balances.Queries;

using FluentValidation;

namespace BalanceService.UnitTests.Application.Common.Behaviors;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_should_throw_validation_exception_when_request_is_invalidAsync()
    {
        var validators = new IValidator<GetPeriodBalanceQuery>[]
        {
            new GetPeriodBalanceQueryValidator()
        };

        var sut = new ValidationBehavior<GetPeriodBalanceQuery, string>(validators);
        var query = new GetPeriodBalanceQuery("m1", new DateOnly(2026, 2, 12), new DateOnly(2026, 2, 10));

        Task<string> act()
        {
            return sut.Handle(query, _ => Task.FromResult("ok"), CancellationToken.None);
        }

        await Assert.ThrowsAsync<ValidationException>((Func<Task<string>>)act);
    }

    [Fact]
    public async Task Handle_should_continue_pipeline_when_request_is_validAsync()
    {
        var validators = new IValidator<GetDailyBalanceQuery>[]
        {
            new GetDailyBalanceQueryValidator()
        };

        var sut = new ValidationBehavior<GetDailyBalanceQuery, string>(validators);
        var query = new GetDailyBalanceQuery("m1", new DateOnly(2026, 2, 10));

        var result = await sut.Handle(query, _ => Task.FromResult("ok"), CancellationToken.None);
        Assert.Equal("ok", result);
    }
}
