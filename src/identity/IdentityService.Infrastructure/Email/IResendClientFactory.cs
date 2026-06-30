using Resend;

namespace IdentityService.Infrastructure.Email;

public interface IResendClientFactory
{
    IResend CreateClient();
}
