using BackendApi.Services;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;

namespace BackendApi.Tests.Services;

public class TotpServiceTests
{
    // EphemeralDataProtectionProvider is the framework-provided in-memory provider meant
    // for exactly this — tests that need real Protect/Unprotect round-tripping without
    // touching disk or requiring a persisted key ring.
    private readonly TotpService _service = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void GenerateSecret_ProducesValidBase32EncodedKey()
    {
        var secret = _service.GenerateSecret();

        var bytes = Base32Encoding.ToBytes(secret);
        Assert.Equal(20, bytes.Length);
    }

    // #131 — the value persisted to storage must be Protect()'d ciphertext, never the raw
    // Base32 secret GenerateSecret() returns.
    [Fact]
    public void Protect_DoesNotReturnRawSecret()
    {
        var secret = _service.GenerateSecret();

        var protectedSecret = _service.Protect(secret);

        Assert.NotEqual(secret, protectedSecret);
        Assert.DoesNotContain(secret, protectedSecret);
    }

    // #131 — round-trip: generate, protect (as done on write), then validate against the
    // protected form (as done on every login/verify read) must succeed end-to-end.
    [Fact]
    public void ValidateCode_ReturnsTrue_ForCurrentCode_AgainstProtectedSecret()
    {
        var secret = _service.GenerateSecret();
        var protectedSecret = _service.Protect(secret);
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var code = totp.ComputeTotp();

        Assert.True(_service.ValidateCode(protectedSecret, code));
    }

    [Fact]
    public void ValidateCode_ReturnsFalse_ForWrongCode_AgainstProtectedSecret()
    {
        var secret = _service.GenerateSecret();
        var protectedSecret = _service.Protect(secret);

        Assert.False(_service.ValidateCode(protectedSecret, "000000"));
    }

    // Defends against a partial fix: if ValidateCode ever regressed to expecting the raw
    // Base32 secret instead of the protected form, this would start failing.
    [Fact]
    public void ValidateCode_ReturnsFalse_WhenGivenRawUnprotectedSecret()
    {
        var secret = _service.GenerateSecret();
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var code = totp.ComputeTotp();

        Assert.False(_service.ValidateCode(secret, code));
    }

    [Fact]
    public void BuildProvisioningUri_IncludesIssuerAndAccountIdentifier()
    {
        var uri = _service.BuildProvisioningUri("SECRETKEY", "parent-1@college", "Omega");

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("issuer=Omega", uri);
        Assert.Contains("secret=SECRETKEY", uri);
    }
}
