namespace BackendApi.Services;

public interface ITotpService
{
    string GenerateSecret();

    bool ValidateCode(string base32Secret, string code);

    string BuildProvisioningUri(string base32Secret, string accountIdentifier, string issuer);
}
