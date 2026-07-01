namespace TransferService.Api.Security;

public static class ScopePolicies
{
    public const string ClaimType = "scope";

    public const string TransferRead = "transfer.read";
    public const string TransferWrite = "transfer.write";

    public const string TransferReadPolicy = "scope:transfer.read";
    public const string TransferWritePolicy = "scope:transfer.write";
}
