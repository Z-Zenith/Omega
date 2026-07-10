namespace BackendApi.Services;

public interface ITotpService
{
    // Returns a raw Base32-encoded secret. Callers must Protect() it before persisting —
    // never write the return value of this method straight into User.TotpSecret (#131).
    string GenerateSecret();

    // Encrypts a raw Base32 secret (via ASP.NET Core Data Protection) for storage in
    // User.TotpSecret. This is the only form of the secret that should ever reach the DB.
    string Protect(string base32Secret);

    // Takes the *protected* (encrypted) secret as stored in User.TotpSecret, decrypts it,
    // and verifies the submitted code against it. Returns false (rather than throwing) if
    // the ciphertext can't be unprotected, e.g. corrupt data or a key-ring rotation.
    bool ValidateCode(string protectedSecret, string code);

    // Takes the raw Base32 secret (not the protected form) — used once at account-creation
    // time to build the provisioning URI/QR code before the raw value is discarded.
    string BuildProvisioningUri(string base32Secret, string accountIdentifier, string issuer);
}
