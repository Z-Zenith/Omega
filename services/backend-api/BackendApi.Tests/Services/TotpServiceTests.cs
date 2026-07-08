using BackendApi.Services;
using OtpNet;

namespace BackendApi.Tests.Services;

public class TotpServiceTests
{
    private readonly TotpService _service = new();

    [Fact]
    public void GenerateSecret_ProducesValidBase32EncodedKey()
    {
        var secret = _service.GenerateSecret();

        var bytes = Base32Encoding.ToBytes(secret);
        Assert.Equal(20, bytes.Length);
    }

    [Fact]
    public void ValidateCode_ReturnsTrue_ForCurrentCode()
    {
        var secret = _service.GenerateSecret();
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        var code = totp.ComputeTotp();

        Assert.True(_service.ValidateCode(secret, code));
    }

    [Fact]
    public void ValidateCode_ReturnsFalse_ForWrongCode()
    {
        var secret = _service.GenerateSecret();

        Assert.False(_service.ValidateCode(secret, "000000"));
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
