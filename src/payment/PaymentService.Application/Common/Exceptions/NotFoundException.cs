namespace PaymentService.Application.Common.Exceptions;

public sealed class NotFoundException(string message) : Exception(message)
{
}
