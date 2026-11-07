using Microsoft.AspNetCore.Identity;
using System.Text;

namespace HagImamAccounts.Services;

public static class PortalRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
}

public sealed record PortalAccount(string UserName, string Role, string PasswordHash);

public sealed class PortalAccountAuthenticator
{
    private readonly PasswordHasher<PortalAccount> _passwordHasher = new();
    private readonly PortalAccount[] _accounts;
    private readonly PortalAccount _dummyAccount;

    public PortalAccountAuthenticator(IConfiguration configuration)
    {
        _accounts =
        [
            new PortalAccount(
                "superadmin",
                PortalRoles.SuperAdmin,
                GetRequiredPasswordHash(configuration, "SuperAdminPasswordHash")),
            new PortalAccount(
                "admin",
                PortalRoles.Admin,
                GetRequiredPasswordHash(configuration, "AdminPasswordHash"))
        ];

        _dummyAccount = new PortalAccount("invalid", PortalRoles.Admin, string.Empty);
        _dummyAccount = _dummyAccount with
        {
            PasswordHash = _passwordHasher.HashPassword(_dummyAccount, Guid.NewGuid().ToString())
        };
    }

    public PortalAccount? Authenticate(string userName, string password)
    {
        var account = _accounts.FirstOrDefault(candidate =>
            string.Equals(candidate.UserName, userName.Trim().Normalize(NormalizationForm.FormKC), StringComparison.OrdinalIgnoreCase));

        var verifiedAccount = account ?? _dummyAccount;
        var result = _passwordHasher.VerifyHashedPassword(
            verifiedAccount,
            verifiedAccount.PasswordHash,
            NormalizePassword(password));
        return account != null && result != PasswordVerificationResult.Failed ? account : null;
    }

    private static string GetRequiredPasswordHash(IConfiguration configuration, string key)
    {
        return configuration[$"PortalAccounts:{key}"] is { Length: > 0 } hash
            ? hash
            : throw new InvalidOperationException(
                $"Missing required configuration value: PortalAccounts:{key}. Configure it using user-secrets or environment variables.");
    }

    private static string NormalizePassword(string password)
    {
        var normalized = password.Normalize(NormalizationForm.FormKC);
        return string.Create(normalized.Length, normalized, static (buffer, value) =>
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                buffer[index] = character switch
                {
                    >= '\u0660' and <= '\u0669' => (char)('0' + character - '\u0660'),
                    >= '\u06F0' and <= '\u06F9' => (char)('0' + character - '\u06F0'),
                    _ => character
                };
            }
        });
    }
}
