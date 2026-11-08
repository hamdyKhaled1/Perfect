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
    private const string DefaultSuperAdminPasswordHash =
        "AQAAAAIAAYagAAAAEMOpkgdlbCDmN7doaTGli8SJG+TupvX/B8BEf2APt7oQ3U0tBT+c5KtbHWFaIGbAGw==";
    private const string DefaultAdminPasswordHash =
        "AQAAAAIAAYagAAAAENFD8mHMfRR78+p7I7vxD0a67o/4zfbDJD8yxVQr4eUgqCombNUasQCjAbhI3h/eUA==";

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
                GetPasswordHash(configuration, "SuperAdminPasswordHash", DefaultSuperAdminPasswordHash)),
            new PortalAccount(
                "admin",
                PortalRoles.Admin,
                GetPasswordHash(configuration, "AdminPasswordHash", DefaultAdminPasswordHash))
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

    private static string GetPasswordHash(IConfiguration configuration, string key, string defaultHash)
    {
        return string.IsNullOrWhiteSpace(configuration[$"PortalAccounts:{key}"])
            ? defaultHash
            : configuration[$"PortalAccounts:{key}"]!;
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
