using System.Security.Cryptography;

namespace Auth.Api.Security;

public interface IRsaKeyProvider
{
    RSA GetPrivateKey();
    RSA GetPublicKey();
    string GetKeyId();
}
