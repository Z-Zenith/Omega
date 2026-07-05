using OtpNet;

namespace BackendApi.Services;

public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public bool ValidateCode(string base32Secret, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));
    }

    public string BuildProvisioningUri(string base32Secret, string accountIdentifier, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountIdentifier}");
        var issuerParam = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={issuerParam}&digits=6&period=30";
    }
}
