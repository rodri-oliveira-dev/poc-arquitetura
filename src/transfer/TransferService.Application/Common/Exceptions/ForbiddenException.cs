namespace TransferService.Application.Common.Exceptions;

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
