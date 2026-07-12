using System.Security.Cryptography;
using System.Text;

using PaymentService.Domain.Payments;

namespace PaymentService.Application.Payments.Ledger;

public static class PaymentLedgerIdempotencyKeyFactory
{
    private const string Operation = "ledger-credit";
    private const string RefundOperation = "ledger-reversal";

    public static Guid CreateForCredit(PaymentId paymentId)
        => Create($"payment:{paymentId.Value:N}:{Operation}");

    public static Guid CreateForRefundReversal(PaymentId paymentId, RefundId refundId)
        => Create($"payment:{paymentId.Value:N}:refund:{refundId.Value:N}:{RefundOperation}");

    private static Guid Create(string input)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(input), hash);

        Span<byte> bytes = stackalloc byte[16];
        hash[..16].CopyTo(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
