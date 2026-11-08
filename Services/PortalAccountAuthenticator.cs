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
    private readonly string[] _missingSettings;

    public PortalAccountAuthenticator(IConfiguration configuration)
    {
        var superAdminHash = configuration["PortalAccounts:SuperAdminPasswordHash"];
        var adminHash = configuration["PortalAccounts:AdminPasswordHash"];
        _missingSettings = new[]
            {
                string.IsNullOrWhiteSpace(superAdminHash) ? "PortalAccounts__SuperAdminPasswordHash" : null,
                string.IsNullOrWhiteSpace(adminHash) ? "PortalAccounts__AdminPasswordHash" : null
            }
            .Where(setting => setting != null)
            .Select(setting => setting!)
            .ToArray();

        _accounts =
        [
            new PortalAccount("superadmin", PortalRoles.SuperAdmin, superAdminHash ?? string.Empty),
            new PortalAccount("admin", PortalRoles.Admin, adminHash ?? string.Empty)
        ];

        _dummyAccount = new PortalAccount("invalid", PortalRoles.Admin, string.Empty);
        _dummyAccount = _dummyAccount with
        {
            PasswordHash = _passwordHasher.HashPassword(_dummyAccount, Guid.NewGuid().ToString())
        };
    }

    public bool IsConfigured => _missingSettings.Length == 0;

    public IReadOnlyList<string> MissingSettings => _missingSettings;

    public PortalAccount? Authenticate(string userName, string password)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Portal account password hashes are not configured.");

        var account = _accounts.FirstOrDefault(candidate =>
            string.Equals(candidate.UserName, userName.Trim().Normalize(NormalizationForm.FormKC), StringComparison.OrdinalIgnoreCase));

        var verifiedAccount = account ?? _dummyAccount;
        var result = _passwordHasher.VerifyHashedPassword(
            verifiedAccount,
            verifiedAccount.PasswordHash,
            NormalizePassword(password));
        return account != null && result != PasswordVerificationResult.Failed ? account : null;
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
