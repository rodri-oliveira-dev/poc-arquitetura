namespace PaymentService.Application.Common.Exceptions;

public sealed class ForbiddenException(string message) : Exception(message)
{
}
