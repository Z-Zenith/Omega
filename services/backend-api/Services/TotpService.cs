using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;

namespace BackendApi.Services;

// #131: TOTP secrets are encrypted at rest via ASP.NET Core Data Protection. The purpose
// string below is part of the key derivation — do not change it without a migration plan,
// existing ciphertext would become unreadable.
public class TotpService : ITotpService
{
    private const string ProtectorPurpose = "BackendApi.TotpSecret.v1";

    private readonly IDataProtector _protector;

    public TotpService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    }

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string Protect(string base32Secret) => _protector.Protect(base32Secret);

    public bool ValidateCode(string protectedSecret, string code)
    {
        string base32Secret;
        try
        {
            base32Secret = _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            // Ciphertext couldn't be decrypted (corrupt data, wrong/rotated key ring, or —
            // pre-migration — a legacy plaintext value that isn't valid protected payload).
            // Treat as an invalid code rather than letting the exception surface.
            return false;
        }

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
