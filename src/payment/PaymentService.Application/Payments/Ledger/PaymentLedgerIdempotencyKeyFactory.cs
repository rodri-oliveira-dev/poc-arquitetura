using System.Security.Cryptography;
using System.Text;

using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Ledger;

public static class PaymentLedgerIdempotencyKeyFactory
{
    private const string Operation = "ledger-credit";

    public static Guid CreateForCredit(PaymentId paymentId)
    {
        var input = $"payment:{paymentId.Value:N}:{Operation}";
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(input), hash);

        Span<byte> bytes = stackalloc byte[16];
        hash[..16].CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
