namespace BackendApi.Services;

// #140: no server-side password strength check existed anywhere before bcrypt-hashing — a
// 1-character password was accepted on every one of the three password-setting paths
// (UsersController.Create's InitialPassword, UsersController.ResetPassword, AuthController.
// ChangePassword). Centralized here so all three enforce the identical rule instead of each
// controller inventing (and potentially disagreeing on) its own.
public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    public static bool IsValid(string? password, out string error)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumLength)
        {
            error = $"Password must be at least {MinimumLength} characters long.";
            return false;
        }

        var hasLetter = password.Any(char.IsLetter);
        var hasDigitOrSymbol = password.Any(c => char.IsDigit(c) || !char.IsLetterOrDigit(c));
        if (!hasLetter || !hasDigitOrSymbol)
        {
            error = "Password must contain at least one letter and at least one digit or symbol.";
            return false;
        }

        error = "";
        return true;
    }
}
